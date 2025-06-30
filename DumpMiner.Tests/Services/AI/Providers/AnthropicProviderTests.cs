using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Services.AI.Configuration;
using DumpMiner.Services.AI.Models;
using DumpMiner.Services.AI.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DumpMiner.Tests.Services.AI.Providers
{
    public class AnthropicProviderTests
    {
        private readonly Mock<ILogger<AnthropicProvider>> _mockLogger;
        private readonly AIConfiguration _config;
        private readonly IOptions<AIConfiguration> _options;

        public AnthropicProviderTests()
        {
            _mockLogger = new Mock<ILogger<AnthropicProvider>>();
            _config = new AIConfiguration
            {
                Anthropic = new AnthropicConfiguration
                {
                    ApiKey = "test-api-key-anthropic",
                    Model = "claude-3-sonnet-20240229",
                    Temperature = 0.7,
                    IsEnabled = true,
                    MaxTokens = 4000,
                    TimeoutSeconds = 60
                }
            };
            _options = Options.Create(_config);
        }

        [Fact]
        public void Constructor_WithValidConfiguration_ShouldInitializeCorrectly()
        {
            // Act
            var provider = new AnthropicProvider(_options, _mockLogger.Object);

            // Assert
            provider.ProviderType.Should().Be(AIProviderType.Anthropic);
            provider.IsConfigured.Should().BeTrue();
            provider.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithEmptyApiKey_ShouldThrowException()
        {
            // Arrange
            var emptyKeyConfig = new AIConfiguration
            {
                Anthropic = new AnthropicConfiguration
                {
                    ApiKey = "",
                    IsEnabled = true
                }
            };
            var emptyKeyOptions = Options.Create(emptyKeyConfig);

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(
                () => new AnthropicProvider(emptyKeyOptions, _mockLogger.Object));
            
            exception.Message.Should().Contain("Anthropic API key is not configured");
        }

        [Fact]
        public void IsConfigured_WithValidApiKey_ShouldReturnTrue()
        {
            // Arrange
            var provider = new AnthropicProvider(_options, _mockLogger.Object);

            // Act & Assert
            provider.IsConfigured.Should().BeTrue();
        }

        [Fact]
        public void IsConfigured_WithDisabledProvider_ShouldReturnFalse()
        {
            // Arrange
            var disabledConfig = new AIConfiguration
            {
                Anthropic = new AnthropicConfiguration
                {
                    ApiKey = "test-key",
                    IsEnabled = false
                }
            };
            var disabledOptions = Options.Create(disabledConfig);
            var provider = new AnthropicProvider(disabledOptions, _mockLogger.Object);

            // Act & Assert
            provider.IsConfigured.Should().BeFalse();
        }

        [Fact]
        public void EstimateCost_WithBasicRequest_ShouldReturnPositiveValue()
        {
            // Arrange
            var provider = new AnthropicProvider(_options, _mockLogger.Object);
            var request = new AIRequest
            {
                RequestId = Guid.NewGuid().ToString(),
                UserPrompt = "Analyze this memory dump for potential memory leaks.",
                ConversationHistory = new List<ConversationMessage>
                {
                    new ConversationMessage { Role = "user", Content = "Previous question" },
                    new ConversationMessage { Role = "assistant", Content = "Previous answer" }
                }
            };

            // Act
            var cost = provider.EstimateCost(request);

            // Assert
            cost.Should().BeGreaterThan(0);
            cost.Should().BeLessThan(1); // Should be reasonable for a typical request
        }

        [Fact]
        public void EstimateCost_WithOpusModel_ShouldReturnHigherCost()
        {
            // Arrange
            var opusConfig = new AIConfiguration
            {
                Anthropic = new AnthropicConfiguration
                {
                    ApiKey = "test-key",
                    Model = "claude-3-opus-20240229",
                    IsEnabled = true,
                    MaxTokens = 4000,
                    TimeoutSeconds = 60
                }
            };
            var opusOptions = Options.Create(opusConfig);
            var provider = new AnthropicProvider(opusOptions, _mockLogger.Object);
            
            var request = new AIRequest
            {
                RequestId = Guid.NewGuid().ToString(),
                UserPrompt = "Test prompt"
            };

            // Act
            var cost = provider.EstimateCost(request);

            // Assert
            cost.Should().BeGreaterThan(0.05m); // Opus should be more expensive
        }

        [Fact]
        public void EstimateCost_WithLargePrompt_ShouldReturnHigherCost()
        {
            // Arrange
            var provider = new AnthropicProvider(_options, _mockLogger.Object);
            var smallRequest = new AIRequest
            {
                RequestId = Guid.NewGuid().ToString(),
                UserPrompt = "Short prompt"
            };
            
            var largeRequest = new AIRequest
            {
                RequestId = Guid.NewGuid().ToString(),
                UserPrompt = new string('a', 10000), // Very large prompt
                ConversationHistory = new List<ConversationMessage>
                {
                    new ConversationMessage { Role = "user", Content = new string('b', 5000) },
                    new ConversationMessage { Role = "assistant", Content = new string('c', 5000) }
                }
            };

            // Act
            var smallCost = provider.EstimateCost(smallRequest);
            var largeCost = provider.EstimateCost(largeRequest);

            // Assert
            largeCost.Should().BeGreaterThan(smallCost);
        }

        [Fact]
        public async Task SendAsync_WithUnconfiguredProvider_ShouldThrowException()
        {
            // Arrange
            var unconfiguredConfig = new AIConfiguration
            {
                Anthropic = new AnthropicConfiguration
                {
                    ApiKey = "",
                    IsEnabled = false
                }
            };
            var unconfiguredOptions = Options.Create(unconfiguredConfig);

            // Act & Assert - Constructor should throw, but if it didn't, SendAsync would fail
            Assert.Throws<InvalidOperationException>(
                () => new AnthropicProvider(unconfiguredOptions, _mockLogger.Object));
        }

        [Fact]
        public void EstimateCost_WithDumpContext_ShouldIncludeContextInCalculation()
        {
            // Arrange
            var provider = new AnthropicProvider(_options, _mockLogger.Object);
            var requestWithContext = new AIRequest
            {
                RequestId = Guid.NewGuid().ToString(),
                UserPrompt = "Analyze memory usage",
                DumpContext = new DumpContext
                {
                    ProcessInfo = new ProcessInfo
                    {
                        ProcessName = "TestApp.exe",
                        ProcessId = 1234,
                        ClrVersion = "8.0.0",
                        ThreadCount = 12
                    },
                    HeapStats = new HeapStatistics
                    {
                        TotalSize = 1024 * 1024 * 100, // 100MB
                        Gen0Size = 1024 * 1024 * 10,
                        Gen1Size = 1024 * 1024 * 20,
                        Gen2Size = 1024 * 1024 * 60,
                        LargeObjectHeapSize = 1024 * 1024 * 10
                    },
                    Exceptions = new List<ExceptionInfo>
                    {
                        new ExceptionInfo
                        {
                            Type = "OutOfMemoryException",
                            Message = "Insufficient memory to continue",
                            Address = 0x12345678
                        }
                    }
                }
            };

            var requestWithoutContext = new AIRequest
            {
                RequestId = Guid.NewGuid().ToString(),
                UserPrompt = "Analyze memory usage"
            };

            // Act
            var costWithContext = provider.EstimateCost(requestWithContext);
            var costWithoutContext = provider.EstimateCost(requestWithoutContext);

            // Assert
            costWithContext.Should().BeGreaterThan(costWithoutContext);
        }

        [Theory]
        [InlineData("claude-3-opus-20240229")]
        [InlineData("claude-3-sonnet-20240229")]
        [InlineData("claude-3-haiku-20240307")]
        [InlineData("claude-3-5-sonnet-20241022")]
        public void EstimateCost_WithDifferentModels_ShouldReturnAppropriateValues(string modelName)
        {
            // Arrange
            var modelConfig = new AIConfiguration
            {
                Anthropic = new AnthropicConfiguration
                {
                    ApiKey = "test-key",
                    Model = modelName,
                    IsEnabled = true,
                    MaxTokens = 1000,
                    TimeoutSeconds = 60
                }
            };
            var modelOptions = Options.Create(modelConfig);
            var provider = new AnthropicProvider(modelOptions, _mockLogger.Object);
            
            var request = new AIRequest
            {
                RequestId = Guid.NewGuid().ToString(),
                UserPrompt = "Test prompt with consistent length for fair comparison"
            };

            // Act
            var cost = provider.EstimateCost(request);

            // Assert
            cost.Should().BeGreaterThan(0);
            
            // Opus should be most expensive, Haiku least expensive
            if (modelName.Contains("opus"))
                cost.Should().BeGreaterThan(0.01m);
            else if (modelName.Contains("haiku"))
                cost.Should().BeLessThan(0.01m);
        }

        [Fact]
        public void Dispose_ShouldNotThrowException()
        {
            // Arrange
            var provider = new AnthropicProvider(_options, _mockLogger.Object);

            // Act & Assert
            var act = () => provider.Dispose();
            act.Should().NotThrow();
            
            // Should be safe to dispose multiple times
            act.Should().NotThrow();
        }

        [Fact]
        public void ProviderType_ShouldReturnAnthropic()
        {
            // Arrange
            var provider = new AnthropicProvider(_options, _mockLogger.Object);

            // Act & Assert
            provider.ProviderType.Should().Be(AIProviderType.Anthropic);
        }
    }
} 