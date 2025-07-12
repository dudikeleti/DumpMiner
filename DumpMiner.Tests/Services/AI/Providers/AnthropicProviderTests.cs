using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Models;
using DumpMiner.Services.AI.Configuration;
using DumpMiner.Services.AI.Models;
using DumpMiner.Services.AI.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DumpMiner.Tests.Services.AI.Providers
{
    public class AnthropicProviderTests
    {
        private readonly Mock<ILogger<AnthropicProvider>> _mockLogger;
        private readonly AnthropicConfiguration _config;

        public AnthropicProviderTests()
        {
            _mockLogger = new Mock<ILogger<AnthropicProvider>>();
            _config = new AnthropicConfiguration
            {
                ApiKey = "test-api-key-anthropic",
                Model = "claude-sonnet-4",
                Temperature = 0.7,
                IsEnabled = true,
                MaxTokens = 4000,
                TimeoutSeconds = 60
            };
        }

        [Fact]
        public async Task Constructor_WithValidConfiguration_ShouldInitializeCorrectly()
        {
            // Arrange
            var provider = new AnthropicProvider(_mockLogger.Object);

            // Act
            await provider.InitializeAsync(_config, CancellationToken.None);

            // Assert
            provider.ProviderType.Should().Be(AIProviderType.Anthropic);
            provider.IsConfigured.Should().BeTrue();
            provider.Should().NotBeNull();
        }

        [Fact]
        public async Task Constructor_WithEmptyApiKey_ShouldThrowException()
        {
            // Arrange
            var emptyKeyConfig = new AnthropicConfiguration
            {
                ApiKey = "",
                IsEnabled = true
            };
            var provider = new AnthropicProvider(_mockLogger.Object);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await provider.InitializeAsync(emptyKeyConfig, CancellationToken.None));

            exception.Message.Should().Contain("Anthropic API key is required");
        }

        [Fact]
        public async Task IsConfigured_WithValidApiKey_ShouldReturnTrue()
        {
            // Arrange
            var provider = new AnthropicProvider(_mockLogger.Object);
            await provider.InitializeAsync(_config, CancellationToken.None);

            // Act & Assert
            provider.IsConfigured.Should().BeTrue();
        }

        [Fact]
        public void IsConfigured_WithDisabledProvider_ShouldReturnFalse()
        {
            // Arrange
            var provider = new AnthropicProvider(_mockLogger.Object);
            // Not initialized - should return false

            // Act & Assert
            provider.IsConfigured.Should().BeFalse();
        }

        [Fact]
        public async Task EstimateCost_WithBasicRequest_ShouldReturnPositiveValue()
        {
            // Arrange
            var provider = new AnthropicProvider(_mockLogger.Object);
            await provider.InitializeAsync(_config, CancellationToken.None);
            
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
        public async Task EstimateCost_WithOpusModel_ShouldReturnHigherCost()
        {
            // Arrange
            var opusConfig = new AnthropicConfiguration
            {
                ApiKey = "test-key",
                Model = "claude-opus-4",
                IsEnabled = true,
                MaxTokens = 4000,
                TimeoutSeconds = 60
            };
            var provider = new AnthropicProvider(_mockLogger.Object);
            await provider.InitializeAsync(opusConfig, CancellationToken.None);

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
        public async Task EstimateCost_WithLargePrompt_ShouldReturnHigherCost()
        {
            // Arrange
            var provider = new AnthropicProvider(_mockLogger.Object);
            await provider.InitializeAsync(_config, CancellationToken.None);
            
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
            smallCost.Should().NotBeNull();
            largeCost.Should().NotBeNull();
            largeCost.Should().BeGreaterThan(smallCost!.Value);
        }

        [Fact]
        public async Task SendAsync_WithUnconfiguredProvider_ShouldThrowException()
        {
            // Arrange
            var provider = new AnthropicProvider(_mockLogger.Object);
            // Not initialized

            var request = new AIRequest
            {
                RequestId = Guid.NewGuid().ToString(),
                UserPrompt = "Test prompt"
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await provider.CompleteAsync(request, CancellationToken.None));

            exception.Message.Should().Contain("Provider not initialized");
        }

        [Fact]
        public async Task EstimateCost_WithDumpContext_ShouldIncludeContextInCalculation()
        {
            // Arrange
            var provider = new AnthropicProvider(_mockLogger.Object);
            await provider.InitializeAsync(_config, CancellationToken.None);
            
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
            costWithContext.Should().NotBeNull();
            costWithoutContext.Should().NotBeNull();
            costWithContext.Should().BeGreaterThan(costWithoutContext!.Value);
        }

        [Theory]
        [InlineData("claude-sonnet-3.7")]
        [InlineData("claude-sonnet-4")]
        [InlineData("claude-opus-4")]
        [InlineData("claude-sonnet-3.5")]
        public async Task EstimateCost_WithDifferentModels_ShouldReturnAppropriateValues(string modelName)
        {
            // Arrange
            var modelConfig = new AnthropicConfiguration
            {
                ApiKey = "test-key",
                Model = modelName,
                IsEnabled = true,
                MaxTokens = 1000,
                TimeoutSeconds = 60
            };
            var provider = new AnthropicProvider(_mockLogger.Object);
            await provider.InitializeAsync(modelConfig, CancellationToken.None);

            var request = new AIRequest
            {
                RequestId = Guid.NewGuid().ToString(),
                UserPrompt = "Test prompt with consistent length for fair comparison"
            };

            // Act
            var cost = provider.EstimateCost(request);

            // Assert
            cost.Should().BeGreaterThan(0);

            // All models should have reasonable costs, with Opus being more expensive
            if (modelName.Contains("opus"))
                cost.Should().BeGreaterThan(0.01m);
            else
                cost.Should().BeGreaterThan(0.001m);
            cost.Should().BeLessThan(0.1m);
        }

        [Fact]
        public async Task Dispose_ShouldNotThrowException()
        {
            // Arrange
            var provider = new AnthropicProvider(_mockLogger.Object);
            await provider.InitializeAsync(_config, CancellationToken.None);

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
            var provider = new AnthropicProvider(_mockLogger.Object);

            // Act & Assert
            provider.ProviderType.Should().Be(AIProviderType.Anthropic);
        }
    }
}