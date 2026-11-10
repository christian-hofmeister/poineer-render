namespace POIneer.Render.Application.Ports;

public interface IDiskSpaceReader
{
    long GetAvailableBytes(string directoryPath);
}
