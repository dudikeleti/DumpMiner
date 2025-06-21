using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace DumpMiner.Models
{
    /// <summary>
    /// Represents the type of memory dump to create.
    /// </summary>
    public enum DumpType
    {
        /// <summary>
        /// Minimal dump with stack traces and module information (~1-10MB).
        /// Best for basic crash analysis and troubleshooting.
        /// </summary>
        [Description("Mini dump (small, basic crash info)")]
        Mini = 0,
        
        /// <summary>
        /// Includes managed heap and is usually much larger (~50-500MB).
        /// Best for memory leak analysis and detailed object inspection.
        /// </summary>
        [Description("Heap dump (medium, includes managed heap)")]
        Heap = 1,
        
        /// <summary>
        /// Minimal dump optimized for triage scenarios (~1-5MB).
        /// Best for quick initial analysis and automated processing.
        /// </summary>
        [Description("Triage dump (smallest, optimized for automation)")]
        Triage = 2,
        
        /// <summary>
        /// Full memory dump including all process memory (can be very large).
        /// Best for comprehensive analysis but requires significant disk space.
        /// </summary>
        [Description("Full dump (large, complete memory contents)")]
        Full = 3
    }

    /// <summary>
    /// Represents the result of a dump creation operation.
    /// </summary>
    public sealed class DumpCreationResult
    {
        /// <summary>
        /// Gets whether the dump creation was successful.
        /// </summary>
        public bool Success { get; init; }
        
        /// <summary>
        /// Gets the path to the created dump file, if successful.
        /// </summary>
        public string FilePath { get; init; }
        
        /// <summary>
        /// Gets the error message if the operation failed.
        /// </summary>
        public string ErrorMessage { get; init; }
        
        /// <summary>
        /// Gets the size of the created dump file in bytes.
        /// </summary>
        public long FileSizeBytes { get; init; }
        
        /// <summary>
        /// Gets the time taken to create the dump.
        /// </summary>
        public TimeSpan Duration { get; init; }
        
        /// <summary>
        /// Gets the type of dump that was created.
        /// </summary>
        public DumpType DumpType { get; init; }
        
        /// <summary>
        /// Gets the ID of the process from which the dump was created.
        /// </summary>
        public int ProcessId { get; init; }
        
        /// <summary>
        /// Gets the name of the process from which the dump was created.
        /// </summary>
        public string ProcessName { get; init; }
        
        /// <summary>
        /// Gets the timestamp when the dump was created.
        /// </summary>
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
        
        /// <summary>
        /// Gets additional metadata about the dump creation process.
        /// </summary>
        public IDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
        
        /// <summary>
        /// Gets the formatted file size as a human-readable string.
        /// </summary>
        public string FormattedFileSize => FormatBytes(FileSizeBytes);
        
        /// <summary>
        /// Creates a successful result.
        /// </summary>
        public static DumpCreationResult CreateSuccess(
            string filePath, 
            long fileSizeBytes, 
            TimeSpan duration, 
            DumpType dumpType, 
            int processId, 
            string processName,
            IDictionary<string, object> metadata = null)
        {
            return new DumpCreationResult
            {
                Success = true,
                FilePath = filePath,
                FileSizeBytes = fileSizeBytes,
                Duration = duration,
                DumpType = dumpType,
                ProcessId = processId,
                ProcessName = processName,
                Metadata = metadata ?? new Dictionary<string, object>()
            };
        }
        
        /// <summary>
        /// Creates a failure result.
        /// </summary>
        public static DumpCreationResult CreateFailure(
            string errorMessage, 
            int processId, 
            string processName, 
            DumpType dumpType,
            TimeSpan duration = default,
            IDictionary<string, object> metadata = null)
        {
            return new DumpCreationResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                ProcessId = processId,
                ProcessName = processName,
                DumpType = dumpType,
                Duration = duration,
                Metadata = metadata ?? new Dictionary<string, object>()
            };
        }
        
        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            if (bytes == 0) return "0 B";
            
            var place = Convert.ToInt32(Math.Floor(Math.Log(Math.Abs(bytes), 1024)));
            var num = Math.Round(bytes / Math.Pow(1024, place), 2);
            return $"{num:F2} {suffixes[place]}";
        }
    }

    /// <summary>
    /// Represents progress information during dump creation.
    /// </summary>
    public sealed class DumpProgress
    {
        /// <summary>
        /// Gets the completion percentage (0-100).
        /// </summary>
        public int PercentComplete { get; init; }
        
        /// <summary>
        /// Gets the current operation being performed.
        /// </summary>
        public string CurrentOperation { get; init; }
        
        /// <summary>
        /// Gets the number of bytes written so far.
        /// </summary>
        public long BytesWritten { get; init; }
        
        /// <summary>
        /// Gets the estimated total bytes to write.
        /// </summary>
        public long EstimatedTotalBytes { get; init; }
        
        /// <summary>
        /// Gets the elapsed time since the operation started.
        /// </summary>
        public TimeSpan ElapsedTime { get; init; }
        
        /// <summary>
        /// Gets the estimated remaining time.
        /// </summary>
        public TimeSpan? EstimatedRemainingTime { get; init; }
        
        /// <summary>
        /// Gets additional contextual information.
        /// </summary>
        public IDictionary<string, object> Context { get; init; } = new Dictionary<string, object>();
        
        /// <summary>
        /// Creates a new progress report.
        /// </summary>
        public static DumpProgress Create(
            int percentComplete, 
            string currentOperation, 
            long bytesWritten = 0, 
            long estimatedTotalBytes = 0,
            TimeSpan elapsedTime = default,
            TimeSpan? estimatedRemainingTime = null,
            IDictionary<string, object> context = null)
        {
            return new DumpProgress
            {
                PercentComplete = Math.Clamp(percentComplete, 0, 100),
                CurrentOperation = currentOperation ?? string.Empty,
                BytesWritten = Math.Max(0, bytesWritten),
                EstimatedTotalBytes = Math.Max(0, estimatedTotalBytes),
                ElapsedTime = elapsedTime,
                EstimatedRemainingTime = estimatedRemainingTime,
                Context = context ?? new Dictionary<string, object>()
            };
        }
    }

    /// <summary>
    /// Represents the result of validating a dump file path.
    /// </summary>
    public sealed class PathValidationResult
    {
        /// <summary>
        /// Gets whether the path is valid.
        /// </summary>
        public bool IsValid { get; init; }
        
        /// <summary>
        /// Gets the validated (and possibly corrected) path.
        /// </summary>
        public string ValidatedPath { get; init; }
        
        /// <summary>
        /// Gets any validation warnings.
        /// </summary>
        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
        
        /// <summary>
        /// Gets any validation errors.
        /// </summary>
        public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
        
        /// <summary>
        /// Gets the available free space at the target location in bytes.
        /// </summary>
        public long AvailableFreeSpaceBytes { get; init; }
        
        /// <summary>
        /// Creates a valid result.
        /// </summary>
        public static PathValidationResult CreateValid(
            string validatedPath, 
            long availableFreeSpaceBytes, 
            IEnumerable<string> warnings = null)
        {
            return new PathValidationResult
            {
                IsValid = true,
                ValidatedPath = validatedPath,
                AvailableFreeSpaceBytes = availableFreeSpaceBytes,
                Warnings = warnings?.ToList() ?? new List<string>()
            };
        }
        
        /// <summary>
        /// Creates an invalid result.
        /// </summary>
        public static PathValidationResult CreateInvalid(
            string originalPath, 
            IEnumerable<string> errors, 
            IEnumerable<string> warnings = null)
        {
            return new PathValidationResult
            {
                IsValid = false,
                ValidatedPath = originalPath,
                Errors = errors?.ToList() ?? new List<string>(),
                Warnings = warnings?.ToList() ?? new List<string>()
            };
        }
    }

    /// <summary>
    /// Represents an estimation of dump file size before creation.
    /// </summary>
    public sealed class DumpSizeEstimation
    {
        /// <summary>
        /// Gets whether the estimation was successful.
        /// </summary>
        public bool Success { get; init; }
        
        /// <summary>
        /// Gets the estimated minimum size in bytes.
        /// </summary>
        public long EstimatedMinSizeBytes { get; init; }
        
        /// <summary>
        /// Gets the estimated maximum size in bytes.
        /// </summary>
        public long EstimatedMaxSizeBytes { get; init; }
        
        /// <summary>
        /// Gets the confidence level of the estimation (0.0 to 1.0).
        /// </summary>
        public double ConfidenceLevel { get; init; }
        
        /// <summary>
        /// Gets any warnings about the size estimation.
        /// </summary>
        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
        
        /// <summary>
        /// Gets the process memory information used for estimation.
        /// </summary>
        public ProcessMemoryInfo ProcessMemoryInfo { get; init; }
        
        /// <summary>
        /// Gets the estimated size range as a formatted string.
        /// </summary>
        public string FormattedSizeRange => 
            $"{FormatBytes(EstimatedMinSizeBytes)} - {FormatBytes(EstimatedMaxSizeBytes)}";
        
        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            if (bytes == 0) return "0 B";
            
            var place = Convert.ToInt32(Math.Floor(Math.Log(Math.Abs(bytes), 1024)));
            var num = Math.Round(bytes / Math.Pow(1024, place), 2);
            return $"{num:F2} {suffixes[place]}";
        }
    }

    /// <summary>
    /// Contains memory information about a process.
    /// </summary>
    public sealed class ProcessMemoryInfo
    {
        /// <summary>
        /// Gets the working set size in bytes.
        /// </summary>
        public long WorkingSetBytes { get; init; }
        
        /// <summary>
        /// Gets the virtual memory size in bytes.
        /// </summary>
        public long VirtualMemoryBytes { get; init; }
        
        /// <summary>
        /// Gets the private memory size in bytes.
        /// </summary>
        public long PrivateMemoryBytes { get; init; }
        
        /// <summary>
        /// Gets whether the process is a 64-bit process.
        /// </summary>
        public bool Is64BitProcess { get; init; }
        
        /// <summary>
        /// Gets the number of threads in the process.
        /// </summary>
        public int ThreadCount { get; init; }
        
        /// <summary>
        /// Gets the number of handles in the process.
        /// </summary>
        public int HandleCount { get; init; }
    }

    /// <summary>
    /// Configuration options for dump creation.
    /// </summary>
    public sealed class DumpCreationOptions
    {
        /// <summary>
        /// Gets or sets the maximum time to wait for dump creation.
        /// </summary>
        public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(10);
        
        /// <summary>
        /// Gets or sets whether to compress the dump file.
        /// </summary>
        public bool CompressOutput { get; init; } = false;
        
        /// <summary>
        /// Gets or sets whether to include detailed progress reporting.
        /// </summary>
        public bool DetailedProgress { get; init; } = true;
        
        /// <summary>
        /// Gets or sets the minimum free space required (in bytes) before creating a dump.
        /// </summary>
        public long MinimumFreeSpaceBytes { get; init; } = 100 * 1024 * 1024; // 100MB
        
        /// <summary>
        /// Gets or sets custom metadata to include with the dump.
        /// </summary>
        public IDictionary<string, object> CustomMetadata { get; init; } = new Dictionary<string, object>();
        
        /// <summary>
        /// Gets or sets whether to verify the dump after creation.
        /// </summary>
        public bool VerifyAfterCreation { get; init; } = true;
    }
} 