using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using dnlib.DotNet;
using dnlib.DotNet.MD;
using dnlib.PE;
using System.Windows.Forms;
using System.ComponentModel;
using System.Threading;
using dnlib.DotNet.Writer;

namespace ACEPatcher.ModuleLoader
{
    class DotNetModuleLoader
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenThread(int dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

        [DllImport("kernel32.dll")]
        private static extern uint SuspendThread(IntPtr hThread);

        [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("dbghelp.dll",SetLastError = true)]
        public static extern bool MiniDumpWriteDump(IntPtr hProcess,uint processId,SafeHandle hFile,int dumpType,IntPtr expParam,IntPtr userStreamParam,IntPtr callbackParam);

        static int BitNessConstant = IntPtr.Size == 4 ? 0x010B : 0x020B;

        static bool IsDotNetAssembly(string path)
        {
            PEImage image = new PEImage(path);
            return image.ImageNTHeaders.OptionalHeader.DataDirectories[14].VirtualAddress != 0;
        }

        static bool DoesBitnessMatch(string path)
        {
            PEImage image = new PEImage(path);
            return image.ImageNTHeaders.OptionalHeader.Magic == BitNessConstant;
        }

        public static ModuleDefMD LoadModule(string path,out ModuleDefMD[] dependancies,out string[] failed) 
        {
            failed = null;
            if (IsDotNetAssembly(path)) 
            {
                ModuleDefMD m = ModuleDefMD.Load(path);
                if (m != null)
                    dependancies = LoadDependancies(m, path, out failed);
                else
                    dependancies = null;
                return m;
            }
            else 
            {
                ModuleDefMD m = TryLoadPackedDotNetAssembly(path);
                if (m != null)
                    dependancies = LoadDependancies(m, path, out failed);
                else
                    dependancies = null;
                return m;
            }
        } 

        static ModuleDefMD TryLoadPackedDotNetAssembly(string path) 
        {
            if (!DoesBitnessMatch(path)) 
            {
                MessageBox.Show("Please use " + (BitNessConstant == 0x010B ? "64bit" : "32bit") + " version of ACEPatcher on this assembly");
                return null;
            }
            Process process;
            if((process = Freeze(path)) != null) 
            {
                string dumpFile = DumpProccess(process);
                process.Kill();
                ModuleDefMD moduleDef = dumpMainModule(dumpFile);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                File.Delete(dumpFile);
                return moduleDef;
            }
            else 
            {
                return null;
            }
        }

        static Process Freeze(string path) 
        {
            Process process = new Process();
            process.StartInfo.FileName = path;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            process.Start();
            while (true) 
            {
                if (GetCLRModule(process.Id)) 
                {
                    SuspendThreads(process.Id);
                    return process;
                }
                if(stopwatch.ElapsedMilliseconds >= 10000) 
                {
                    process.Close();
                    return null;
                }
            }
        }

        static bool GetCLRModule(int pID)
        {
            ProcessModuleCollection modules = Process.GetProcessById(pID).Modules;
            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i].ModuleName.ToLower() == "clr.dll")
                {
                    return true;
                }
            }
            return false;
        }

        static void SuspendThreads(int pid)
        {
            Process processById = Process.GetProcessById(pid);
            if (string.IsNullOrEmpty(processById.ProcessName))
            {
                return;
            }
            foreach (object obj in processById.Threads)
            {
                ProcessThread processThread = (ProcessThread)obj;
                IntPtr intPtr = OpenThread(2, false, (uint)processThread.Id);
                if (!(intPtr == IntPtr.Zero))
                {
                    SuspendThread(intPtr);
                    CloseHandle(intPtr);
                }
            }
        }

        static string DumpProccess(Process process) 
        {
            string result = Path.GetTempFileName();
            using (var fs = new FileStream(result, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                if (!MiniDumpWriteDump(process.Handle, (uint)process.Id, fs.SafeFileHandle, 2, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            return result;
        }

        static unsafe ModuleDefMD dumpMainModule(string fileName)
        {
            byte[] dumpData = File.ReadAllBytes(fileName);
            fixed (byte* dump = dumpData)
            {
                for (int i = 0; i < dumpData.Length; i++)
                {
                    if (dump[i] == 0x4D && dump[i + 1] == 0x5A)
                    {
                        int NTHeaderOffset = *(int*)(dump + i + 0x3C);

                        if (*(int*)(dump + i + NTHeaderOffset) == 0x00004550)
                        {
                            if (*(short*)(dump + i + NTHeaderOffset + 0x18) == 0x010B || *(short*)(dump + i + NTHeaderOffset + 0x18) == 0x020B)
                            {
                                PEImage image = new PEImage((IntPtr)(dump + i), false);
                                uint alignment = image.ImageNTHeaders.OptionalHeader.FileAlignment;
                                uint imageSize = image.ImageSectionHeaders.Last().PointerToRawData + image.ImageSectionHeaders.Last().SizeOfRawData;
                                if (imageSize % alignment != 0)
                                    imageSize = imageSize - (imageSize % alignment) + alignment;
                                byte[] AssemblyData = new byte[imageSize];
                                Marshal.Copy((IntPtr)(dump + i), AssemblyData, 0, (int)imageSize);
                                if (image.ImageNTHeaders.OptionalHeader.DataDirectories[14].VirtualAddress != 0)
                                {
                                    ModuleDefMD moduleDef = ModuleDefMD.Load(AssemblyData);
                                    if (moduleDef.Name.EndsWith(".exe")) 
                                    {
                                        return moduleDef;
                                    }
                                    else
                                        moduleDef.Dispose();
                                }

                            }
                        }
                    }

                }
            }
            return null;
        }

        public static ModuleDefMD[] LoadDependancies(ModuleDefMD md, string path, out string[] failed)
        {
            List<string> f = new List<string>();
            List<ModuleDefMD> result = new List<ModuleDefMD>();
            List<AssemblyRef> refs = md.GetAssemblyRefs().ToList();
            List<AssemblyRef> backup = new List<AssemblyRef>(refs);
            string[] files = Directory.GetFiles(Path.GetDirectoryName(path)).Where(x => x.ToLower().EndsWith(".dll") || x.ToLower().EndsWith(".exe")).ToArray();
            foreach (string file in files)
            {
                try
                {
                    using (PEImage image = new PEImage(file))
                    {
                        if (IsDotNetAssembly(file))
                        {
                            AssemblyName name = AssemblyName.GetAssemblyName(file);
                            if (refs.Any(x => x.FullName == name.FullName))
                            {
                                result.Add(ModuleDefMD.Load(file));
                                refs.Remove(refs.Where(x => x.FullName == name.FullName).First());
                                backup.Remove(backup.Where(x => x.FullName == name.FullName).First());
                            }
                        }
                    }
                }
                catch
                {

                }
            }
            foreach (AssemblyRef r in refs)
            {
                try
                {
                    AssemblyName name = new AssemblyName(r.FullName);
                    result.Add(ModuleDefMD.Load(Assembly.Load(name).ManifestModule));
                    backup.Remove(r);
                }
                catch
                {

                }
            }
            foreach (AssemblyRef r in backup)
                f.Add(r.FullName);
            failed = f.ToArray();
            return result.ToArray();
        }


    }

    class NoErrorLoggin : ILogger
    {
        public bool IgnoresEvent(LoggerEvent loggerEvent)
        {
            return true;
        }

        public void Log(object sender, LoggerEvent loggerEvent, string format, params object[] args)
        {
            
        }
    }
}
