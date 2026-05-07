namespace POIneer.Render.Infrastructure.FileSystem;

public interface ITemporaryDirectoryFactory
{
    TemporaryDirectory Create(
        string prefix,
        bool? keepOnDispose = null);
}