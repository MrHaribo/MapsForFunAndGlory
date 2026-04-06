using System;
using System.Collections.Generic;
using System.Text;

namespace MapGen.Core
{
    public enum FeatureType { Ocean, Lake, Island }
    public enum FeatureGroup
    {
        Undefined,
        Ocean, Sea, Gulf,             // Water Groups
        Continent, Island, Isle, LakeIsland, // Land Groups
        Freshwater, Salt, Frozen, Dry, Sinkhole, Lava // Lake Groups
    }

    public enum Culture
    {
        Undefined,
        World, European, Oriental, English, Antique,
        HighFantasy, DarkFantasy, Random
    }
}
