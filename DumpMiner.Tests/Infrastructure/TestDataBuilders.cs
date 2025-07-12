using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DumpMiner.Models;
using DumpMiner.Services.AI.Configuration;
using DumpMiner.Services.AI.Models;
using Microsoft.Diagnostics.Runtime;

namespace DumpMiner.Tests.Infrastructure
{
    /// <summary>
    /// Builder for creating test OperationModel instances
    /// </summary>
    public class OperationModelBuilder
    {
        private readonly OperationModel _model;

        public OperationModelBuilder()
        {
            _model = new OperationModel();
        }

        public OperationModelBuilder WithObjectAddress(ulong address)
        {
            _model.ObjectAddress = address;
            return this;
        }

        public OperationModelBuilder WithTypes(string types)
        {
            _model.Types = types;
            return this;
        }

        public OperationModelBuilder WithNumOfResults(int numOfResults)
        {
            _model.NumOfResults = numOfResults;
            return this;
        }

        public OperationModelBuilder WithUserPrompt(string prompt)
        {
            _model.UserPrompt = new System.Text.StringBuilder(prompt);
            return this;
        }

        public OperationModelBuilder WithChat(params ConversationMessage[] messages)
        {
            _model.Chat = new ObservableCollection<ConversationMessage>(messages);
            return this;
        }

        public OperationModel Build() => _model;
    }

    /// <summary>
    /// Builder for creating test AIRequest instances
    /// </summary>
    public class AIRequestBuilder
    {
        private readonly AIRequest _request;

        public AIRequestBuilder()
        {
            _request = new AIRequest
            {
                RequestId = Guid.NewGuid().ToString(),
                SystemPrompt = "You are a helpful assistant",
                UserPrompt = "Test prompt",
                MaxTokens = 1000,
                Temperature = 0.7,
                ConversationHistory = new List<ConversationMessage>()
            };
        }

        public AIRequestBuilder WithRequestId(string requestId)
        {
            _request.RequestId = requestId;
            return this;
        }

        public AIRequestBuilder WithSystemPrompt(string systemPrompt)
        {
            _request.SystemPrompt = systemPrompt;
            return this;
        }

        public AIRequestBuilder WithUserPrompt(string userPrompt)
        {
            _request.UserPrompt = userPrompt;
            return this;
        }

        public AIRequestBuilder WithMaxTokens(int maxTokens)
        {
            _request.MaxTokens = maxTokens;
            return this;
        }

        public AIRequestBuilder WithTemperature(double temperature)
        {
            _request.Temperature = temperature;
            return this;
        }

        public AIRequestBuilder WithProvider(AIProviderType provider)
        {
            _request.PreferredProvider = provider;
            return this;
        }

        public AIRequestBuilder WithConversationHistory(params ConversationMessage[] messages)
        {
            _request.ConversationHistory = messages.ToList();
            return this;
        }

        public AIRequestBuilder WithDumpContext(DumpContext context)
        {
            _request.DumpContext = context;
            return this;
        }

        public AIRequestBuilder WithOperationContext(OperationContext context)
        {
            _request.OperationContext = context;
            return this;
        }

        public AIRequest Build() => _request;
    }

    /// <summary>
    /// Builder for creating test AIResponse instances
    /// </summary>
    public class AIResponseBuilder
    {
        private readonly AIResponse _response;

        public AIResponseBuilder()
        {
            _response = new AIResponse
            {
                RequestId = Guid.NewGuid().ToString(),
                Content = "Test response content",
                Provider = AIProviderType.OpenAI,
                Model = "gpt-4",
                IsSuccess = true,
                Timestamp = DateTimeOffset.UtcNow,
                Metadata = new ResponseMetadata
                {
                    PromptTokens = 100,
                    CompletionTokens = 200,
                    TotalTokens = 300,
                    ProcessingTimeMs = 1500,
                    EstimatedCost = 0.01m
                }
            };
        }

        public AIResponseBuilder WithRequestId(string requestId)
        {
            _response.RequestId = requestId;
            return this;
        }

