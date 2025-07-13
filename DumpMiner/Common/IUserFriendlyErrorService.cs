using System;

namespace DumpMiner.Common
{
    /// <summary>
    /// Service for converting technical errors into user-friendly messages
    /// </summary>
    public interface IUserFriendlyErrorService
    {
        /// <summary>
        /// Converts a technical exception into a user-friendly error message
        /// </summary>
        /// <param name="exception">The technical exception</param>
        /// <param name="context">Additional context about where the error occurred</param>
        /// <returns>User-friendly error message with suggested actions</returns>
        UserFriendlyError ConvertError(Exception exception, string context = null);

        /// <summary>
        /// Shows a user-friendly error dialog
        /// </summary>
        /// <param name="exception">The technical exception</param>
        /// <param name="context">Additional context about where the error occurred</param>
        void ShowError(Exception exception, string context = null);

        /// <summary>
        /// Shows a user-friendly error dialog with a custom message
        /// </summary>
        /// <param name="message">User-friendly error message</param>
        /// <param name="title">Dialog title</param>
        void ShowError(string message, string title = "Error");
    }

    /// <summary>
    /// Represents a user-friendly error with helpful information
    /// </summary>
    public class UserFriendlyError
    {
        /// <summary>
        /// User-friendly error message
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Dialog title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Suggested actions the user can take
        /// </summary>
        public string SuggestedActions { get; set; }

        /// <summary>
        /// Error category for tracking
        /// </summary>
        public ErrorCategory Category { get; set; }

        /// <summary>
        /// Whether technical details should be available (for advanced users)
        /// </summary>
        public bool HasTechnicalDetails { get; set; }

        /// <summary>
        /// Technical details (hidden by default)
        /// </summary>
        public string TechnicalDetails { get; set; }
    }

    /// <summary>
    /// Categories of errors for better handling
    /// </summary>
    public enum ErrorCategory
    {
        Configuration,
        Network,
        Memory,
        FileSystem,
        Permission,
        UserInput,
        System,
        AI,
        Operation,
        Unknown
    }
} 