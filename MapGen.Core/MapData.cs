namespace MapGen.Core
{
    public class MapData
    {
        // Grid Constants
        public int Width { get; set; }
        public int Height { get; set; }
        public int PointsCount { get; set; }
        public double Spacing { get; set; }

        public float[] BoundaryX { get; set; }
        public float[] BoundaryY { get; set; }

        // Raw Coordinates (The "Grid")
        public float[] X;
        public float[] Y;

        // The "Cells" (The simulation data / "Pack")
        public float[] Heights;     // elevation
        public float[] Precipitation;
        public int[] Features;       // 0: ocean, 1: land, 2+: lakes/islands

        public MapData(int count, int width, int height)
        {
            PointsCount = count;
            Width = width;
            Height = height;

            X = new float[count];
            Y = new float[count];
            Heights = new float[count];
            Precipitation = new float[count];
            Features = new int[count];
        }
    }
}