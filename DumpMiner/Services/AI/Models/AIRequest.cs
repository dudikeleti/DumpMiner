using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using DumpMiner.Services.AI.Configuration;
using DumpMiner.Models;

namespace DumpMiner.Services.AI.Models
{
    /// <summary>
    /// Represents a request to an AI provider for dump analysis
    /// </summary>
    public sealed class AIRequest
    {
        /// <summary>
        /// Unique identifier for this request
        /// </summary>
        public string RequestId { get; init; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The user's question or prompt
        /// </summary>
        public string UserPrompt { get; init; } = string.Empty;

        /// <summary>
        /// System prompt for context and behavior
        /// </summary>
        public string SystemPrompt { get; init; } = string.Empty;

        /// <summary>
        /// Dump analysis context
        /// </summary>
        public DumpContext? DumpContext { get; init; }

        /// <summary>
        /// Operation-specific context
        /// </summary>
        public OperationContext? OperationContext { get; init; }

        /// <summary>
        /// Conversation history for context
        /// </summary>
        public List<ConversationMessage> ConversationHistory { get; init; } = new();

        /// <summary>
        /// Preferred AI provider
        /// </summary>
        public AIProviderType? PreferredProvider { get; init; }

        /// <summary>
        /// Maximum tokens for the response
        /// </summary>
        public int? MaxTokens { get; init; }

        /// <summary>
        /// Temperature for response creativity (0.0 to 1.0)
        /// </summary>
        public double? Temperature { get; init; }

        /// <summary>
        /// Request timestamp
        /// </summary>
        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Enable caching for this request
        /// </summary>
        public bool EnableCaching { get; init; } = true;
    }

    /// <summary>
    /// Represents a response from an AI provider
    /// </summary>
    public sealed class AIResponse
    {
        /// <summary>
        /// Request ID this response corresponds to
        /// </summary>
        public string RequestId { get; init; } = string.Empty;

        /// <summary>
        /// The AI's response content
        /// </summary>
        public string Content { get; init; } = string.Empty;

        /// <summary>
        /// Provider that generated this response
        /// </summary>
        public AIProviderType Provider { get; init; }

        /// <summary>
        /// Model used for generation
        /// </summary>
        public string Model { get; init; } = string.Empty;

        /// <summary>
        /// Success status
        /// </summary>
        public bool IsSuccess { get; init; } = true;

        /// <summary>
        /// Error message if failed
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Response metadata
        /// </summary>
        public ResponseMetadata Metadata { get; init; } = new();

        /// <summary>
        /// Response timestamp
        /// </summary>
        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Whether response was served from cache
        /// </summary>
        public bool IsFromCache { get; init; }
    }

    /// <summary>
    /// Metadata about the AI response
    /// </summary>
    public sealed class ResponseMetadata
    {
        /// <summary>
        /// Tokens used in the prompt
        /// </summary>
        public int PromptTokens { get; init; }

        /// <summary>
        /// Tokens generated in the response
        /// </summary>
        public int CompletionTokens { get; init; }

        /// <summary>
        /// Total tokens used
        /// </summary>
        public int TotalTokens { get; init; }

        /// <summary>
        /// Processing time in milliseconds
        /// </summary>
        public long ProcessingTimeMs { get; init; }

        /// <summary>
        /// Estimated cost in USD (if available)
        /// </summary>
        public decimal? EstimatedCost { get; init; }
    }

    /// <summary>
    /// Represents dump analysis context
    /// </summary>
    public sealed class DumpContext
    {
        /// <summary>
        /// Process information
        /// </summary>
        public ProcessInfo? ProcessInfo { get; init; }

        /// <summary>
        /// Heap statistics
        /// </summary>
        public HeapStatistics? HeapStats { get; init; }

        /// <summary>
        /// Exception information
        /// </summary>
        public List<ExceptionInfo> Exceptions { get; init; } = new();

        /// <summary>
        /// Thread information
        /// </summary>
        public List<ThreadInfo> Threads { get; init; } = new();

        /// <summary>
        /// Large objects information
        /// </summary>
        public List<ObjectInfo> LargeObjects { get; init; } = new();

        /// <summary>
        /// Additional context data
        /// </summary>
        public Dictionary<string, object> AdditionalData { get; init; } = new();
    }

    /// <summary>
    /// Operation-specific context information
    /// </summary>
    public sealed class OperationContext
    {
        /// <summary>
        /// Name of the current operation
        /// </summary>
        public string OperationName { get; init; } = string.Empty;

        /// <summary>
        /// Operation parameters
        /// </summary>
        public Dictionary<string, object> Parameters { get; init; } = new();

        /// <summary>
        /// Analysis results
        /// </summary>
        public List<object> Results { get; init; } = new();

        /// <summary>
        /// Result count
        /// </summary>
        public int ResultCount { get; init; }
    }

    // ConversationMessage is now imported from DumpMiner.Models

    // Supporting classes for context
    public sealed class ProcessInfo
    {
        public int? ProcessId { get; init; }
        public string ProcessName { get; init; } = string.Empty;
        public long WorkingSetSize { get; init; }
        public string ClrVersion { get; init; } = string.Empty;
        public int ThreadCount { get; init; }
    }

    public sealed class HeapStatistics
    {
        public long TotalSize { get; init; }
        public long Gen0Size { get; init; }
        public long Gen1Size { get; init; }
        public long Gen2Size { get; init; }
        public long LargeObjectHeapSize { get; init; }
        public int ObjectCount { get; init; }
    }

    public sealed class ExceptionInfo
    {
        public string Type { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string StackTrace { get; init; } = string.Empty;
        public ulong Address { get; init; }
    }

    public sealed class ThreadInfo
    {
        public int ThreadId { get; init; }
        public string State { get; init; } = string.Empty;
        public ulong StackPointer { get; init; }
        public List<StackFrame> StackFrames { get; init; } = new();
    }

    public sealed class StackFrame
    {
        public string MethodName { get; init; } = string.Empty;
        public string ModuleName { get; init; } = string.Empty;
        public ulong InstructionPointer { get; init; }
    }

    public sealed class ObjectInfo
    {
        public ulong Address { get; init; }
        public string Type { get; init; } = string.Empty;
        public long Size { get; init; }
        public int Generation { get; init; }
    }
} 