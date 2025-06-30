using System;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Sinks.Datadog.Logs;

namespace DumpMiner.Common
{
    /// <summary>
    /// Helper class for configuring DataDog logging when needed
    /// </summary>
    public static class DataDogLoggingHelper
    {
        /// <summary>
        /// Adds DataDog sink to the logger configuration if enabled
        /// </summary>
        public static LoggerConfiguration AddDataDogIfEnabled(this LoggerConfiguration loggerConfig, IConfiguration configuration)
        {
            var dataDogConfig = configuration.GetSection("DatadogLogging");
            var isEnabled = dataDogConfig.GetValue<bool>("Enabled");
            
            if (!isEnabled)
            {
                return loggerConfig;
            }

            var apiKey = dataDogConfig.GetValue<string>("ApiKey");
            if (string.IsNullOrEmpty(apiKey))
            {
                Log.Warning("DataDog logging is enabled but no API key is configured");
                return loggerConfig;
            }

            var source = dataDogConfig.GetValue<string>("Source") ?? "csharp";
            var service = dataDogConfig.GetValue<string>("Service") ?? "dumpminer";
            var host = dataDogConfig.GetValue<string>("Host") ?? Environment.MachineName;
            var tags = dataDogConfig.GetSection("Tags").Get<string[]>() ?? Array.Empty<string>();

            var config = new DatadogConfiguration(
                url: "https://http-intake.logs.datadoghq.com",
                port: 443,
                useSSL: true,
                useTCP: false
            );

            return loggerConfig.WriteTo.DatadogLogs(
                apiKey,
                source,
                service,
                host,
                tags,
                config);
        }

        /// <summary>
        /// Creates a DataDog-enabled logger configuration
        /// </summary>
        public static LoggerConfiguration CreateDataDogEnabledLogger(IConfiguration configuration)
        {
            return new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .AddDataDogIfEnabled(configuration);
        }
    }
} 