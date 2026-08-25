namespace POIneer.Render.Application.Ports;

public interface IVectorTileGenerator
{
    Task GenerateAsync(
        string pbfPath,
        string outputPath,
        CancellationToken cancellationToken = default);
}
