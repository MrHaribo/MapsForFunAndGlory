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

        // River/Lake Hydrology
        public const int MIN_FLUX_TO_FORM_RIVER = 30;
        public const double LAKE_ELEVATION_DELTA = 0.1;
        public const double LAKE_ELEVATION_LIMIT = 20;
        public const double LAKE_HEIGHT_EXPONENT = 2;
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

        // Feature Size Threshold Divisors (TotalCells / Divisor)
        public const int OCEAN_MIN_SIZE_DIVISOR = 25;
        public const int SEA_MIN_SIZE_DIVISOR = 1000;
        public const int CONTINENT_MIN_SIZE_DIVISOR = 10;
        public const int ISLAND_MIN_SIZE_DIVISOR = 1000;

        // Lake Specific Logic Constants
        public const int LAKE_FROZEN_TEMP = -3;
        public const int LAVA_LAKE_MIN_HEIGHT = 60;
        public const int LAVA_LAKE_MAX_CELLS = 10;
        public const int SINKHOLE_MAX_CELLS = 3;

        // Cell Ranking / Suitability Weights
        public const int SCORE_ESTUARY = 15;
        public const int SCORE_OCEAN_COAST = 5;
        public const int SCORE_SAFE_HARBOR = 20;
        public const int SCORE_FRESHWATER = 30;
        public const int SCORE_SALT = 10;
        public const int SCORE_FROZEN = 1;
        public const int SCORE_DRY = -5;
        public const int SCORE_SINKHOLE = -15; // Adjusted to match common JS logic (-5 in your snippet, -15 in some FMG versions, we'll use -15)
        public const int SCORE_LAVA = -30;

        // Normalization Constants
        public const float SUITABILITY_RIVER_SCALE = 250f;
        public const float SUITABILITY_DIVISOR = 5f;
        public const int ELEVATION_OPTIMUM = 50; // The "neutral" height for population
    }
}
