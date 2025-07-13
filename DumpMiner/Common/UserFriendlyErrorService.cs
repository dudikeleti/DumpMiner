using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using Microsoft.Extensions.Logging;

namespace DumpMiner.Common
{
    /// <summary>
    /// Service for converting technical errors into user-friendly messages
    /// </summary>
    [Export(typeof(IUserFriendlyErrorService))]
    public class UserFriendlyErrorService : IUserFriendlyErrorService
    {
        private readonly ILogger<UserFriendlyErrorService> _logger;
        private readonly IDialogService _dialogService;

        public UserFriendlyErrorService()
        {
            _dialogService = App.Container.GetExport<IDialogService>().Value;
            _logger = LoggingExtensions.CreateLogger<UserFriendlyErrorService>();
        }

        /// <summary>
        /// Converts a technical exception into a user-friendly error message
        /// </summary>
        public UserFriendlyError ConvertError(Exception exception, string context = null)
        {
            if (exception == null)
                return CreateGenericError("An unknown error occurred", context);

            _logger.LogError(exception, "Converting error to user-friendly message. Context: {Context}", context);

            var error = new UserFriendlyError
            {
                HasTechnicalDetails = true,
                TechnicalDetails = FormatTechnicalDetails(exception, context)
            };

            // Handle specific exception types
            switch (exception)
            {
                case OperationCanceledException:
                    error.Title = "Operation Cancelled";
                    error.Message = "The operation was cancelled by user request.";
                    error.Category = ErrorCategory.Operation;
                    error.SuggestedActions = "• If you need the operation to complete, try running it again\n• For long operations, consider increasing the timeout in Settings";
                    break;

                case TimeoutException:
                    error.Title = "Operation Timed Out";
                    error.Message = "The operation took too long to complete and was automatically cancelled.";
                    error.Category = ErrorCategory.Operation;
                    error.SuggestedActions = "• Try increasing the timeout in Settings → General\n• For large dumps, consider using a more powerful machine\n• Check if the target process is responsive";
                    break;

                case UnauthorizedAccessException:
                case System.Security.SecurityException:
                    error.Title = "Access Denied";
                    error.Message = "DumpMiner doesn't have permission to access the required resource.";
                    error.Category = ErrorCategory.Permission;
                    error.SuggestedActions = "• Run DumpMiner as Administrator\n• Check if the target process allows debugging\n• Ensure the file is not locked by another process";
                    break;

                case FileNotFoundException:
                case DirectoryNotFoundException:
                    error.Title = "File Not Found";
                    error.Message = GetFileNotFoundMessage(exception);
                    error.Category = ErrorCategory.FileSystem;
                    error.SuggestedActions = "• Check if the file path is correct\n• Ensure the file hasn't been moved or deleted\n• For dump files, verify the file format is supported";
                    break;

                case IOException:
                    error.Title = "File Access Error";
                    error.Message = "There was a problem accessing a file or directory.";
                    error.Category = ErrorCategory.FileSystem;
                    error.SuggestedActions = "• Check if the file is being used by another process\n• Ensure you have sufficient disk space\n• Try closing other applications that might be using the file";
                    break;

                case OutOfMemoryException:
                    error.Title = "Memory Limit Exceeded";
                    error.Message = "DumpMiner ran out of memory while processing the operation.";
                    error.Category = ErrorCategory.Memory;
                    error.SuggestedActions = "• Close other applications to free memory\n• For large dumps, try processing smaller sections\n• Consider using a machine with more RAM";
                    break;

                case HttpRequestException:
                case WebException:
                    error.Title = "Network Connection Error";
                    error.Message = "Could not connect to the required service.";
                    error.Category = ErrorCategory.Network;
                    error.SuggestedActions = "• Check your internet connection\n• Verify proxy settings if applicable\n• For AI features, check if the API key is valid";
                    break;

                case ArgumentNullException:
                case ArgumentOutOfRangeException:
                case ArgumentException:
                    error.Title = "Invalid Input";
                    error.Message = GetArgumentErrorMessage(exception, context);
                    error.Category = ErrorCategory.UserInput;
                    error.SuggestedActions = "• Check that all required fields are filled\n• Ensure addresses are in hexadecimal format (e.g., 0x12345678)\n• Verify numeric values are within valid ranges";
                    break;

                case InvalidOperationException:
                    error = HandleInvalidOperationException(exception, context);
                    break;

                case NotSupportedException:
                    error.Title = "Operation Not Supported";
                    error.Message = "This operation is not supported in the current context.";
                    error.Category = ErrorCategory.System;
                    error.SuggestedActions = "• Check if you're using a supported .NET version\n• Ensure the target process is a .NET application\n• Try with a different dump or process";
                    break;

                case System.Runtime.InteropServices.COMException:
                    error.Title = "Debugging Engine Error";
                    error.Message = "The debugging engine encountered an error while processing the request.";
                    error.Category = ErrorCategory.System;
                    error.SuggestedActions = "• Try detaching and reattaching to the process\n• Restart DumpMiner\n• Check if the target process is still running";
                    break;

                default:
                    error = CreateGenericError(exception.Message, context);
                    break;
            }

            return error;
        }

        /// <summary>
        /// Shows a user-friendly error dialog
        /// </summary>
        public void ShowError(Exception exception, string context = null)
        {
            var error = ConvertError(exception, context);
            ShowErrorDialog(error);
        }

