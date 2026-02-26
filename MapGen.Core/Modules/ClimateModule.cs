using MapGen.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using static MapGen.Core.Helpers.NumberUtils;

namespace MapGen.Core.Modules
{
    public static class ClimateModule
    {
        #region Temperature

        public static void CalculateTemperatures(MapData data)
        {
            var opt = data.Options;

            // Pre-calculate gradients using options from MapData
            double tempNorthTropic = opt.TemperatureEquator - MapConstants.TROPICS[0] * MapConstants.TROPICAL_GRADIENT;
            double northernGradient = (tempNorthTropic - opt.TemperatureNorthPole) / (90 - MapConstants.TROPICS[0]);

            double tempSouthTropic = opt.TemperatureEquator + MapConstants.TROPICS[1] * MapConstants.TROPICAL_GRADIENT;
            double southernGradient = (tempSouthTropic - opt.TemperatureSouthPole) / (90 + MapConstants.TROPICS[1]);

            double heightExponent = opt.HeightExponent;

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

#endregion

        #region Precipitation

        public static void GeneratePrecipitation(MapData data)
        {
            var cells = data.Cells;
            var opt = data.Options;

            // 1. Calculate Modifiers
            // JS: (pointsInput.dataset.cells / 10000) ** 0.25
            double cellsNumberModifier = Math.Pow(data.PointsCount / 10000.0, 0.25);
            double precInputModifier = opt.Precipitation / 100.0;
            double modifier = cellsNumberModifier * precInputModifier;
            double[] latitudeModifier = MapConstants.LATITUDE_MODIFIER;

            var westerly = new List<(int index, double latMod)>();
            var easterly = new List<(int index, double latMod)>();
            int southerly = 0;
            int northerly = 0;

            // 2. Define wind directions based on latitude bands
            // We iterate by row (cellsCountX) to find the start of each horizontal wind path
            for (int i = 0; i < data.CellsCountY; i++)
            {
                int c = i * data.CellsCountX;
                double lat = data.Coords.LatN - (i / (double)data.CellsCountY) * data.Coords.LatT;

                int latBand = (int)Math.Floor((Math.Abs(lat) - 1) / 5.0);
                latBand = Math.Clamp(latBand, 0, latitudeModifier.Length - 1);
                double latMod = latitudeModifier[latBand];

                int windTier = (int)Math.Floor(Math.Abs(lat - 89) / 30.0);
                windTier = Math.Clamp(windTier, 0, 5); // JS: 30d tiers from 0 to 5

                var (isWest, isEast, isNorth, isSouth) = GetWindDirections(windTier, MapConstants.WIND_DIRECTIONS);

                if (isWest) westerly.Add((c, latMod));
                if (isEast) easterly.Add((c + data.CellsCountX - 1, latMod));
                if (isNorth) northerly++;
                if (isSouth) southerly++;
            }

            // 3. Horizontal Wind Passes
            if (westerly.Any()) PassWind(data, westerly, 120 * modifier, 1, data.CellsCountX, modifier);
            if (easterly.Any()) PassWind(data, easterly, 120 * modifier, -1, data.CellsCountX, modifier);

            // 4. Vertical Wind Passes
            int vertT = southerly + northerly;
            if (northerly > 0)
            {
                int bandN = (int)Math.Floor((Math.Abs(data.Coords.LatN) - 1) / 5.0);
                bandN = Math.Clamp(bandN, 0, latitudeModifier.Length - 1);
                double latModN = data.Coords.LatT > 60 ? latitudeModifier.Average() : latitudeModifier[bandN];
                double maxPrecN = (northerly / (double)vertT) * 60 * modifier * latModN;

                var northSource = Enumerable.Range(0, data.CellsCountX).Select(idx => (idx, 1.0)).ToList();
                PassWind(data, northSource, maxPrecN, data.CellsCountX, data.CellsCountY, modifier);
            }

            if (southerly > 0)
            {
                int bandS = (int)Math.Floor((Math.Abs(data.Coords.LatS) - 1) / 5.0);
                bandS = Math.Clamp(bandS, 0, latitudeModifier.Length - 1);
                double latModS = data.Coords.LatT > 60 ? latitudeModifier.Average() : latitudeModifier[bandS];
                double maxPrecS = (southerly / (double)vertT) * 60 * modifier * latModS;

                int startIdx = data.Cells.Length - data.CellsCountX;
                var southSource = Enumerable.Range(startIdx, data.CellsCountX).Select(idx => (idx, 1.0)).ToList();
                PassWind(data, southSource, maxPrecS, -data.CellsCountX, data.CellsCountY, modifier);
            }
        }

        private static (bool isWest, bool isEast, bool isNorth, bool isSouth) GetWindDirections(int tier, int[] winds)
        {
            int angle = winds[tier];
            bool isWest = angle > 40 && angle < 140;
            bool isEast = angle > 220 && angle < 320;
            bool isNorth = angle > 100 && angle < 260;
            bool isSouth = angle > 280 || angle < 80;
            return (isWest, isEast, isNorth, isSouth);
        }

        private static void PassWind(MapData data, List<(int index, double latMod)> source, double maxPrecBase, int next, int steps, double modifier)
        {
            foreach (var start in source)
            {
                double maxPrec = Math.Min(maxPrecBase * start.latMod, 255);
                int current = start.index;
                double humidity = maxPrec - data.Cells[current].H;

                if (humidity <= 0) continue;

                for (int s = 0; s < steps; s++)
                {
                    if (current < 0 || current >= data.Cells.Length) break;
                    var cell = data.Cells[current];

                    if (cell.Temp < -5) { current += next; continue; }

                    if (cell.H < MapConstants.LAND_THRESHOLD)
                    {
                        // Water cell logic
                        int nextIdx = current + next;
                        if (nextIdx >= 0 && nextIdx < data.Cells.Length && data.Cells[nextIdx].H >= MapConstants.LAND_THRESHOLD)
                        {
                            // Coastal precipitation: Add to next cell (the land)
                            data.Cells[nextIdx].Prec = (byte)MinMax(data.Cells[nextIdx].Prec + Math.Max(humidity / data.Rng.Next(10, 20), 1), 0, 255);
                        }
                        else
                        {
                            humidity = Math.Min(humidity + 5 * modifier, maxPrec);
                            cell.Prec = (byte)MinMax(cell.Prec + 5 * modifier, 0, 255);
                        }
                    }
                    else
                    {
                        // Land cell logic
                        int nextIdx = current + next;
                        bool isPassable = nextIdx >= 0 && nextIdx < data.Cells.Length && data.Cells[nextIdx].H <= MapConstants.MAX_PASSABLE_ELEVATION;

                        double precipitation = isPassable
                            ? GetPrecipitation(data, humidity, current, nextIdx, modifier)
                            : humidity;

                        cell.Prec = (byte)MinMax(cell.Prec + precipitation, 0, 255);
                        double evaporation = precipitation > 1.5 ? 1 : 0;
                        humidity = isPassable ? MinMax(humidity - precipitation + evaporation, 0, maxPrec) : 0;
                    }

                    current += next;
                }
            }
        }

        private static double GetPrecipitation(MapData data, double humidity, int i, int n, double modifier)
        {
            double normalLoss = Math.Max(humidity / (10 * modifier), 1);
            double diff = Math.Max(data.Cells[n].H - data.Cells[i].H, 0);
            double mod = Math.Pow(data.Cells[n].H / 70.0, 2);

            double result = normalLoss + diff * mod;
            // Instead of a strict MinMax(result, 1, humidity), 
            // we mimic the JS behavior where humidity is the absolute ceiling.
            return Math.Min(result, humidity);
        }

        #endregion
    }
}