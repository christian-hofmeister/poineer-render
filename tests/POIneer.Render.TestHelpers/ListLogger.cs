namespace POIneer.Render.TestHelpers;
using Microsoft.Extensions.Logging;
public sealed class ListLogger<T> : ILogger<T>, IDisposable
{
    private readonly List<string> _lines = new();
    public IReadOnlyList<string> Lines => _lines;

    public IDisposable BeginScope<TState>(TState state) => this;
    public void Dispose() { }
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _lines.Add(formatter(state, exception));
}