        /// <summary>
        /// Shows a user-friendly error dialog with a custom message
        /// </summary>
        public void ShowError(string message, string title = "Error")
        {
            var error = new UserFriendlyError
            {
                Message = message,
                Title = title,
                Category = ErrorCategory.Unknown,
                HasTechnicalDetails = false
            };
            ShowErrorDialog(error);
        }

        private UserFriendlyError CreateGenericError(string message, string context)
        {
            return new UserFriendlyError
            {
                Title = "Unexpected Error",
                Message = !string.IsNullOrEmpty(message) ? message : "An unexpected error occurred.",
                Category = ErrorCategory.Unknown,
                SuggestedActions = "• Try the operation again\n• Restart DumpMiner if the problem persists\n• Check the logs for more details",
                HasTechnicalDetails = false
            };
        }

        private UserFriendlyError HandleInvalidOperationException(Exception exception, string context)
        {
            var message = exception.Message.ToLower();
            
            if (message.Contains("attached") || message.Contains("detached"))
            {
                return new UserFriendlyError
                {
                    Title = "Process Connection Error",
                    Message = "The operation cannot be performed because there is no active connection to a process or dump file.",
                    Category = ErrorCategory.Operation,
                    SuggestedActions = "• Use 'Attach/Detach' to connect to a process\n• Load a dump file using 'Load Dump'\n• Ensure the target process is still running",
                    HasTechnicalDetails = true,
                    TechnicalDetails = FormatTechnicalDetails(exception, context)
                };
            }

            if (message.Contains("api key") || message.Contains("authentication"))
            {
                return new UserFriendlyError
                {
                    Title = "AI Service Configuration Error",
                    Message = "The AI service is not properly configured or authenticated.",
                    Category = ErrorCategory.AI,
                    SuggestedActions = "• Go to Settings → AI Settings\n• Configure at least one AI provider\n• Enter a valid API key\n• Test the connection",
                    HasTechnicalDetails = true,
                    TechnicalDetails = FormatTechnicalDetails(exception, context)
                };
            }

            if (message.Contains("configuration") || message.Contains("settings"))
            {
                return new UserFriendlyError
                {
                    Title = "Configuration Error",
                    Message = "There is an issue with the application configuration.",
                    Category = ErrorCategory.Configuration,
                    SuggestedActions = "• Check Settings for any invalid values\n• Try resetting to default settings\n• Restart DumpMiner\n• Check the appsettings.json file",
                    HasTechnicalDetails = true,
                    TechnicalDetails = FormatTechnicalDetails(exception, context)
                };
            }

            return new UserFriendlyError
            {
                Title = "Operation Not Allowed",
                Message = "The requested operation cannot be performed in the current state.",
                Category = ErrorCategory.Operation,
                SuggestedActions = "• Check the current state of the application\n• Ensure all prerequisites are met\n• Try the operation in a different order",
                HasTechnicalDetails = true,
                TechnicalDetails = FormatTechnicalDetails(exception, context)
            };
        }

        private string GetFileNotFoundMessage(Exception exception)
        {
            if (exception.Message.Contains(".dmp") || exception.Message.Contains("dump"))
                return "The specified dump file could not be found.";
            
            if (exception.Message.Contains("symbol") || exception.Message.Contains(".pdb"))
                return "Symbol files could not be found. This may affect the quality of analysis.";
            
            if (exception.Message.Contains("config") || exception.Message.Contains("settings"))
                return "Configuration file could not be found. Default settings will be used.";
            
            return "A required file could not be found.";
        }

        private string GetArgumentErrorMessage(Exception exception, string context)
        {
            var message = exception.Message.ToLower();
            
            if (message.Contains("address") || message.Contains("pointer"))
                return "The specified memory address is invalid or out of range.";
            
            if (message.Contains("null") || message.Contains("empty"))
                return "A required value was not provided.";
            
            if (message.Contains("format") || message.Contains("parse"))
                return "The input format is invalid. Please check the format and try again.";
            
            if (message.Contains("range") || message.Contains("bounds"))
                return "The specified value is outside the valid range.";
            
            return "The provided input is invalid.";
        }

        private string FormatTechnicalDetails(Exception exception, string context)
        {
            var details = new StringBuilder();
            
            if (!string.IsNullOrEmpty(context))
            {
                details.AppendLine($"Context: {context}");
                details.AppendLine();
            }
            
            details.AppendLine($"Exception Type: {exception.GetType().Name}");
            details.AppendLine($"Message: {exception.Message}");
            
            if (exception.InnerException != null)
            {
                details.AppendLine($"Inner Exception: {exception.InnerException.GetType().Name}");
                details.AppendLine($"Inner Message: {exception.InnerException.Message}");
            }
            
            // Include only the first line of stack trace to avoid overwhelming users
            if (!string.IsNullOrEmpty(exception.StackTrace))
            {
                var stackLines = exception.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                if (stackLines.Length > 0)
                {
                    details.AppendLine($"Location: {stackLines[0].Trim()}");
                }
            }
            
            return details.ToString();
        }

        private void ShowErrorDialog(UserFriendlyError error)
        {
            var message = new StringBuilder();
            message.AppendLine(error.Message);
            
            if (!string.IsNullOrEmpty(error.SuggestedActions))
            {
                message.AppendLine();
                message.AppendLine("Suggested Actions:");
                message.AppendLine(error.SuggestedActions);
            }
            
            if (error.HasTechnicalDetails && !string.IsNullOrEmpty(error.TechnicalDetails))
            {
                message.AppendLine();
                message.AppendLine("Technical Details:");
                message.AppendLine(error.TechnicalDetails);
            }
            
            _dialogService.ShowDialog(message.ToString(), error.Title);
        }
    }
} 