using System;
using Microsoft.Extensions.Logging;
using Serilog.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace DumpMiner.Common
{
    /// <summary>
    /// Extension methods and utilities for consistent logging across the application
    /// </summary>
    public static class LoggingExtensions
    {
        /// <summary>
        /// Creates a logger for a specific type using Serilog
        /// </summary>
        public static ILogger<T> CreateLogger<T>()
        {
            var serilogLogger = Serilog.Log.ForContext<T>();
            return new SerilogLoggerFactory(serilogLogger).CreateLogger<T>();
        }

        /// <summary>
        /// Creates a logger for a specific type name using Serilog
        /// </summary>
        public static ILogger<T> CreateLogger<T>(string contextName)
        {
            var serilogLogger = Serilog.Log.ForContext("SourceContext", contextName);
            return new SerilogLoggerFactory(serilogLogger).CreateLogger<T>();
        }

        /// <summary>
        /// Logs operation start with timing
        /// </summary>
        public static IDisposable LogOperation(this ILogger logger, string operationName, params object[] args)
        {
            logger.LogInformation("Starting operation: {OperationName} with args: {Args}", operationName, args);
            return new OperationLogger(logger, operationName, DateTime.UtcNow);
        }

        /// <summary>
        /// Logs performance metrics in a structured way
        /// </summary>
        public static void LogPerformance(this ILogger logger, string operationName, TimeSpan elapsed, string details = null)
        {
            logger.LogInformation("Performance: {OperationName} completed in {ElapsedMs}ms {Details}",
                operationName, elapsed.TotalMilliseconds, details ?? "");
        }

        /// <summary>
        /// Logs memory usage information
        /// </summary>
        public static void LogMemoryUsage(this ILogger logger, string context)
        {
            var gcMemory = GC.GetTotalMemory(false);
            var workingSet = Environment.WorkingSet;
            
            logger.LogDebug("Memory usage in {Context}: GC={GCMemoryMB}MB, WorkingSet={WorkingSetMB}MB",
                context, gcMemory / 1024.0 / 1024.0, workingSet / 1024.0 / 1024.0);
        }

        private class OperationLogger : IDisposable
        {
            private readonly ILogger _logger;
            private readonly string _operationName;
            private readonly DateTime _startTime;

            public OperationLogger(ILogger logger, string operationName, DateTime startTime)
            {
                _logger = logger;
                _operationName = operationName;
                _startTime = startTime;
            }

            public void Dispose()
            {
                var elapsed = DateTime.UtcNow - _startTime;
                _logger.LogInformation("Completed operation: {OperationName} in {ElapsedMs}ms",
                    _operationName, elapsed.TotalMilliseconds);
            }
        }
    }
} 