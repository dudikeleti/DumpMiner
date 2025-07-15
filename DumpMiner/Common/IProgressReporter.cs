using System;
using System.Threading;

namespace DumpMiner.Common
{
    /// <summary>
    /// Provides comprehensive progress reporting capabilities for long-running operations
    /// </summary>
    public interface IProgressReporter
    {
        /// <summary>
        /// Reports progress with percentage completion
        /// </summary>
        /// <param name="percentage">Completion percentage (0-100)</param>
        /// <param name="currentItem">Current item being processed (e.g., "Thread 3 of 15")</param>
        /// <param name="statusMessage">Detailed status message</param>
        void ReportProgress(int percentage, string currentItem = null, string statusMessage = null);

        /// <summary>
        /// Reports progress with specific counts
        /// </summary>
        /// <param name="current">Current item count</param>
        /// <param name="total">Total item count</param>
        /// <param name="itemType">Type of items being processed (e.g., "objects", "threads")</param>
        /// <param name="statusMessage">Detailed status message</param>
        void ReportProgress(long current, long total, string itemType, string statusMessage = null);

        /// <summary>
        /// Reports progress with time estimates
        /// </summary>
        /// <param name="percentage">Completion percentage (0-100)</param>
        /// <param name="currentItem">Current item being processed</param>
        /// <param name="statusMessage">Detailed status message</param>
        /// <param name="estimatedTimeRemaining">Estimated time remaining</param>
        /// <param name="processingSpeed">Processing speed (items per second)</param>
        void ReportProgress(int percentage, string currentItem, string statusMessage, 
            TimeSpan? estimatedTimeRemaining, double processingSpeed = 0);

        /// <summary>
        /// Reports a phase change in the operation
        /// </summary>
        /// <param name="phaseName">Name of the current phase</param>
        /// <param name="phaseDescription">Description of what's happening in this phase</param>
        void ReportPhase(string phaseName, string phaseDescription);

        /// <summary>
        /// Reports that the operation has completed
        /// </summary>
        /// <param name="totalProcessed">Total items processed</param>
        /// <param name="totalTime">Total time taken</param>
        void ReportCompleted(long totalProcessed, TimeSpan totalTime);

        /// <summary>
        /// Event fired when progress is updated
        /// </summary>
        event EventHandler<ProgressEventArgs> ProgressChanged;
    }

    /// <summary>
    /// Event arguments for progress updates
    /// </summary>
    public class ProgressEventArgs : EventArgs
    {
        public int Percentage { get; set; }
        public string CurrentItem { get; set; }
        public string StatusMessage { get; set; }
        public TimeSpan? EstimatedTimeRemaining { get; set; }
        public double ProcessingSpeed { get; set; }
        public string PhaseName { get; set; }
        public string PhaseDescription { get; set; }
        public bool IsCompleted { get; set; }
        public long TotalProcessed { get; set; }
        public TimeSpan TotalTime { get; set; }
        public long CurrentCount { get; set; }
        public long TotalCount { get; set; }
        public string ItemType { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
} 