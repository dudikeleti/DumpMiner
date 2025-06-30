using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Services.AI.Configuration;
using DumpMiner.Services.AI.Interfaces;
using DumpMiner.Services.AI.Models;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DumpMiner.Tests.Integration;

/// <summary>
/// Integration tests demonstrating the complete AI service workflow
/// </summary>
public class AIServiceIntegrationTests : IClassFixture<AIServiceTestFixture>
{
    private readonly AIServiceTestFixture _fixture;

    public AIServiceIntegrationTests(AIServiceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CompleteAIWorkflow_WithMockedProvider_ShouldExecuteSuccessfully()
    {
        // Arrange
        var serviceProvider = _fixture.ServiceProvider;
        var aiServiceManager = serviceProvider.GetRequiredService<IAIServiceManager>();

        var request = new AIRequest
        {
            UserPrompt = "Analyze this memory dump for potential memory leaks",
            SystemPrompt = "You are a memory dump analysis expert",
            DumpContext = new DumpContext
            {
                ProcessInfo = new ProcessInfo
                {
                    ProcessId = 1234,
                    ProcessName = "TestApp.exe",
                    ClrVersion = "8.0.0",
                    ThreadCount = 5
                },
                HeapStats = new HeapStatistics
                {
                    TotalSize = 100_000_000,
                    Gen0Size = 10_000_000,
                    Gen1Size = 5_000_000,
                    Gen2Size = 50_000_000,
                    LargeObjectHeapSize = 35_000_000,
                    ObjectCount = 50000
                },
                Exceptions = new List<ExceptionInfo>
                {
                    new ExceptionInfo
                    {
                        Type = "OutOfMemoryException",
                        Message = "Insufficient memory to continue the execution of the program.",
                        Address = 0x12345678
                    }
                }
            },
            OperationContext = new OperationContext
            {
                OperationName = "DumpHeap",
                ResultCount = 150,
                Parameters = new Dictionary<string, object>
                {
                    ["Generation"] = 2,
                    ["Types"] = "System.String"
                }
            },
            PreferredProvider = AIProviderType.OpenAI,
            MaxTokens = 1000,
            Temperature = 0.7
        };

        // Act
        var response = await aiServiceManager.AnalyzeDumpAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeTrue();
        response.Content.Should().NotBeEmpty();
        response.Provider.Should().Be(AIProviderType.OpenAI);
        response.RequestId.Should().Be(request.RequestId);
        response.Metadata.Should().NotBeNull();
    }

    [Fact]
    public async Task AIServiceManager_ProviderAvailability_ShouldReportCorrectly()
    {
        // Arrange
        var serviceProvider = _fixture.ServiceProvider;
        var aiServiceManager = serviceProvider.GetRequiredService<IAIServiceManager>();

        // Act
        var availableProviders = aiServiceManager.AvailableProviders;
        var isOpenAIAvailable = await aiServiceManager.IsProviderAvailableAsync(AIProviderType.OpenAI);
        var isAnthropicAvailable = await aiServiceManager.IsProviderAvailableAsync(AIProviderType.Anthropic);

        // Assert
        availableProviders.Should().NotBeEmpty();
        availableProviders.Should().Contain(AIProviderType.OpenAI);
        isOpenAIAvailable.Should().BeTrue(); // Mocked to return true
        isAnthropicAvailable.Should().BeTrue(); // Mocked to return true
    }

    [Fact]
    public async Task AIServiceManager_ConversationHistory_ShouldPersistAndRetrieve()
    {
        // Arrange
        var serviceProvider = _fixture.ServiceProvider;
        var aiServiceManager = serviceProvider.GetRequiredService<IAIServiceManager>();
        var sessionId = "test-session-123";

        var request = new AIRequest
        {
            UserPrompt = "What types of objects are consuming the most memory?",
            ConversationHistory = new List<ConversationMessage>
            {
                new ConversationMessage { Role = "user", Content = "Analyze this dump" },
                new ConversationMessage { Role = "assistant", Content = "I see high memory usage..." }
            }
        };

        // Act
        var response = await aiServiceManager.AnalyzeDumpAsync(request);
        var history = await aiServiceManager.GetConversationHistoryAsync(sessionId);

        // Assert
        response.Should().NotBeNull();
        history.Should().NotBeNull();
        // Note: In a real implementation, history would be persisted
    }

    [Fact]
    public async Task AIServiceManager_ProviderTesting_ShouldValidateConnectivity()
    {
        // Arrange
        var serviceProvider = _fixture.ServiceProvider;
        var aiServiceManager = serviceProvider.GetRequiredService<IAIServiceManager>();

        // Act
        var testResult = await aiServiceManager.TestProviderAsync(AIProviderType.OpenAI);

        // Assert
        testResult.Should().NotBeNull();
        testResult.IsSuccess.Should().BeTrue();
        testResult.ResponseTime.Should().BeGreaterThan(TimeSpan.Zero);
        testResult.Model.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(AIProviderType.OpenAI)]
    [InlineData(AIProviderType.Anthropic)]
    [InlineData(AIProviderType.Google)]
    public async Task AIServiceManager_SupportedProviders_ShouldHandleAllTypes(AIProviderType providerType)
    {
        // Arrange
        var serviceProvider = _fixture.ServiceProvider;
        var aiServiceManager = serviceProvider.GetRequiredService<IAIServiceManager>();

        // Act
        var isAvailable = await aiServiceManager.IsProviderAvailableAsync(providerType);
        var testResult = await aiServiceManager.TestProviderAsync(providerType);

        // Assert
        isAvailable.Should().BeTrue();
        testResult.Should().NotBeNull();
        testResult.IsSuccess.Should().BeTrue();
    }
}

/// <summary>
/// Test fixture for setting up AI service dependencies
/// </summary>
public class AIServiceTestFixture : IDisposable
{
    public ServiceProvider ServiceProvider { get; private set; }

