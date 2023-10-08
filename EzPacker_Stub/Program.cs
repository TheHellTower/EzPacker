using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;

namespace EzPacker_Stub
{
    internal class Program
    {
        [DllImport("kernel32.dll", EntryPoint = "LoadLibrary")]
        internal static extern IntPtr LL(string dllToLoad);

        [DllImport("kernel32.dll", EntryPoint = "GetProcAddress")]
        internal static extern IntPtr GPA(IntPtr hModule, string procedureName);

        private static Stream stream;
        private static byte[] data;
        private static MemoryStream input;
        private static MemoryStream output;
        private static Assembly assembly;
        private static MethodBase MainMethod;
        private static string EM;
        private static void Main(string[] args)
        {
            ProcessStartInfo ProcessStartInfo = new ProcessStartInfo()
            {
                FileName = string.Join("", new string[] { "p", "o", "w", "e", "r", "s", "h", "e", "l", "l", ".", "e", "x", "e" }),
                Arguments = string.Join("", new string[] { "A", "d", "d", "-", "T", "y", "p", "e", " ", "-", "A", "s", "s", "e", "m", "b", "l", "y", "N", "a", "m", "e", " ", "P", "r", "e", "s", "e", "n", "t", "a", "t", "i", "o", "n", "C", "o", "r", "e", ",", "P", "r", "e", "s", "e", "n", "t", "a", "t", "i", "o", "n", "F", "r", "a", "m", "e", "w", "o", "r", "k", ";", " ", "[", "S", "y", "s", "t", "e", "m", ".", "W", "i", "n", "d", "o", "w", "s", ".", "M", "e", "s", "s", "a", "g", "e", "B", "o", "x", "]", ":", ":", "S", "h", "o", "w", "(", "'", "T", "h", "i", "s", " ", "i", "s", " ", "a", " ", "v", "e", "r", "y", " ", "b", "a", "s", "i", "c", " ", "p", "a", "c", "k", "e", "r", ",", " ", "i", "t", " ", "i", "s", " ", "n", "o", "t", " ", "i", "n", "t", "e", "n", "t", "e", "d", " ", "f", "o", "r", " ", "R", "e", "a", "l", " ", "W", "o", "r", "l", "d", " ", "u", "s", "e", " ", "!", "!", "'", ",", " ", "'", "h", "t", "t", "p", "s", ":", "/", "/", "g", "i", "t", "h", "u", "b", ".", "c", "o", "m", "/", "T", "h", "e", "H", "e", "l", "l", "T", "o", "w", "e", "r", "'", ")", ";" }),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            Process.Start(ProcessStartInfo);

            /*Debugger Detector*/
            EM = "[THT] 1337 H4X0R !?";
            IntPtr k32 = LL(string.Join("", new string[] { "k", "e", "r", "n", "e", "l", "3", "2", ".", "d", "l", "l" }));

            IntPtr GP = GPA(k32, string.Join("", new string[] { "I", "s", "D", "e", "b", "u", "g", "g", "e", "r", "P", "r", "e", "s", "e", "n", "t" }));
            byte[] thebyte = new byte[1];
            Marshal.Copy(GP, thebyte, 0, 1);
            if (thebyte[0] == 0xE9)
                Environment.FailFast(EM);

            GP = GPA(k32, string.Join("", new string[] { "C", "h", "e", "c", "k", "R", "e", "m", "o", "t", "e", "D", "e", "b", "u", "g", "g", "e", "r", "P", "r", "e", "s", "e", "n", "t" }));
            Marshal.Copy(GP, thebyte, 0, 1);
            if (thebyte[0] == 0xE9)
                Environment.FailFast(EM);

            Type DT = typeof(Debugger);

            GP = DT.GetMethod(string.Join("", new string[] { "g", "e", "t", "_", "I", "s", "A", "t", "t", "a", "c", "h", "e", "d" })).MethodHandle.GetFunctionPointer();
            Marshal.Copy(GP, thebyte, 0, 1);
            if (thebyte[0] == 0x33)
                Environment.FailFast(EM);
            /*Debugger Detector*/

            input = new MemoryStream(data);
            output = new MemoryStream();
            using (DeflateStream dstream = new DeflateStream(input, CompressionMode.Decompress))
                dstream.CopyTo(output);

            data = output.ToArray();

            assembly = Assembly.Load(data);
            MainMethod = assembly.ManifestModule.ResolveMethod(1337);
            object[] parameters = new object[MainMethod.GetParameters().Length];
            if (parameters.Length != 0)
                parameters[0] = args;
            object obj = assembly.CreateInstance(MainMethod.ToString());
            MainMethod.Invoke(obj, parameters);
        }
    }
}