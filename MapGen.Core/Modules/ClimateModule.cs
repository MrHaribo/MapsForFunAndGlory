using System;
using static MapGen.Core.Helpers.NumberUtils;

namespace MapGen.Core.Modules
{
    public static class ClimateModule
    {
        public static void CalculateTemperatures(MapData data)
        {
            var opt = data.Options;

            // Pre-calculate gradients using options from MapData
            double tempNorthTropic = opt.TemperatureEquator - MapConstants.TROPICS[0] * MapConstants.TROPICAL_GRADIENT;
            double northernGradient = (tempNorthTropic - opt.TemperatureNorthPole) / (90 - MapConstants.TROPICS[0]);

            double tempSouthTropic = opt.TemperatureEquator + MapConstants.TROPICS[1] * MapConstants.TROPICAL_GRADIENT;
            double southernGradient = (tempSouthTropic - opt.TemperatureSouthPole) / (90 + MapConstants.TROPICS[1]);

            double heightExponent = 1.0;

            for (int r = 0; r < data.CellsCountY; r++)
            {
                int rowStartId = r * data.CellsCountX;
                double y = data.Points[rowStartId].Y;

                double relativeY = y / data.Height;
                double rowLatitude = data.Coords.LatN - (relativeY * data.Coords.LatT);

                double tempSeaLevel = CalculateSeaLevelTemp(rowLatitude, opt.TemperatureEquator, tempNorthTropic, tempSouthTropic, northernGradient, southernGradient);

                for (int c = 0; c < data.CellsCountX; c++)
                {
                    int cellId = rowStartId + c;
                    var cell = data.Cells[cellId];

                    double tempAltitudeDrop = GetAltitudeTemperatureDrop(cell.H, heightExponent);
                    double finalTemp = tempSeaLevel - tempAltitudeDrop;

                    cell.Temp = (sbyte)MinMax(finalTemp, -128, 127);
                }
            }
        }

        private static double CalculateSeaLevelTemp(double lat, double equatorTemp, double nTropic, double sTropic, double nGrad, double sGrad)
        {
            bool isTropical = lat <= MapConstants.TROPICS[0] && lat >= MapConstants.TROPICS[1];
            if (isTropical)
                return equatorTemp - Math.Abs(lat) * MapConstants.TROPICAL_GRADIENT;

            return lat > 0
                ? nTropic - (lat - MapConstants.TROPICS[0]) * nGrad
                : sTropic + (lat - MapConstants.TROPICS[1]) * sGrad;
        }

        private static double GetAltitudeTemperatureDrop(byte h, double exponent)
        {
            if (h < MapConstants.LAND_THRESHOLD) return 0;
            double height = Math.Pow(h - 18, exponent);
            return Round((height / 1000.0) * MapConstants.LAPSE_RATE);
        }
    }
}