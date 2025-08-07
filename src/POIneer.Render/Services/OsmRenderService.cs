namespace POIneer.Render.Services;

public class OsmRenderService
{
    public void RenderRegion(string region)
    {
        Console.WriteLine($"Rendering region: {region}");

        // TODO: 1. Download OSM-Daten (z. B. PBF)
        // TODO: 2. Extrahiere POIs per osm2pgsql oder eigenes Tool
        // TODO: 3. Erstelle SQLite-DB
        // TODO: 4. Wende Flyway-Migration an
        // TODO: 5. Füge POI-Daten ein (aus CSV, JSON, ...)

        Console.WriteLine("Render completed.");
    }
}
