using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Forms;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.PE;


namespace ACEPatcher
{
    class Patcher
    {
        public static ModuleDefMD patchAssembly;
        public static ModuleDefMD _0harmony = ModuleDefMD.Load(ACEFiles._0Harmony);
        public static string patchAssemblyPath;
        public static Dictionary<IMethod, IMethod> patchList = new Dictionary<IMethod, IMethod>();
        
        private static bool CompareSignature(IMethod method1,IMethod method2) 
        {
            if (method1.MethodSig.RetType.FullName != method2.MethodSig.RetType.FullName)
                return false;
            if (((MethodDef)method1).HasThis)
            {
                if (method1.MethodSig.Params.Count+1 != method2.MethodSig.Params.Count)
                    return false;
                if (method1.DeclaringType.FullName != method2.GetParam(0).FullName)
                    return false;
                int i = 0;
                for(int j = 0; j < method1.MethodSig.Params.Count; j++) 
                {
                    if (method1.MethodSig.Params[j].FullName == method2.MethodSig.Params[j+1].FullName)
                        i++;
                }
                return method1.MethodSig.Params.Count == i;
            }
            else
            {
                if (method1.MethodSig.Params.Count != method2.MethodSig.Params.Count)
                    return false;
                int i = 0;
                for (int j = 0; j < method1.MethodSig.Params.Count; j++)
                {
                    if (method1.MethodSig.Params[j].FullName == method2.MethodSig.Params[j].FullName)
                        i++;
                }
                return i == method1.MethodSig.Params.Count;
            }
        }

        private static OpCode DetermineStindType(TypeSig sig) 
        {
            if (sig == patchAssembly.CorLibTypes.IntPtr)
                return OpCodes.Stind_I;
            else if (sig == patchAssembly.CorLibTypes.SByte || sig == patchAssembly.CorLibTypes.Byte || sig == patchAssembly.CorLibTypes.Boolean)
                return OpCodes.Stind_I1;
            else if (sig == patchAssembly.CorLibTypes.Int16 || sig == patchAssembly.CorLibTypes.UInt16 || sig == patchAssembly.CorLibTypes.Char)
                return OpCodes.Stind_I2;
            else if (sig == patchAssembly.CorLibTypes.Int32 || sig == patchAssembly.CorLibTypes.UInt32)
                return OpCodes.Stind_I4;
            else if (sig == patchAssembly.CorLibTypes.Int64 || sig == patchAssembly.CorLibTypes.UInt64)
                return OpCodes.Stind_I8;
            else if (sig == patchAssembly.CorLibTypes.Single)
                return OpCodes.Stind_R4;
            else if (sig == patchAssembly.CorLibTypes.Double)
                return OpCodes.Stind_R8;
            else
                return OpCodes.Stind_Ref;
        }

        private static OpCode DetermineLdindType(TypeSig sig)
        {
            if (sig == patchAssembly.CorLibTypes.IntPtr)
                return OpCodes.Ldind_I;
            else if (sig == patchAssembly.CorLibTypes.SByte || sig == patchAssembly.CorLibTypes.Byte || sig == patchAssembly.CorLibTypes.Boolean)
                return OpCodes.Ldind_I1;
            else if (sig == patchAssembly.CorLibTypes.Int16 || sig == patchAssembly.CorLibTypes.UInt16 || sig == patchAssembly.CorLibTypes.Char)
                return OpCodes.Ldind_I2;
            else if (sig == patchAssembly.CorLibTypes.Int32 || sig == patchAssembly.CorLibTypes.UInt32)
                return OpCodes.Ldind_I4;
            else if (sig == patchAssembly.CorLibTypes.Int64 || sig == patchAssembly.CorLibTypes.UInt64)
                return OpCodes.Ldind_I8;
            else if (sig == patchAssembly.CorLibTypes.Single)
                return OpCodes.Ldind_R4;
            else if (sig == patchAssembly.CorLibTypes.Double)
                return OpCodes.Ldind_R8;
            else
                return OpCodes.Ldind_Ref;
        }

