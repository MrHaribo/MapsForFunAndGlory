using System;
using System.Collections.Generic;
using System.Text;

namespace MapGen.Core
{
    public static class MapConstants
    {
        public const byte LAND_THRESHOLD = 20;

        // Distance Field Constants
        public const sbyte DEEPER_LAND = 3;
        public const sbyte LANDLOCKED = 2;
        public const sbyte LAND_COAST = 1;
        public const sbyte UNMARKED = 0;
        public const sbyte WATER_COAST = -1;
        public const sbyte DEEP_WATER = -2;
    }
}
