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

    public enum CultureType
    {
        Undefined,
        Generic,
        Nomadic,
        Highland,
        Lake,
        Naval,
        River,
        Hunting
    }

    public enum DiplomacyRelation
    {
        None,       // Replaces "x"
        Ally,
        Friendly,
        Neutral,
        Suspicion,
        Rival,
        Unknown,
        Vassal,
        Suzerain,
        Enemy
    }

    public enum StateForm
    {
        Undefined,
        Monarchy,
        Republic,
        Union,
        Theocracy,
        Anarchy
    }

    public enum RouteType
    {
        Roads,
        Trails,
        Searoutes
    }

    public enum ReligionGroup 
    { 
        Folk, 
        Organized, 
        Cult, 
        Heresy 
    }

    public enum BurgType
    {
        Undefined,
        Town, // IsDefault
        Capital,
        City,
        Fort,
        Monastery,
        Caravanserai,
        TradingPost,
        Village,
        Hamlet
    }

    [Flags]
    public enum BurgFeature
    {
        None = 0,
        Capital = 1 << 0,
        Citadel = 1 << 1,
        Walls = 1 << 2,
        Plaza = 1 << 3,
        Port = 1 << 4,
        Temple = 1 << 5,
        Shanty = 1 << 6
    }
}
