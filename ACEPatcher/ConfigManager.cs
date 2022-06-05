using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using dnlib.DotNet;
using System.Windows.Forms;
using dnlib.PE;

namespace ACEPatcher
{
    class ConfigManager
    {

        public static void ExportConfig(ModuleDefMD mainAssembly,string path,bool Secure,string password = null)
        {
            foreach(KeyValuePair<IMethod,IMethod> patch in Patcher.patchList) 
            {
                if(((MethodDef)patch.Key).Module.Assembly.FullName == mainAssembly.Assembly.FullName) 
                {
                    MessageBox.Show("These patches are only applicable to the assembly in question and thus cannot be exported","Error",MessageBoxButtons.OK,MessageBoxIcon.Error);
                    return;
                }
            }
            byte[] content;
            using(MemoryStream stream = new MemoryStream()) 
            {
                using(BinaryWriter writer = new BinaryWriter(stream)) 
                {
                    writer.Write((ushort)Patcher.patchList.Count);
                    foreach(KeyValuePair<IMethod, IMethod> patch in Patcher.patchList) 
                    {
                        writer.Write(patch.Key.DeclaringType.DefinitionAssembly.FullName);
                        writer.Write('\0');
                        writer.Write(patch.Key.FullName);
                        writer.Write('\0');
                        writer.Write(patch.Value.DeclaringType.DefinitionAssembly.FullName);
                        writer.Write('\0');
                        writer.Write(patch.Value.FullName);
                        writer.Write('\0');
                    }
                    writer.Write(DotNetUtils.ReadAssembly(Patcher.patchAssembly));
                }
                content = Compress(stream.ToArray());
                if (Secure)
                {
                    content = Encrypt(content, password);
                }
            }
            File.WriteAllBytes(path,content.Prepend<byte>(Secure ? (byte)1 : (byte)0).ToArray());
        }


        public static void ImportConfig(ModuleDefMD[] dependancies,string path) 
        {
            byte[] config = File.ReadAllBytes(path);
            byte magic = config[0];
            config = config.Skip(1).ToArray();
            if(magic == 1) 
            {
                string password = null;
                if (MainFRM.InputBox("Password", "Type in password:", ref password) == DialogResult.OK)
                {
                    try 
                    {
                        config = Decrypt(config, password);
                    }
                    catch 
                    {
                        MessageBox.Show("Invalid password", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                else
                    return;
            }
            try 
            {
                config = Decompress(config);
            }
            catch 
            {
                MessageBox.Show("Invalid patch", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            using (MemoryStream stream = new MemoryStream(config))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    ushort patchNo = reader.ReadUInt16();
                    KeyValuePair<string, string>[] pairs = new KeyValuePair<string, string>[patchNo*2];
                    for(int i = 0; i < patchNo * 2; i+=2) 
                    {
                        string _1 = reader.ReadString();
                        reader.ReadByte();
                        string _2 = reader.ReadString();
                        reader.ReadByte();
                        pairs[i] = new KeyValuePair<string, string>(_1, _2);
                        _1 = reader.ReadString();
                        reader.ReadByte();
                        _2 = reader.ReadString();
                        reader.ReadByte();
                        pairs[i+1] = new KeyValuePair<string, string>(_1, _2);
                    }
                    for(int i = 0;i<pairs.Length;i+=2)
                    {
                        if(dependancies.Where(x => x.Assembly.FullName == pairs[i].Key).Count() == 0) 
                        {
                            MessageBox.Show("Assembly " + pairs[i].Key + " is missing", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                    }
                    byte[] PatchAssembly = new byte[stream.Length - stream.Position];
                    reader.Read(PatchAssembly, 0, (int)(stream.Length - stream.Position));
                    Patcher.patchAssembly = ModuleDefMD.Load(PatchAssembly);
                    for(int i = 0; i < pairs.Length; i += 2) 
                    {
                        Patcher.patchList.Add(DotNetUtils.findMethodByName(dependancies,pairs[i].Key,pairs[i].Value), DotNetUtils.findMethodByName(dependancies.Prepend(Patcher.patchAssembly).ToArray(), pairs[i+1].Key, pairs[i+1].Value));
                    }
                }
            }
            MessageBox.Show("Config succesfully imported", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
        
        }

        
        static byte[] Compress(byte[] data)
        {
            byte[] compressArray = null;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (GZipStream deflateStream = new GZipStream(memoryStream, CompressionMode.Compress))
                {
                    deflateStream.Write(data, 0, data.Length);
                }
                compressArray = memoryStream.ToArray();
            }
            return compressArray;
        }

        static byte[] Decompress(byte[] data)
        {
            using (GZipStream stream = new GZipStream(new MemoryStream(data), CompressionMode.Decompress))
            {
                byte[] buffer = new byte[4096];
                using (MemoryStream memory = new MemoryStream())
                {
                    int count = 0;
                    do
                    {
                        count = stream.Read(buffer, 0, 4096);
                        if (count > 0)
                        {
                            memory.Write(buffer, 0, count);
                        }
                    }
                    while (count > 0);
                    return memory.ToArray();
                }
            }
        }

        static byte[] Encrypt(byte[] data,string password) 
        {
            byte[] result;
            SHA256Managed sha256 = new SHA256Managed();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            AesManaged aes = new AesManaged();
            ICryptoTransform encryptor = aes.CreateEncryptor(hash.Take(16).ToArray(), hash.Skip(16).Take(16).ToArray());
            using (MemoryStream ms = new MemoryStream())
            {
                using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    using (BinaryWriter sw = new BinaryWriter(cs))
                        sw.Write(data);
                    result = ms.ToArray();
                }
            }
            return result;
        }

        static byte[] Decrypt(byte[] data, string password)
        {
            byte[] result;
            SHA256Managed sha256 = new SHA256Managed();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            AesManaged aes = new AesManaged();
            ICryptoTransform decryptor = aes.CreateDecryptor(hash.Take(16).ToArray(), hash.Skip(16).Take(16).ToArray());
            using (MemoryStream ms = new MemoryStream())
            {
                using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
                {
                    using (BinaryWriter sw = new BinaryWriter(cs))
                        sw.Write(data);
                    result = ms.ToArray();
                }
            }
            return result;
        }

    }
}
