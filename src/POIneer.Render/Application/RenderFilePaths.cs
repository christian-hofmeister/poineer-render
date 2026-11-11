namespace POIneer.Render.Application;

internal static class RenderFilePaths
{
    public const string SourcePbf = "osm.pbf";
    public const string PoiDataset = "poi.sqlite";
    public const string StagingPoiDataset = PoiDataset + ".tmp";
    public const string VectorTiles = "map.pmtiles";
    public const string StagingVectorTiles = "map.tmp.pmtiles";
    public const string PlanetilerScratchDirectory = "planetiler+scratch";

    public static IReadOnlyList<string> SqliteSidecarSuffixes { get; } = ["-wal", "-shm", "-journal"];

    public static string GetCutPbfPath(string sourcePath)
        => Path.ChangeExtension(sourcePath, ".cut.osm.pbf");

    public static string GetPlanetilerTempPath(string regionWorkDir)
        => Path.Combine(regionWorkDir, PlanetilerScratchDirectory, "tmp");
}
