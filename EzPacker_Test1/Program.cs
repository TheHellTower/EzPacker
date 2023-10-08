using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EzPacker_Test1
{
    internal class Program
    {
        public static void LogUser() => Console.WriteLine(Encoding.UTF8.GetString(Convert.FromBase64String("SGVsbG8gezB9")), $"{Environment.MachineName}\\{Environment.UserName}");

        public static void Log(string x) => Console.WriteLine(x);

        public static void Main(string[] args)
        {
            LogUser();
            Log($"x64 System: {Environment.Is64BitOperatingSystem}\nx64 Process: {Environment.Is64BitProcess}");
            Log($"Test: {HelloInt * 2 / 2}");
            Console.ReadLine();
        }
        private static int HelloInt = 1337;
    }
}