        public AIResponseBuilder WithContent(string content)
        {
            _response.Content = content;
            return this;
        }

        public AIResponseBuilder WithProvider(AIProviderType provider)
        {
            _response.Provider = provider;
            return this;
        }

        public AIResponseBuilder WithModel(string model)
        {
            _response.Model = model;
            return this;
        }

        public AIResponseBuilder WithSuccess(bool isSuccess)
        {
            _response.IsSuccess = isSuccess;
            return this;
        }

        public AIResponseBuilder WithError(string errorMessage)
        {
            _response.IsSuccess = false;
            _response.ErrorMessage = errorMessage;
            return this;
        }

        public AIResponseBuilder WithTokens(int promptTokens, int completionTokens)
        {
            _response.Metadata.PromptTokens = promptTokens;
            _response.Metadata.CompletionTokens = completionTokens;
            _response.Metadata.TotalTokens = promptTokens + completionTokens;
            return this;
        }

        public AIResponseBuilder WithProcessingTime(int milliseconds)
        {
            _response.Metadata.ProcessingTimeMs = milliseconds;
            return this;
        }

        public AIResponseBuilder WithCost(decimal cost)
        {
            _response.Metadata.EstimatedCost = cost;
            return this;
        }

        public AIResponse Build() => _response;
    }

    /// <summary>
    /// Builder for creating test DumpContext instances
    /// </summary>
    public class DumpContextBuilder
    {
        private readonly DumpContext _context;

        public DumpContextBuilder()
        {
            _context = new DumpContext();
        }

        public DumpContextBuilder WithProcessInfo(string processName, int processId, string clrVersion = "8.0.0")
        {
            _context.ProcessInfo = new ProcessInfo
            {
                ProcessName = processName,
                ProcessId = processId,
                ClrVersion = clrVersion,
                ThreadCount = 10
            };
            return this;
        }

        public DumpContextBuilder WithHeapStats(long totalSize, int objectCount)
        {
            _context.HeapStats = new HeapStatistics
            {
                TotalSize = totalSize,
                ObjectCount = objectCount,
                Gen0Size = totalSize / 10,
                Gen1Size = totalSize / 5,
                Gen2Size = totalSize / 2,
                LargeObjectHeapSize = totalSize / 4
            };
            return this;
        }

        public DumpContextBuilder WithExceptions(params ExceptionInfo[] exceptions)
        {
            _context.Exceptions = exceptions.ToList();
            return this;
        }

        public DumpContextBuilder WithException(string type, string message, ulong address = 0x12345678)
        {
            _context.Exceptions = _context.Exceptions ?? new List<ExceptionInfo>();
            _context.Exceptions.Add(new ExceptionInfo
            {
                Type = type,
                Message = message,
                Address = address
            });
            return this;
        }

        public DumpContext Build() => _context;
    }

    /// <summary>
    /// Builder for creating test AI Configuration instances
    /// </summary>
    public class AIConfigurationBuilder
    {
        private readonly AIConfiguration _config;

        public AIConfigurationBuilder()
        {
            _config = new AIConfiguration();
        }

        public AIConfigurationBuilder WithDefaultProvider(AIProviderType provider)
        {
            _config.DefaultProvider = provider;
            return this;
        }

        public AIConfigurationBuilder WithMaxTokens(int maxTokens)
        {
            _config.MaxTokens = maxTokens;
            return this;
        }

        public AIConfigurationBuilder WithTimeout(int timeoutSeconds)
        {
            _config.TimeoutSeconds = timeoutSeconds;
            return this;
        }

        public AIConfigurationBuilder WithOpenAI(string apiKey, string model = "gpt-4", bool enabled = true)
        {
            _config.Providers.OpenAI = new OpenAIConfiguration
            {
                ApiKey = apiKey,
                Model = model,
                IsEnabled = enabled
            };
            return this;
        }

        public AIConfigurationBuilder WithAnthropic(string apiKey, string model = "claude-3-opus-20240229", bool enabled = true)
        {
            _config.Providers.Anthropic = new AnthropicConfiguration
            {
                ApiKey = apiKey,
                Model = model,
                IsEnabled = enabled
            };
            return this;
        }

