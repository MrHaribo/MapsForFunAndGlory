using System;
using System.Collections.Generic;
using System.Text;

namespace MapGen.Core
{
    public static class HeightmapTemplates
    {
        public static string GetRecipe(HeightmapTemplate template)
        {
            return template switch
            {
                HeightmapTemplate.Volcano =>
                    @"Hill 1 90-100 44-56 40-60
                    Multiply 0.8 50-100 0 0
                    Range 1.5 30-55 45-55 40-60
                    Smooth 3 0 0 0
                    Hill 1.5 35-45 25-30 20-75
                    Hill 1 35-55 75-80 25-75
                    Hill 0.5 20-25 10-15 20-25
                    Mask 3 0 0 0",

                HeightmapTemplate.HighIsland =>
                    @"Hill 1 90-100 65-75 47-53
                    Add 7 all 0 0
                    Hill 5-6 20-30 25-55 45-55
                    Range 1 40-50 45-55 45-55
                    Multiply 0.8 land 0 0
                    Mask 3 0 0 0
                    Smooth 2 0 0 0
                    Trough 2-3 20-30 20-30 20-30
                    Trough 2-3 20-30 60-80 70-80
                    Hill 1 10-15 60-60 50-50
                    Hill 1.5 13-16 15-20 20-75
                    Range 1.5 30-40 15-85 30-40
                    Range 1.5 30-40 15-85 60-70
                    Pit 3-5 10-30 15-85 20-80",

                HeightmapTemplate.Archipelago =>
                    @"Add 11 all 0 0
                    Range 2-3 40-60 20-80 20-80
                    Hill 5 15-20 10-90 30-70
                    Hill 2 10-15 10-30 20-80
                    Hill 2 10-15 60-90 20-80
                    Smooth 3 0 0 0
                    Trough 10 20-30 5-95 5-95
                    Strait 2 vertical 0 0
                    Strait 2 horizontal 0 0",

                HeightmapTemplate.Test => "Hill 1 90-100 44-56 40-60",

                _ => throw new ArgumentException($"Template {template} recipe not implemented.")
            };
        }
    }
}
