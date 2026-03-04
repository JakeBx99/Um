using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;
using System.Threading;

namespace BloxManager.Logging
{
    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _logsDir;
        private readonly Func<string> _filePathResolver;
        private readonly string _errorsPath;
        private readonly object _lock = new();

        public FileLoggerProvider(string logsDir)
        {
            _logsDir = logsDir;
            Directory.CreateDirectory(_logsDir);
            _filePathResolver = () => Path.Combine(_logsDir, $"{DateTime.Now:yyyy-MM-dd}.log");
            _errorsPath = Path.Combine(_logsDir, "error_logs.txt");
        }

        public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, _filePathResolver, _lock, _errorsPath);

        public void Dispose() 
        {
        }

        private class FileLogger : ILogger
        {
            private readonly string _category;
            private readonly Func<string> _filePathResolver;
            private readonly object _lock;
            private readonly string _errorsPath;

            public FileLogger(string category, Func<string> filePathResolver, object writeLock, string errorsPath)
            {
                _category = category;
                _filePathResolver = filePathResolver;
                _lock = writeLock;
                _errorsPath = errorsPath;
            }

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                try
                {
                    var message = formatter(state, exception);
                    var sb = new StringBuilder();
                    sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                    sb.Append(" [").Append(logLevel).Append("] ");
                    sb.Append(_category).Append(": ").Append(message);
                    if (exception != null)
                    {
                        sb.AppendLine();
                        sb.Append(exception);
                    }

                    lock (_lock)
                    {
                        File.AppendAllText(_errorsPath, sb.ToString() + Environment.NewLine);
                    }
                }
                catch
                {
                    // swallow logging errors
                }
            }
        }

        private class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
