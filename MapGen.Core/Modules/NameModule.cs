using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Text;

namespace MapGen.Core.Modules
{
    public static class NameModule
    {
        public static string GetBase(int baseId, int minLength, int maxLength, string prefix, string suffix) => null;

        public static string GetBaseShort(int baseId) => null;

        public static int NameBasesCount => GetNameBases().Count;
        public static List<string> GetNameBases() => null;
    }
}