        public AIConfigurationBuilder WithGoogle(string apiKey, string model = "gemini-pro", bool enabled = true)
        {
            _config.Providers.Google = new GoogleConfiguration
            {
                ApiKey = apiKey,
                Model = model,
                IsEnabled = enabled
            };
            return this;
        }

        public AIConfigurationBuilder WithCaching(bool enabled = true, int expirationMinutes = 60)
        {
            _config.EnableCaching = enabled;
            _config.CacheExpirationMinutes = expirationMinutes;
            return this;
        }

        public AIConfiguration Build() => _config;
    }

    /// <summary>
    /// Builder for creating test ConversationMessage instances
    /// </summary>
    public class ConversationMessageBuilder
    {
        private readonly ConversationMessage _message;

        public ConversationMessageBuilder()
        {
            _message = new ConversationMessage();
        }

        public ConversationMessageBuilder WithRole(string role)
        {
            _message.Role = role;
            return this;
        }

        public ConversationMessageBuilder WithContent(string content)
        {
            _message.Content = content;
            return this;
        }

        public ConversationMessageBuilder WithTimestamp(DateTimeOffset timestamp)
        {
            _message.Timestamp = timestamp;
            return this;
        }

        public ConversationMessageBuilder AsUser(string content)
        {
            _message.Role = "user";
            _message.Content = content;
            return this;
        }

        public ConversationMessageBuilder AsAssistant(string content)
        {
            _message.Role = "assistant";
            _message.Content = content;
            return this;
        }

        public ConversationMessageBuilder AsSystem(string content)
        {
            _message.Role = "system";
            _message.Content = content;
            return this;
        }

        public ConversationMessage Build() => _message;
    }

    /// <summary>
    /// Static factory methods for common test data scenarios
    /// </summary>
    public static class TestDataFactory
    {
        /// <summary>
        /// Creates a basic operation model for testing
        /// </summary>
        public static OperationModel CreateBasicOperationModel()
        {
            return new OperationModelBuilder()
                .WithObjectAddress(0x12345678)
                .WithTypes("System.String")
                .WithNumOfResults(100)
                .WithUserPrompt("Test prompt")
                .Build();
        }

        /// <summary>
        /// Creates a basic AI request for testing
        /// </summary>
        public static AIRequest CreateBasicAIRequest()
        {
            return new AIRequestBuilder()
                .WithSystemPrompt("You are a memory dump analysis expert.")
                .WithUserPrompt("Analyze this memory dump for potential issues.")
                .WithMaxTokens(2000)
                .WithTemperature(0.2)
                .Build();
        }

        /// <summary>
        /// Creates a successful AI response for testing
        /// </summary>
        public static AIResponse CreateSuccessfulAIResponse()
        {
            return new AIResponseBuilder()
                .WithContent("The memory dump analysis shows no critical issues.")
                .WithProvider(AIProviderType.OpenAI)
                .WithModel("gpt-4")
                .WithTokens(150, 250)
                .WithProcessingTime(2000)
                .WithCost(0.015m)
                .Build();
        }

        /// <summary>
        /// Creates a failed AI response for testing
        /// </summary>
        public static AIResponse CreateFailedAIResponse(string errorMessage = "API rate limit exceeded")
        {
            return new AIResponseBuilder()
                .WithError(errorMessage)
                .Build();
        }

        /// <summary>
        /// Creates a basic dump context for testing
        /// </summary>
        public static DumpContext CreateBasicDumpContext()
        {
            return new DumpContextBuilder()
                .WithProcessInfo("TestApp.exe", 1234, "8.0.0")
                .WithHeapStats(100_000_000, 50000)
                .WithException("OutOfMemoryException", "Insufficient memory to continue")
                .Build();
        }

