namespace SmakenziBot.Utils
{
    using System.Linq;
    using BroodWar.Api;
    using NBWTA;
    using NBWTA.Result;

    public static class TerrainAnalyzerAdapter
    {
        private const string PathToData = "../../../data/";

        public static AnalyzedMap Get()
        {
            var mapHash = Game.MapHash;
            var fileName = $"{PathToData}{mapHash}.dat";
            var result = AnalyzedMap.TryLoadFromFile(fileName);
            if (result.HasValue) return result.Value;

            var analyzedMap = Analyze();
            analyzedMap.SaveToFile(fileName);

            return analyzedMap;
        }

        private static AnalyzedMap Analyze()
        {
            var mapAnalyzer = new MapAnalyzer();
            var mineralsToConsider = Game.StaticMinerals.Where(m => m.InitialResources > 200).Select(ToXy).ToList();
            var geysersToConsider = Game.StaticGeysers.Where(g => g.InitialResources > 200).Select(ToXy).ToList();

            var analyzedMap = mapAnalyzer
                .Analyze(Game.MapWidth * 4, Game.MapHeight * 4, tile => Game.IsWalkable(tile.x, tile.y),
                    mineralsToConsider, geysersToConsider, tile => Game.IsBuildable(tile.x, tile.y, false));

            return analyzedMap;
        }

        private static (int x, int y) ToXy(Unit u) => (u.TilePosition.X, u.TilePosition.Y);
    }
}