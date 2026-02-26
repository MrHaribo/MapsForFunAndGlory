using MapGen.Core;
using MapGen.Core.Helpers;
using MapGen.Core.Modules;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapGen.Tests
{
    public class OptionsTest
    {
        [Fact]
        public void RandomizeOptions_MatchesJs()
        {
            // 1. Setup
            var json = File.ReadAllText("data/regression_options.json");
            var expected = JsonConvert.DeserializeObject<MapOptions>(json);

            var options = new MapOptions { Seed = "42" };
            options.Template = HeightmapTemplate.Continents;
            var rng = new AleaRandom(options.Seed);

            // 2. Execute
            MapOptions.RandomizeOptions(options, rng);

            // 3. Assert
            Assert.Equal(expected.Template, options.Template);
            Assert.Equal(expected.TemperatureEquator, options.TemperatureEquator);
            Assert.Equal(expected.TemperatureNorthPole, options.TemperatureNorthPole);
            Assert.Equal(expected.TemperatureSouthPole, options.TemperatureSouthPole);
            Assert.Equal(expected.Precipitation, options.Precipitation);
            Assert.Equal(expected.CultureSet, options.CultureSet);
            Assert.Equal(expected.CulturesCount, options.CulturesCount);
            Assert.Equal(expected.StatesCount, options.StatesCount);
            Assert.Equal(expected.GrowthRate, options.GrowthRate);
        }
    }
}
