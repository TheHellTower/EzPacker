using System;
using System.Linq;

namespace EzPacker.Helpers
{
    internal static class Generator
    {
        internal static Random Random = new Random();
        internal static string RandomString(int length) => $"<THT{new string(Enumerable.Repeat("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789", length).Select(s => s[Random.Next(s.Length)]).ToArray())}THT>";
    }
}