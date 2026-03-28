using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace POIneer.Render.TestHelpers;

public sealed class XunitLogger<T> : ILogger<T>
{
    private readonly ITestOutputHelper _output;

    public XunitLogger(ITestOutputHelper output)
    {
        _output = output;
    }

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
        => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        _output.WriteLine($"[{logLevel}] {message}");

        if (exception is not null)
        {
            _output.WriteLine(exception.ToString());
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}