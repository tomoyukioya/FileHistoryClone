using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace FileHistory.Tests
{
    public class LoggerFactoryMock : ILoggerFactory
    {
        public List<ILoggerProvider> Providers { get; set; } = new List<ILoggerProvider>();

        public void AddProvider(ILoggerProvider provider) => Providers.Add(provider);

        public ILogger CreateLogger(string categoryName) => new LoggerMock<object>() as ILogger;

        public void Dispose() { }
    }

    public class LoggerMock<T> : ILogger<T>, IDisposable
    {
        public List<string> LogTrace { get; set; } = new List<string>();
        public List<string> LogDebug { get; set; } = new List<string>();
        public List<string> LogInformation { get; set; } = new List<string>();
        public List<string> LogWarning { get; set; } = new List<string>();
        public List<string> LogError { get; set; } = new List<string>();
        public List<string> LogCritical { get; set; } = new List<string>();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => this;

        public void Dispose() { }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            switch (logLevel)
            {
                case LogLevel.Trace:
                    LogTrace.Add(message);
                    break;
                case LogLevel.Debug:
                    LogDebug.Add(message);
                    break;
                case LogLevel.Information:
                    LogInformation.Add(message);
                    break;
                case LogLevel.Warning:
                    LogWarning.Add(message);
                    break;
                case LogLevel.Error:
                    LogError.Add(message);
                    break;
                case LogLevel.Critical:
                    LogCritical.Add(message);
                    break;
            }
        }
    }
}
