using System;
using System.Collections.Generic;
using System.Text;

namespace MapGen.Core
{
    public interface IRandom
    {
        public double Next();
        double Next(double min, double max);
        int Next(int min, int max);
    }
}
