using System;
using System.Collections.ObjectModel;
using System.Text;

namespace DumpMiner.Models
{
    public class OperationModel
    {
        public int NumOfResults { get; set; }
        public string Types { get; set; }
        public ulong ObjectAddress { get; set; }
        public StringBuilder UserPrompt { get; set; } = new();
        public ObservableCollection<ConversationMessage> Chat { get; set; } = [];
        
        /// <summary>
        /// Operation-specific custom parameter (different for each operation)
        /// </summary>
        public object CustomParameter { get; set; }
        
        /// <summary>
        /// Human-readable description of the custom parameter for AI context
        /// </summary>
        public string CustomParameterDescription { get; set; }
    }

    /// <summary>
    /// Unified conversation message for both UI and AI provider use
    /// </summary>
    public class ConversationMessage
    {
        /// <summary>
        /// Message role: "user", "assistant", "system"
        /// </summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// Message content/text
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Message timestamp
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Display-friendly role name for UI binding
        /// </summary>
        public string DisplayRole => Role switch
        {
            "user" => "User",
            "assistant" => "AI",
            "system" => "System",
            _ => Role
        };
    }
}