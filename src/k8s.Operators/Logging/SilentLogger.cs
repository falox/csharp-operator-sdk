using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace k8s.Operators.Logging
{
    /// <summary>
    /// Empty ILogger that doesn't log, used as fallback when no logger is passed to the library.
    /// </summary>
    [ExcludeFromCodeCoverage]
    internal class SilentLogger : Disposable, ILogger
    {
        public static ILogger Instance = new SilentLogger();
        
        public IDisposable BeginScope<TState>(TState state) => this;
        
        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) 
        {
        }
    }
}