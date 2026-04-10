using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

public sealed class XunitLogger<T> : ILogger<T>
{
    private readonly ITestOutputHelper _output;
    private readonly string _categoryName;

    public XunitLogger(ITestOutputHelper output)
    {
        _output = output;
        _categoryName = typeof(T).Name;
    }

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
        => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel)
        => true; // später evtl. filterbar

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var message = formatter(state, exception);

        var logLine = $"[{timestamp}] [{logLevel}] [{_categoryName}] {message}";
        _output.WriteLine(logLine);

        if (exception is not null)
        {
            _output.WriteLine(exception.ToString());
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}