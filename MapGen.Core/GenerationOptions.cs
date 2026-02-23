namespace MapGen.Core
{
    public class GenerationOptions
    {
        public string Seed { get; set; } = "azgaar";
        public int Width { get; set; } = 1920;
        public int Height { get; set; } = 1080;
        public int PointsCount { get; set; } = 2000;

        public static GenerationOptions TestOptions => new GenerationOptions
        {
            Seed = "42",
            Width = 1920,
            Height = 1080,
            PointsCount = 2000
        };
    }
}