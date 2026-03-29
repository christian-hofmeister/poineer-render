namespace POIneer.Render.Infrastructure.FileSystem;

public sealed class TempOptions
{
    public string RootFolderName { get; init; } = "poineer-temp";
    public bool KeepOnDispose { get; init; } = false;
}