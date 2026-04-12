using MapGen.Core.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapGen.Tests
{
    public class StateTests
    {
        [Fact]
        public void TestStates()
        {
            var mapData = TestMapData.TestData; // Ensure this uses expected.Seed
            GridGenerator.Generate(mapData);
            VoronoiGenerator.CalculateVoronoi(mapData);
            HeightmapGenerator.Generate(mapData);
            FeatureModule.MarkupGrid(mapData);
            GlobeModule.DefineMapSize(mapData);
            GlobeModule.CalculateMapCoordinates(mapData);
            ClimateModule.CalculateTemperatures(mapData);
            ClimateModule.GeneratePrecipitation(mapData);

            var pack = PackModule.ReGraph(mapData);
            FeatureModule.MarkupPack(pack);
            RiverModule.Generate(pack);
            BiomModule.Define(pack);
            FeatureModule.DefineGroups(pack);
            FeatureModule.RankCells(pack);
            CultureModule.Generate(pack);
            CultureModule.ExpandCultures(pack);
            BurgModule.Generate(pack);

            StateModule.Generate(pack);
        }
    }
}
