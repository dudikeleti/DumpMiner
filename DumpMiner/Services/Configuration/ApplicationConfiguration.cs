using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using DumpMiner.Services.AI.Configuration;

namespace DumpMiner.Services.Configuration
{
    /// <summary>
    /// Unified application configuration containing all settings in a hierarchical structure
    /// </summary>
    public sealed class ApplicationConfiguration
    {
        public const string SectionName = "Application";

        /// <summary>
        /// General application settings
        /// </summary>
        public GeneralSettings General { get; set; } = new();

        /// <summary>
        /// Appearance and UI settings
        /// </summary>
        public AppearanceSettings Appearance { get; set; } = new();

        /// <summary>
        /// AI service configuration
        /// </summary>
        public AIConfiguration AI { get; set; } = new();

        /// <summary>
        /// Advanced configuration settings
        /// </summary>
        public AdvancedSettings Advanced { get; set; } = new();
    }

    /// <summary>
    /// General application settings
    /// </summary>
    public sealed class GeneralSettings
    {
        /// <summary>
        /// Path to symbol cache directory
        /// </summary>
        [Required]
        public string SymbolCachePath { get; set; } = @"c:\dev";

        /// <summary>
        /// Default operation timeout in milliseconds
        /// </summary>
        [Range(1000, 300000)]
        public int DefaultTimeoutMs { get; set; } = 60000;

        /// <summary>
        /// Enable automatic updates check
        /// </summary>
        public bool AutoCheckUpdates { get; set; } = true;

        /// <summary>
        /// Maximum number of recent files to track
        /// </summary>
        [Range(5, 50)]
        public int MaxRecentFiles { get; set; } = 10;
    }

    /// <summary>
    /// Appearance and UI configuration
    /// </summary>
    public sealed class AppearanceSettings
    {
        /// <summary>
        /// Application theme (Light, Dark)
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ThemeType Theme { get; set; } = ThemeType.Dark;

        /// <summary>
        /// UI accent color in hex format
        /// </summary>
        [RegularExpression(@"^#[0-9A-Fa-f]{6}$")]
        public string AccentColor { get; set; } = "#1BA1E2";

        /// <summary>
        /// Font size setting
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public FontSizeType FontSize { get; set; } = FontSizeType.Normal;

        /// <summary>
        /// Show detailed tooltips
        /// </summary>
        public bool ShowDetailedTooltips { get; set; } = true;

        /// <summary>
        /// Enable animations
        /// </summary>
        public bool EnableAnimations { get; set; } = true;
    }

    /// <summary>
    /// Advanced configuration for power users
    /// </summary>
    public sealed class AdvancedSettings
    {
        /// <summary>
        /// Enable debug logging
        /// </summary>
        public bool EnableDebugLogging { get; set; } = false;

        /// <summary>
        /// Log level for application logging (managed by Serilog configuration)
        /// </summary>
        public string LogLevel { get; set; } = "Information";

        /// <summary>
        /// Enable performance profiling
        /// </summary>
        public bool EnableProfiling { get; set; } = false;

        /// <summary>
        /// Maximum log file size in MB
        /// </summary>
        [Range(1, 100)]
        public int MaxLogFileSizeMB { get; set; } = 10;

        /// <summary>
        /// Memory usage warning threshold in MB
        /// </summary>
        [Range(100, 8192)]
        public int MemoryWarningThresholdMB { get; set; } = 1024;
    }

    // UI-specific enums (not AI-related)
    public enum ThemeType
    {
        Light,
        Dark
    }

    public enum FontSizeType
    {
        Small,
        Normal,
        Large
    }
} 