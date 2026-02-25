using System;
using System.Collections.Generic;
using System.Text;

namespace MapGen.Core
{
    public static class MapConstants
    {
        // Elevation Thresholds
        public const byte LAND_THRESHOLD = 20;      // Heights >= 20 are land
        public const byte LAKE_HEIGHT = 19;         // Default height for lake water
        public const int LAKE_BREACH_LIMIT = 22;    // Max height water can breach to join the sea
        public const int DEFAULT_LAKE_ELEV_LIMIT = 80; // JS default "no-op" limit

        // Distance Field Constants (T)
        public const sbyte DEEPER_LAND = 3;
        public const sbyte LANDLOCKED = 2;
        public const sbyte LAND_COAST = 1;
        public const sbyte UNMARKED = 0;
        public const sbyte WATER_COAST = -1;
        public const sbyte DEEP_WATER = -2;
    }
}
