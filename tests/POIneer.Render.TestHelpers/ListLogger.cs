using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace POIneer.Render.TestHelpers;

public sealed class ListLogger<T> : ILogger<T>, IDisposable
{
    private readonly List<string> _lines = new();
    public IReadOnlyList<string> Lines => _lines;

    public void Dispose() { }
    public bool IsEnabled(LogLevel logLevel) => true;

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly IDisposable Instance = new NoopDisposable();
        private NoopDisposable() { }
        public void Dispose() { }
    }

    // Explizite Implementierungen – ohne eigene where-Constraints
    IDisposable? ILogger.BeginScope<TState>(TState state)
        => NoopDisposable.Instance;

    void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                             Exception? exception, Func<TState, Exception?, string> formatter)
        => _lines.Add(formatter(state, exception));
}