        /// <summary>
        /// Creates a comprehensive AI configuration for testing
        /// </summary>
        public static AIConfiguration CreateTestAIConfiguration()
        {
            return new AIConfigurationBuilder()
                .WithDefaultProvider(AIProviderType.OpenAI)
                .WithMaxTokens(4000)
                .WithTimeout(60)
                .WithOpenAI("sk-test-key", "gpt-4", true)
                .WithAnthropic("anthropic-test-key", "claude-3-opus-20240229", true)
                .WithGoogle("google-test-key", "gemini-pro", false)
                .WithCaching(true, 30)
                .Build();
        }

        /// <summary>
        /// Creates a conversation with multiple messages
        /// </summary>
        public static List<ConversationMessage> CreateConversationHistory()
        {
            return new List<ConversationMessage>
            {
                new ConversationMessageBuilder()
                    .AsUser("What does this memory dump tell us about the application?")
                    .Build(),
                
                new ConversationMessageBuilder()
                    .AsAssistant("The dump shows high memory usage with several OutOfMemoryExceptions.")
                    .Build(),
                
                new ConversationMessageBuilder()
                    .AsUser("What should we do to fix this?")
                    .Build(),
                
                new ConversationMessageBuilder()
                    .AsAssistant("I recommend investigating the large object heap and implementing object pooling.")
                    .Build()
            };
        }

        /// <summary>
        /// Creates test data for specific scenarios
        /// </summary>
        public static class Scenarios
        {
            /// <summary>
            /// Creates test data for a memory leak scenario
            /// </summary>
            public static (OperationModel model, DumpContext context, AIRequest request) MemoryLeak()
            {
                var model = new OperationModelBuilder()
                    .WithTypes("System.String;System.Collections.Generic.List`1")
                    .WithNumOfResults(1000)
                    .WithUserPrompt("Analyze for memory leaks")
                    .Build();

                var context = new DumpContextBuilder()
                    .WithProcessInfo("LeakyApp.exe", 5678)
                    .WithHeapStats(2_000_000_000, 1_000_000)
                    .WithException("OutOfMemoryException", "Not enough memory available")
                    .Build();

                var request = new AIRequestBuilder()
                    .WithSystemPrompt("You are a memory leak detection expert.")
                    .WithUserPrompt("Analyze this heap dump for potential memory leaks.")
                    .WithDumpContext(context)
                    .WithMaxTokens(4000)
                    .Build();

                return (model, context, request);
            }

            /// <summary>
            /// Creates test data for a performance analysis scenario
            /// </summary>
            public static (OperationModel model, DumpContext context, AIRequest request) PerformanceIssue()
            {
                var model = new OperationModelBuilder()
                    .WithTypes("System.Threading.Tasks.Task")
                    .WithNumOfResults(500)
                    .WithUserPrompt("Analyze performance bottlenecks")
                    .Build();

                var context = new DumpContextBuilder()
                    .WithProcessInfo("SlowApp.exe", 9012)
                    .WithHeapStats(500_000_000, 200_000)
                    .Build();

                var request = new AIRequestBuilder()
                    .WithSystemPrompt("You are a .NET performance optimization expert.")
                    .WithUserPrompt("Identify performance bottlenecks in this application.")
                    .WithDumpContext(context)
                    .WithMaxTokens(3000)
                    .Build();

                return (model, context, request);
            }

            /// <summary>
            /// Creates test data for a deadlock scenario
            /// </summary>
            public static (OperationModel model, DumpContext context, AIRequest request) Deadlock()
            {
                var model = new OperationModelBuilder()
                    .WithUserPrompt("Analyze for deadlocks")
                    .Build();

                var context = new DumpContextBuilder()
                    .WithProcessInfo("DeadlockedApp.exe", 3456)
                    .WithHeapStats(300_000_000, 150_000)
                    .Build();

                var request = new AIRequestBuilder()
                    .WithSystemPrompt("You are a threading and deadlock analysis expert.")
                    .WithUserPrompt("Analyze this dump for potential deadlocks.")
                    .WithDumpContext(context)
                    .WithMaxTokens(2500)
                    .Build();

                return (model, context, request);
            }
        }
    }
} 