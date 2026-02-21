namespace MapGen.Core
{
    public class GenerationOptions
    {
        public string Seed { get; set; } = "azgaar";
        public int Width { get; set; } = 1920;
        public int Height { get; set; } = 1080;
        public int PointsCount { get; set; } = 10000;
        public double Jitter { get; set; } = 0.8;

        // TODO: Add this for regression testing
        public double? FixedSpacing { get; set; }
    }
}