using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Common;
using DumpMiner.Models;
using DumpMiner.Services.AI.Configuration;
using DumpMiner.Services.AI.Interfaces;
using DumpMiner.Services.AI.Models;
using DumpMiner.Services.AI.Orchestration;
using DumpMiner.Services.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DumpMiner.Tests.Infrastructure
{
    /// <summary>
    /// Factory for creating commonly used mocks with sensible defaults
    /// </summary>
    public static class MockFactories
    {
        /// <summary>
        /// Creates a mock AI service manager with default behavior
        /// </summary>
        public static Mock<IAIServiceManager> CreateAIServiceManager()
        {
            var mock = new Mock<IAIServiceManager>();

            // Setup default properties
            mock.Setup(x => x.AvailableProviders)
                .Returns(new[] { AIProviderType.OpenAI, AIProviderType.Anthropic });

            mock.Setup(x => x.DefaultProvider)
                .Returns(AIProviderType.OpenAI);

            // Setup default methods
            mock.Setup(x => x.IsProviderAvailableAsync(It.IsAny<AIProviderType>()))
                .ReturnsAsync(true);

            mock.Setup(x => x.IsAvailableAsync())
                .ReturnsAsync(true);

            mock.Setup(x => x.AnalyzeDumpAsync(It.IsAny<AIRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AIRequest request, CancellationToken _) =>
                    TestDataFactory.CreateSuccessfulAIResponse());

            mock.Setup(x => x.AskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AIProviderType?>()))
                .ReturnsAsync("Test AI response");

            mock.Setup(x => x.EstimateCost(It.IsAny<AIRequest>(), It.IsAny<AIProviderType?>()))
                .Returns(0.01m);

            mock.Setup(x => x.GetConversationHistoryAsync(It.IsAny<string>()))
                .ReturnsAsync(TestDataFactory.CreateConversationHistory());

            mock.Setup(x => x.GetConfiguration())
                .Returns(TestDataFactory.CreateTestAIConfiguration());

            return mock;
        }

        /// <summary>
        /// Creates a mock AI provider with default behavior
        /// </summary>
        public static Mock<IAIProvider> CreateAIProvider(AIProviderType providerType = AIProviderType.OpenAI)
        {
            var mock = new Mock<IAIProvider>();

            mock.Setup(x => x.ProviderType).Returns(providerType);
            mock.Setup(x => x.DisplayName).Returns(providerType.ToString());
            mock.Setup(x => x.IsConfigured).Returns(true);
            mock.Setup(x => x.SupportedModels).Returns(new[] { "gpt-4", "gpt-3.5-turbo" });

            mock.Setup(x => x.CompleteAsync(It.IsAny<AIRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AIRequest request, CancellationToken _) =>
                    TestDataFactory.CreateSuccessfulAIResponse());

            mock.Setup(x => x.TestConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(ProviderTestResult.Success(TimeSpan.FromMilliseconds(500), "gpt-4"));

            mock.Setup(x => x.EstimateCost(It.IsAny<AIRequest>()))
                .Returns(0.01m);

            return mock;
        }

        /// <summary>
        /// Creates a mock AI orchestrator with default behavior
        /// </summary>
        public static Mock<IAIOrchestrator> CreateAIOrchestrator()
        {
            var mock = new Mock<IAIOrchestrator>();

            mock.Setup(x => x.IsAvailableAsync())
                .ReturnsAsync(true);

            mock.Setup(x => x.AnalyzeOperationAsync(
                It.IsAny<string>(),
                It.IsAny<OperationModel>(),
                It.IsAny<Collection<object>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AIAnalysisResult
                {
                    IsSuccess = true,
                    Content = "Test analysis result",
                    Metadata = new Dictionary<string, object>
                    {
                        ["Confidence"] = 0.95,
                        ["ProcessingTime"] = TimeSpan.FromSeconds(2),
                        ["TokensUsed"] = 500,
                        ["EstimatedCost"] = 0.01m
                    }
                });

            mock.Setup(x => x.GetConversationHistoryAsync(It.IsAny<string>(), It.IsAny<OperationModel>()))
                .ReturnsAsync(TestDataFactory.CreateConversationHistory());

            mock.Setup(x => x.GetAvailableFunctions())
                .Returns(new[]
                {
                    new AIFunctionDefinition
                    {
                        Name = "DumpObject",
                        Description = "Analyzes a specific object in memory",
                        Parameters = new Dictionary<string, AIFunctionParameter>
                        {
                            ["objectAddress"] = new AIFunctionParameter
                            {
                                Type = "ulong",
                                Description = "Memory address of the object",
                                Required = true
                            }
                        }
                    }
                });

            return mock;
        }

        /// <summary>
        /// Creates a mock debugger operation with default behavior
        /// </summary>
        public static Mock<IDebuggerOperation> CreateDebuggerOperation(string operationName = "TestOperation")
        {
            var mock = new Mock<IDebuggerOperation>();

            mock.Setup(x => x.Name).Returns(operationName);

            mock.Setup(x => x.Execute(It.IsAny<OperationModel>(), It.IsAny<CancellationToken>(), It.IsAny<object>()))
                .ReturnsAsync(new List<object>
                {
                    new { Address = 0x12345678UL, Type = "System.String", Value = "Test" },
                    new { Address = 0x87654321UL, Type = "System.Int32", Value = 42 }
                });

            return mock;
        }

        /// <summary>
        /// Creates a mock AI-enabled operation
        /// </summary>
        public static Mock<IAIEnabledOperation> CreateAIEnabledOperation(string operationName = "TestAIOperation")
        {
            var mock = new Mock<IAIEnabledOperation>();

            mock.Setup(x => x.Name).Returns(operationName);

            mock.Setup(x => x.Execute(It.IsAny<OperationModel>(), It.IsAny<CancellationToken>(), It.IsAny<object>()))
                .ReturnsAsync(new List<object>
                {
                    new { Address = 0x12345678UL, Type = "System.String", Value = "Test" }
                });

            mock.Setup(x => x.AskAi(
                It.IsAny<OperationModel>(),
                It.IsAny<Collection<object>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<object>()))
                .ReturnsAsync("AI analysis of the operation results");

            return mock;
        }

        /// <summary>
        /// Creates a mock configuration service with default settings
        /// </summary>
        public static ApplicationConfiguration CreateTestApplicationConfiguration()
        {
            return new ApplicationConfiguration
            {
                General = new GeneralSettings
                {
                    SymbolCachePath = @"C:\Symbols",
                    DefaultTimeoutMs = 30000
                },
                AI = TestDataFactory.CreateTestAIConfiguration(),
                Appearance = new AppearanceSettings
                {
                    Theme = ThemeType.Dark,
                    AccentColor = "#FF0078D4",
                    FontSize = FontSizeType.Small
                }
            };
        }

        /// <summary>
        /// Creates a mock IOptions<ApplicationConfiguration> for testing
        /// </summary>
        public static Mock<IOptions<ApplicationConfiguration>> CreateConfigurationOptions()
        {
            var config = CreateTestApplicationConfiguration();
            var mock = new Mock<IOptions<ApplicationConfiguration>>();
            mock.Setup(x => x.Value).Returns(config);
            return mock;
        }

        /// <summary>
        /// Creates a mock logger with default behavior
        /// </summary>
        public static Mock<ILogger<T>> CreateLogger<T>()
        {
            var mock = new Mock<ILogger<T>>();

            mock.Setup(x => x.IsEnabled(It.IsAny<LogLevel>()))
                .Returns(true);

            // Allow logging without throwing exceptions
            mock.Setup(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()));

            return mock;
        }

        /// <summary>
        /// Creates a mock IOptions<T> with the provided value
        /// </summary>
        public static Mock<IOptions<T>> CreateOptions<T>(T value) where T : class
        {
            var mock = new Mock<IOptions<T>>();
            mock.Setup(x => x.Value).Returns(value);
            return mock;
        }

        /// <summary>
        /// Creates a mock cancellation token source
        /// </summary>
        public static Mock<CancellationTokenSource> CreateCancellationTokenSource()
        {
            var mock = new Mock<CancellationTokenSource>();
            mock.Setup(x => x.Token).Returns(CancellationToken.None);
            return mock;
        }

        /// <summary>
        /// Factory for creating scenario-specific mock combinations
        /// </summary>
        public static class Scenarios
        {
            /// <summary>
            /// Creates mocks for a basic AI operation scenario
            /// </summary>
            public static class BasicAIOperation
            {
                public static Mock<IAIServiceManager> AIServiceManager => CreateAIServiceManager();
                public static Mock<ILogger<object>> Logger => CreateLogger<object>();
                public static Mock<IDebuggerOperation> Operation => CreateDebuggerOperation("DumpHeap");

                public static (Mock<IAIServiceManager> aiService, Mock<ILogger<T>> logger, Mock<IDebuggerOperation> operation)
                    CreateMocks<T>()
                {
                    return (CreateAIServiceManager(), CreateLogger<T>(), CreateDebuggerOperation());
                }
            }

            /// <summary>
            /// Creates mocks for AI provider testing scenarios
            /// </summary>
            public static class AIProviderTesting
            {
                public static Mock<IAIProvider> SuccessfulProvider(AIProviderType providerType = AIProviderType.OpenAI)
                {
                    var mock = CreateAIProvider(providerType);
                    mock.Setup(x => x.IsConfigured).Returns(true);
                    return mock;
                }

                public static Mock<IAIProvider> FailedProvider(AIProviderType providerType = AIProviderType.OpenAI)
                {
                    var mock = CreateAIProvider(providerType);
                    mock.Setup(x => x.IsConfigured).Returns(false);
                    mock.Setup(x => x.CompleteAsync(It.IsAny<AIRequest>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(TestDataFactory.CreateFailedAIResponse("Provider not configured"));
                    return mock;
                }

                public static Mock<IAIProvider> SlowProvider(AIProviderType providerType = AIProviderType.OpenAI)
                {
                    var mock = CreateAIProvider(providerType);
                    mock.Setup(x => x.CompleteAsync(It.IsAny<AIRequest>(), It.IsAny<CancellationToken>()))
                        .Returns(async (AIRequest request, CancellationToken ct) =>
                        {
                            await Task.Delay(5000, ct); // Simulate slow response
                            return TestDataFactory.CreateSuccessfulAIResponse();
                        });
                    return mock;
                }
            }

            /// <summary>
            /// Creates mocks for configuration testing scenarios
            /// </summary>
            public static class ConfigurationTesting
            {
                public static Mock<IOptions<AIConfiguration>> ValidAIConfig()
                {
                    return CreateOptions(TestDataFactory.CreateTestAIConfiguration());
                }

                public static Mock<IOptions<AIConfiguration>> InvalidAIConfig()
                {
                    var config = new AIConfiguration
                    {
                        DefaultProvider = AIProviderType.OpenAI,
                        Providers = new ProviderConfigurations
                        {
                            OpenAI = new OpenAIConfiguration
                            {
                                ApiKey = "", // Invalid - empty key
                                IsEnabled = true
                            }
                        }
                    };
                    return CreateOptions(config);
                }

                public static Mock<IOptions<AIConfiguration>> DisabledAIConfig()
                {
                    var config = new AIConfiguration
                    {
                        DefaultProvider = AIProviderType.OpenAI,
                        Providers = new ProviderConfigurations
                        {
                            OpenAI = new OpenAIConfiguration
                            {
                                ApiKey = "sk-test-key",
                                IsEnabled = false // Disabled
                            }
                        }
                    };
                    return CreateOptions(config);
                }
            }

            /// <summary>
            /// Creates mocks for error handling scenarios
            /// </summary>
            public static class ErrorHandling
            {
                public static Mock<IAIServiceManager> ThrowingAIServiceManager()
                {
                    var mock = new Mock<IAIServiceManager>();
                    mock.Setup(x => x.AnalyzeDumpAsync(It.IsAny<AIRequest>(), It.IsAny<CancellationToken>()))
                        .ThrowsAsync(new InvalidOperationException("AI service unavailable"));
                    return mock;
                }

                public static Mock<IDebuggerOperation> ThrowingOperation()
                {
                    var mock = new Mock<IDebuggerOperation>();
                    mock.Setup(x => x.Execute(It.IsAny<OperationModel>(), It.IsAny<CancellationToken>(), It.IsAny<object>()))
                        .ThrowsAsync(new InvalidOperationException("Operation failed"));
                    return mock;
                }

                public static Mock<IAIProvider> TimeoutProvider()
                {
                    var mock = new Mock<IAIProvider>();
                    mock.Setup(x => x.CompleteAsync(It.IsAny<AIRequest>(), It.IsAny<CancellationToken>()))
                        .ThrowsAsync(new TimeoutException("Request timed out"));
                    return mock;
                }
            }
        }
    }

    /// <summary>
    /// Extension methods for enhanced mock setup
    /// </summary>
    public static class MockExtensions
    {
        /// <summary>
        /// Sets up a mock to return specific values in sequence
        /// </summary>
        public static Mock<T> SetupSequence<T, TResult>(this Mock<T> mock,
            System.Linq.Expressions.Expression<Func<T, TResult>> expression,
            params TResult[] results) where T : class
        {
            var setup = mock.SetupSequence(expression);
            foreach (var result in results)
            {
                setup.Returns(result);
            }
            return mock;
        }

        /// <summary>
        /// Sets up a mock to throw specific exceptions in sequence
        /// </summary>
        public static Mock<T> SetupSequenceThrows<T, TResult>(this Mock<T> mock,
            System.Linq.Expressions.Expression<Func<T, TResult>> expression,
            params Exception[] exceptions) where T : class
        {
            var setup = mock.SetupSequence(expression);
            foreach (var exception in exceptions)
            {
                setup.Throws(exception);
            }
            return mock;
        }

        /// <summary>
        /// Sets up a mock with conditional behavior
        /// </summary>
        public static Mock<T> SetupConditional<T>(this Mock<T> mock,
            System.Linq.Expressions.Expression<Func<T, bool>> expression,
            bool condition,
            Action<Mock<T>> setupAction) where T : class
        {
            if (condition)
            {
                setupAction(mock);
            }
            return mock;
        }

        /// <summary>
        /// Verifies that a method was called with specific parameters
        /// </summary>
        public static void VerifyCallWith<T>(this Mock<T> mock,
            System.Linq.Expressions.Expression<Action<T>> expression,
            Times times) where T : class
        {
            mock.Verify(expression, times);
        }

        /// <summary>
        /// Verifies that an async method was called with specific parameters
        /// </summary>
        public static void VerifyCallWithAsync<T>(this Mock<T> mock,
            System.Linq.Expressions.Expression<Func<T, Task>> expression,
            Times times) where T : class
        {
            mock.Verify(expression, times);
        }
    }
}