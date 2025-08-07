using POIneer.Render.Services;

Console.WriteLine("Starting POIneer.Render...");

var renderer = new OsmRenderService();
renderer.RenderRegion("Berlin");

Console.WriteLine("Done.");