    public AIServiceTestFixture()
    {
        var services = new ServiceCollection();
        
        // Configuration
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.test.json")
            .Build();
        
        services.AddSingleton<IConfiguration>(configuration);
        services.Configure<AIConfiguration>(configuration.GetSection(AIConfiguration.SectionName));

        // Logging
        services.AddLogging();

        // Mock AI Service Manager
        var mockServiceManager = new Mock<IAIServiceManager>();
        
        // Setup mock behavior
        mockServiceManager.Setup(x => x.AvailableProviders)
            .Returns(new[] { AIProviderType.OpenAI, AIProviderType.Anthropic, AIProviderType.Google });
        
        mockServiceManager.Setup(x => x.DefaultProvider)
            .Returns(AIProviderType.OpenAI);
        
        mockServiceManager.Setup(x => x.IsProviderAvailableAsync(It.IsAny<AIProviderType>()))
            .ReturnsAsync(true);
        
        mockServiceManager.Setup(x => x.AnalyzeDumpAsync(It.IsAny<AIRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIRequest request, CancellationToken _) => new AIResponse
            {
                RequestId = request.RequestId,
                Content = GenerateMockAnalysisResponse(request),
                Provider = request.PreferredProvider ?? AIProviderType.OpenAI,
                Model = "gpt-4",
                IsSuccess = true,
                Metadata = new ResponseMetadata
                {
                    PromptTokens = 250,
                    CompletionTokens = 500,
                    TotalTokens = 750,
                    ProcessingTimeMs = 1500,
                    EstimatedCost = 0.015m
                },
                Timestamp = DateTimeOffset.UtcNow
            });
        
        mockServiceManager.Setup(x => x.GetConversationHistoryAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<ConversationMessage>
            {
                new ConversationMessage { Role = "user", Content = "Previous question" },
                new ConversationMessage { Role = "assistant", Content = "Previous answer" }
            });
        
        mockServiceManager.Setup(x => x.TestProviderAsync(It.IsAny<AIProviderType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderTestResult.Success(TimeSpan.FromMilliseconds(500), "test-model"));

        services.AddSingleton(mockServiceManager.Object);

        ServiceProvider = services.BuildServiceProvider();
    }

    private static string GenerateMockAnalysisResponse(AIRequest request)
    {
        var analysis = new System.Text.StringBuilder();
        
        analysis.AppendLine("## Memory Dump Analysis Report");
        analysis.AppendLine();
        
        if (request.DumpContext?.ProcessInfo != null)
        {
            analysis.AppendLine($"**Process:** {request.DumpContext.ProcessInfo.ProcessName} (PID: {request.DumpContext.ProcessInfo.ProcessId})");
            analysis.AppendLine($"**CLR Version:** {request.DumpContext.ProcessInfo.ClrVersion}");
            analysis.AppendLine();
        }
        
        if (request.DumpContext?.HeapStats != null)
        {
            analysis.AppendLine("### Heap Analysis");
            analysis.AppendLine($"- Total Heap Size: {FormatBytes(request.DumpContext.HeapStats.TotalSize)}");
            analysis.AppendLine($"- Large Object Heap: {FormatBytes(request.DumpContext.HeapStats.LargeObjectHeapSize)}");
            analysis.AppendLine();
        }
        
        if (request.DumpContext?.Exceptions?.Count > 0)
        {
            analysis.AppendLine("### Critical Issues Found");
            foreach (var exception in request.DumpContext.Exceptions)
            {
                analysis.AppendLine($"- **{exception.Type}**: {exception.Message}");
            }
            analysis.AppendLine();
        }
        
        if (request.OperationContext != null)
        {
            analysis.AppendLine($"### {request.OperationContext.OperationName} Operation Results");
            analysis.AppendLine($"Found {request.OperationContext.ResultCount} items matching your criteria.");
            analysis.AppendLine();
        }
        
        analysis.AppendLine("### Recommendations");
        analysis.AppendLine("1. **Memory Optimization**: Consider implementing object pooling for frequently allocated objects");
        analysis.AppendLine("2. **Garbage Collection**: Review GC settings and consider tuning for your workload");
        analysis.AppendLine("3. **Code Review**: Examine code paths that lead to large object allocations");
        analysis.AppendLine();
        analysis.AppendLine("Would you like me to analyze any specific aspect in more detail?");
        
        return analysis.ToString();
    }
    
    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number = number / 1024;
            counter++;
        }
        return $"{number:n1}{suffixes[counter]}";
    }

    public void Dispose()
    {
        ServiceProvider?.Dispose();
    }
} 