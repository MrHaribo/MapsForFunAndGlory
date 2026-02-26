using System;
using System.Collections.Generic;
using System.Text;

namespace MapGen.Core
{
    public interface IRandom
    {
        void Init(string seed);
        double Next();
    }
}