        public static void AddPatchToList(IMethod method) 
        {
            if(patchAssembly == null) 
            {
                OpenFileDialog dialog = new OpenFileDialog()
                {
                    Title = "Load assembly",
                    Filter = "Executable|*.exe|Dynamic Link Library|*.dll",
                    CheckFileExists = true
                };
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        patchAssembly = ModuleDefMD.Load(dialog.FileName);
                        patchAssemblyPath = dialog.FileName;
                    }
                    catch 
                    {
                        MessageBox.Show("Invalid Assembly", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                    return;
            }
            PatchDialog patchDialog = new PatchDialog();
            if(patchDialog.ShowDialog() == DialogResult.OK) 
            {
                if (((MethodDef)patchDialog.SelectedMethod).HasThis) 
                {
                    MessageBox.Show("The chosen method must be static", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (CompareSignature(method, patchDialog.SelectedMethod)) 
                {
                    if (!patchList.ContainsKey(method))
                        patchList.Add(method, patchDialog.SelectedMethod);
                    else
                        MessageBox.Show("Method already patched", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else 
                {
                    MessageBox.Show("Method signature mismatch","Error",MessageBoxButtons.OK,MessageBoxIcon.Error);
                }
            }
        }

        private static MethodDefUser GenerateMethod(IMethod method,Importer importer) 
        {
            MethodDefUser resultMethod = new MethodDefUser("ACE" + Guid.NewGuid().ToString("N"), new MethodSig(CallingConvention.Default, 0, importer.Import(typeof(System.Reflection.MethodBase)).ToTypeSig()), MethodAttributes.Public | MethodAttributes.Static);
            resultMethod.Body = new CilBody();
            resultMethod.Body.Instructions.Add(new Instruction(OpCodes.Ldtoken, method.DeclaringType.DefinitionAssembly.FullName == patchAssembly.Assembly.FullName ? ((MethodDef)method) : importer.Import((MethodDef)method)));
            resultMethod.Body.Instructions.Add(new Instruction(OpCodes.Call, importer.Import(typeof(System.Reflection.MethodBase).GetMethod("GetMethodFromHandle", new Type[] { typeof(RuntimeMethodHandle) }))));
            resultMethod.Body.Instructions.Add(new Instruction(OpCodes.Ret));
            return resultMethod;
        }

        public static void ApplyPatches(string MainAssemblyPath) 
        {
            byte[] backup = DotNetUtils.ReadAssembly(patchAssembly);
            Dictionary<MethodDef, IMethod> patchList2 = new Dictionary<MethodDef, IMethod>();
            foreach (KeyValuePair<IMethod,IMethod> patch in patchList) 
            {
                MethodSig sig = new MethodSig();
                sig.CallingConvention = CallingConvention.Default;
                sig.RetType = patchAssembly.CorLibTypes.Boolean;
                foreach(TypeSig typeSig in patch.Value.MethodSig.Params) 
                    sig.Params.Add(new ByRefSig( typeSig));
                    sig.Params.Add(new ByRefSig(patch.Value.MethodSig.RetType)); 
                MethodDefUser method = new MethodDefUser("Prefix" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),sig,MethodAttributes.Public| MethodAttributes.Static);
                List<ParamDef> paramds = ((MethodDef)patch.Value).ParamDefs.ToList();
                if (((MethodDef)patch.Key).HasThis)
                {
                    method.ParamDefs.Add(new ParamDefUser("__instance", 1));
                    paramds.RemoveAt(0);
                }
                foreach (ParamDef pdef in paramds)
                    method.ParamDefs.Add(new ParamDefUser(pdef.Name, (ushort)(method.ParamDefs.Count + 1)));
                method.ParamDefs.Add(new ParamDefUser("__result",(ushort)(method.ParamDefs.Count+1) ));
                method.Body = new CilBody();
                DotNetUtils.CopyBodyFromTo((MethodDef)patch.Value, method);
                if(((MethodDef)patch.Value).HasReturnType)
                method.Body.Variables.Add(new Local(patch.Value.MethodSig.RetType));
                int stack = 0;
                int LastStackZero = 0;
                method.Body.Instructions.SimplifyMacros(method.Body.Variables, method.Parameters);
                for(int i = 0; i < method.Body.Instructions.Count; i++) 
                {
                    if (stack == 0)
                        LastStackZero = i;
                    if(method.Body.Instructions[i].OpCode == OpCodes.Ret) 
                    {
                        if(((MethodDef)patch.Value).HasReturnType)
                            i += DotNetUtils.ReplaceInstruction(method.Body.Instructions, method.Body.Instructions[i], Instruction.Create(OpCodes.Stloc_S, method.Body.Variables.Last()), Instruction.Create(OpCodes.Ldarg_S, method.Parameters.Last()), Instruction.Create(OpCodes.Ldloc_S, method.Body.Variables.Last()), Instruction.Create(DetermineStindType(patch.Value.MethodSig.RetType)), Instruction.Create(OpCodes.Ldc_I4_0),Instruction.Create(OpCodes.Ret));
                        else
                            i += DotNetUtils.ReplaceInstruction(method.Body.Instructions, method.Body.Instructions[i],Instruction.Create(OpCodes.Ldc_I4_0), Instruction.Create(OpCodes.Ret));
                        LastStackZero = i;
                        stack = 0;
                    }
                    else if(method.Body.Instructions[i].Operand != null && method.Body.Instructions[i].Operand.GetType().GetInterfaces().Contains(typeof(IMethodDefOrRef)) && ((IMDTokenProvider)method.Body.Instructions[i].Operand).MDToken == patch.Value.MDToken) 
                    {
                        if(method.Body.Instructions[i + 1].OpCode != OpCodes.Ret) 
                        {
                            MessageBox.Show("Invalid call to continue", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                        for(int j = LastStackZero; j < i; j++) 
                        {
                            method.Body.Instructions[j].OpCode = OpCodes.Nop;
                            method.Body.Instructions[j].Operand = null;
                        }
                        i += DotNetUtils.ReplaceInstruction(method.Body.Instructions, method.Body.Instructions[i],new Instruction(OpCodes.Ldc_I4_1));
                        LastStackZero = i;
                        stack = 0;
                    }
                    else if (method.Body.Instructions[i].OpCode.ToString().ToLower().Contains("ldarg")) 
                    {
                        method.Body.Instructions.Insert(i + 1, new Instruction(DetermineLdindType(((Parameter)method.Body.Instructions[i].Operand).Type)));
                    }
                    else if (method.Body.Instructions[i].OpCode.ToString().ToLower().Contains("starg")) 
                    {
                        stack++;
                        method.Body.Instructions.Insert(LastStackZero, new Instruction(OpCodes.Ldarg, method.Body.Instructions[i++].Operand));
                        method.Body.Instructions[i].OpCode = DetermineStindType(((Parameter)method.Body.Instructions[i].Operand).Type);
                        method.Body.Instructions[i].Operand = null;
                    }
                    try
                    {
                        method.Body.Instructions[i].UpdateStack(ref stack);
                    }
                    catch 
                    {
                        break;
                    }
                }
                method.Body.Instructions.UpdateInstructionOffsets();
                method.Body.Instructions.OptimizeMacros();
                ((TypeDef)patch.Value.DeclaringType).Methods.Add(method);
                patchList2.Add(method, patch.Key);
            }
            TypeDefUser patchType = new TypeDefUser("ACE", patchAssembly.CorLibTypes.Object.ToTypeDefOrRef());
            MethodDefUser patchMethod = new MethodDefUser("ACE" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), new MethodSig(CallingConvention.Default, 0, patchAssembly.CorLibTypes.Int32, patchAssembly.CorLibTypes.String), MethodAttributes.Public | MethodAttributes.Static);
            patchAssembly.Types.Add(patchType);
            patchType.Methods.Add(patchMethod);
            patchMethod.Body = new CilBody();
            Importer importer = new Importer(patchAssembly);
            var guid = importer.Import(typeof(Guid));
            patchMethod.Body.Variables.Add(new Local(new ValueTypeSig(guid)));
            foreach (KeyValuePair<MethodDef,IMethod> patch in patchList2) 
            {
                patchMethod.Body.Instructions.Add(new Instruction(OpCodes.Call,importer.Import(typeof(Guid).GetMethod("NewGuid"))));
                patchMethod.Body.Instructions.Add(new Instruction(OpCodes.Stloc_0));
                patchMethod.Body.Instructions.Add(new Instruction(OpCodes.Ldloca_S,patchMethod.Body.Variables[0]));
                patchMethod.Body.Instructions.Add(new Instruction(OpCodes.Ldstr,"N"));
                patchMethod.Body.Instructions.Add(new Instruction(OpCodes.Call, importer.Import(typeof(Guid).GetMethod("ToString",new Type[] { typeof(string)}))));
                patchMethod.Body.Instructions.Add(new Instruction(OpCodes.Newobj, importer.Import(_0harmony.ResolveMethod(438))));
                MethodDef method1 = GenerateMethod(patch.Value, importer);
                patchType.Methods.Add(method1);
                patchMethod.Body.Instructions.Add(new Instruction(OpCodes.Call,method1));
                method1 = GenerateMethod(patch.Key, importer);
                patchType.Methods.Add(method1);
                patchMethod.Body.Instructions.Add(new Instruction(OpCodes.Call, method1));
                patchMethod.Body.Instructions.Add(new Instruction(OpCodes.Castclass, importer.Import(typeof(System.Reflection.MethodInfo))));
                patchMethod.Body.Instructions.Add(new Instruction(OpCodes.Newobj, importer.Import(_0harmony.ResolveMethod(488))));
                patchMethod.Body.Instructions.Add(new Instruction(OpCodes.Ldnull));
                patchMethod.Body.Instructions.Add(new Instruction(OpCodes.Ldnull));
                patchMethod.Body.Instructions.Add(new Instruction(OpCodes.Ldnull));
                patchMethod.Body.Instructions.Add(new Instruction(OpCodes.Call, importer.Import(_0harmony.ResolveMethod(444))));
                patchMethod.Body.Instructions.Add(new Instruction(OpCodes.Pop));
            }
            patchMethod.Body.Instructions.Add(new Instruction(OpCodes.Ldc_I4_1));
            patchMethod.Body.Instructions.Add(new Instruction(OpCodes.Ret));
            MemoryStream stream = new MemoryStream();
            patchAssembly.Write(stream);
            byte[] patchedAssembly = stream.ToArray();
            string path = Path.GetFileNameWithoutExtension(MainAssemblyPath) + "_patch_" + DateTimeOffset.Now.ToUnixTimeSeconds();
            Directory.CreateDirectory(path);
            File.Copy(MainAssemblyPath, path + "/" + Path.GetFileName(MainAssemblyPath));
            File.WriteAllBytes(path + "/0harmony.dll", ACEFiles._0Harmony);
            if(new PEImage(MainAssemblyPath).ImageNTHeaders.OptionalHeader.Magic == 0x010B) 
            {
                File.WriteAllBytes(path + "/DllInjector.exe",ACEFiles.DllInjector);
                File.WriteAllBytes(path + "/ACE.dll",ACEFiles.ACEx32);
            }
            else 
            {
                File.WriteAllBytes(path + "/DllInjector.exe", ACEFiles.DllInjector64);
                File.WriteAllBytes(path + "/ACE.dll", ACEFiles.ACEx64);
            }
            File.WriteAllBytes(path + "/patch", patchedAssembly);
            string command = "DllInjector.exe " + Path.GetFileName(MainAssemblyPath) + " ACE.dll patch " + patchType.Name + "." + patchMethod.Name;
            File.WriteAllText(path + "/Execute.bat", command);
            ModuleDefMD backup2 = ModuleDefMD.Load(backup);
            Dictionary<IMethod, IMethod> newDict = new Dictionary<IMethod, IMethod>();
            foreach(KeyValuePair<IMethod,IMethod> pair in patchList) 
            {
                newDict.Add(pair.Key, (IMethod)backup2.ResolveToken(pair.Value.MDToken));
            }
            patchList = newDict;
            patchAssembly = backup2;
            MessageBox.Show("Assembly succcessfully patched, please move all dependancies into the folder with patched executable","Success",MessageBoxButtons.OK,MessageBoxIcon.Information);
        }


    }
}
