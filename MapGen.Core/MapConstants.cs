namespace MapGen.Core
{
    public static class MapConstants
    {
        // Elevation Thresholds
        public const byte LAND_THRESHOLD = 20;      // Heights >= 20 are land
        public const byte LAKE_HEIGHT = 19;         // Default height for lake water
        public const int LAKE_BREACH_LIMIT = 22;    // Max height water can breach to join the sea
        public const int DEFAULT_LAKE_ELEV_LIMIT = 80; // JS default "no-op" limit

        // Climate Constants
        public const double TROPICAL_GRADIENT = 0.15;
        public const double LAPSE_RATE = 6.5; // °C per 1km
        public const double TEMPERATURE_EQUATOR = 27;
        public const double TEMPERATURE_NORTH_POLE = -15;
        public const double TEMPERATURE_SOUTH_POLE = -25;
        public static readonly double[] TROPICS = { 16, -20 };

        // Wind Directions (in degrees) based on Latitude bands
        // 0-30° (Trade Winds), 30-60° (Westerlies), 60-90° (Polar Easterlies)
        public static readonly int[] WIND_DIRECTIONS = { 225, 45, 225, 315, 135, 315 };
        public static readonly double[] LATITUDE_MODIFIER = { 4, 2, 2, 2, 1, 1, 2, 2, 2, 2, 3, 3, 2, 2, 1, 1, 1, 0.5 };
        public const int MAX_PASSABLE_ELEVATION = 85;

        // Distance Field Constants (T)
        public const sbyte DEEPER_LAND = 3;
        public const sbyte LANDLOCKED = 2;
        public const sbyte LAND_COAST = 1;
        public const sbyte UNMARKED = 0;
        public const sbyte WATER_COAST = -1;
        public const sbyte DEEP_WATER = -2;

        // River Hydrology
        public const int MIN_FLUX_TO_FORM_RIVER = 30;
        public const double LAKE_ELEVATION_DELTA = 0.1;
        public const int MAX_DOWNCUT = 5;

        // River Geometry
        public const double RIVER_FLUX_FACTOR = 500.0;
        public const double RIVER_MAX_FLUX_WIDTH = 1.0;
        public const double RIVER_LENGTH_FACTOR = 200.0;
        public static readonly double[] RIVER_LENGTH_PROGRESSION = { 1, 1, 2, 3, 5, 8, 13, 21, 34 };

        // Depression Resolution
        public const int DEPRESSION_MAX_ITERATIONS = 500;
        public const double DEPRESSION_LAKE_ITER_RATIO = 0.85;
        public const double DEPRESSION_ELEVATE_ITER_RATIO = 0.75;
    }
}
