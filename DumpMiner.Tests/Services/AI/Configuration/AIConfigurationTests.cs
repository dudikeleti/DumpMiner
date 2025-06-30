using System.ComponentModel.DataAnnotations;
using DumpMiner.Services.AI.Configuration;
using FluentAssertions;
using Xunit;

namespace DumpMiner.Tests.Services.AI.Configuration;

/// <summary>
/// Unit tests for AI configuration models
/// </summary>
public class AIConfigurationTests
{
    [Fact]
    public void AIConfiguration_DefaultValues_ShouldBeValid()
    {
        // Arrange & Act
        var config = new AIConfiguration();

        // Assert
        config.DefaultProvider.Should().Be(AIProviderType.OpenAI);
        config.MaxTokens.Should().Be(4000);
        config.TimeoutSeconds.Should().Be(60);
        config.EnableCaching.Should().BeTrue();
        config.CacheExpirationMinutes.Should().Be(60);
        config.MaxConversationHistory.Should().Be(20);
        config.Providers.Should().NotBeNull();
    }

    [Fact]
    public void AIConfiguration_SectionName_ShouldBeCorrect()
    {
        // Act & Assert
        AIConfiguration.SectionName.Should().Be("AI");
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(32000, true)]
    [InlineData(99, false)]
    [InlineData(32001, false)]
    public void AIConfiguration_MaxTokens_ValidationShouldWork(int maxTokens, bool expectedValid)
    {
        // Arrange
        var config = new AIConfiguration { MaxTokens = maxTokens };

        // Act
        var validationResults = ValidateModel(config);

        // Assert
        var hasMaxTokensError = validationResults.Any(v => v.MemberNames.Contains(nameof(AIConfiguration.MaxTokens)));
        hasMaxTokensError.Should().Be(!expectedValid);
    }

    [Theory]
    [InlineData(5, true)]
    [InlineData(300, true)]
    [InlineData(4, false)]
    [InlineData(301, false)]
    public void AIConfiguration_TimeoutSeconds_ValidationShouldWork(int timeoutSeconds, bool expectedValid)
    {
        // Arrange
        var config = new AIConfiguration { TimeoutSeconds = timeoutSeconds };

        // Act
        var validationResults = ValidateModel(config);

        // Assert
        var hasTimeoutError = validationResults.Any(v => v.MemberNames.Contains(nameof(AIConfiguration.TimeoutSeconds)));
        hasTimeoutError.Should().Be(!expectedValid);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(1440, true)]
    [InlineData(0, false)]
    [InlineData(1441, false)]
    public void AIConfiguration_CacheExpirationMinutes_ValidationShouldWork(int cacheExpirationMinutes, bool expectedValid)
    {
        // Arrange
        var config = new AIConfiguration { CacheExpirationMinutes = cacheExpirationMinutes };

        // Act
        var validationResults = ValidateModel(config);

        // Assert
        var hasCacheExpirationError = validationResults.Any(v => v.MemberNames.Contains(nameof(AIConfiguration.CacheExpirationMinutes)));
        hasCacheExpirationError.Should().Be(!expectedValid);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(0, false)]
    [InlineData(101, false)]
    public void AIConfiguration_MaxConversationHistory_ValidationShouldWork(int maxConversationHistory, bool expectedValid)
    {
        // Arrange
        var config = new AIConfiguration { MaxConversationHistory = maxConversationHistory };

        // Act
        var validationResults = ValidateModel(config);

        // Assert
        var hasMaxHistoryError = validationResults.Any(v => v.MemberNames.Contains(nameof(AIConfiguration.MaxConversationHistory)));
        hasMaxHistoryError.Should().Be(!expectedValid);
    }

    [Fact]
    public void OpenAIConfiguration_DefaultValues_ShouldBeValid()
    {
        // Arrange & Act
        var config = new OpenAIConfiguration();

        // Assert
        config.ApiKey.Should().Be(string.Empty);
        config.Model.Should().Be("gpt-4");
        config.BaseUrl.Should().Be("https://api.openai.com/v1");
        config.Temperature.Should().Be(0.7);
        config.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void OpenAIConfiguration_WithApiKey_ShouldPassValidation()
    {
        // Arrange
        var config = new OpenAIConfiguration { ApiKey = "sk-test-key" };

        // Act
        var validationResults = ValidateModel(config);

        // Assert
        validationResults.Should().BeEmpty();
    }

    [Fact]
    public void OpenAIConfiguration_WithoutApiKey_ShouldFailValidation()
    {
        // Arrange
        var config = new OpenAIConfiguration { ApiKey = string.Empty };

        // Act
        var validationResults = ValidateModel(config);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(v => v.MemberNames.Contains(nameof(OpenAIConfiguration.ApiKey)));
    }

    [Fact]
    public void AnthropicConfiguration_DefaultValues_ShouldBeValid()
    {
        // Arrange & Act
        var config = new AnthropicConfiguration();

        // Assert
        config.ApiKey.Should().Be(string.Empty);
        config.Model.Should().Be("claude-3-sonnet-20240229");
        config.BaseUrl.Should().Be("https://api.anthropic.com");
        config.Temperature.Should().Be(0.7);
        config.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void GoogleConfiguration_DefaultValues_ShouldBeValid()
    {
        // Arrange & Act
        var config = new GoogleConfiguration();

        // Assert
        config.ApiKey.Should().Be(string.Empty);
        config.Model.Should().Be("gemini-pro");
        config.BaseUrl.Should().Be("https://generativelanguage.googleapis.com");
        config.Temperature.Should().Be(0.7);
        config.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void ProviderConfigurations_DefaultValues_ShouldBeValid()
    {
        // Arrange & Act
        var config = new ProviderConfigurations();

        // Assert
        config.OpenAI.Should().NotBeNull();
        config.Anthropic.Should().NotBeNull();
        config.Google.Should().NotBeNull();
    }

    [Theory]
    [InlineData(AIProviderType.OpenAI)]
    [InlineData(AIProviderType.Anthropic)]
    [InlineData(AIProviderType.Google)]
    public void AIProviderType_AllValues_ShouldBeValid(AIProviderType providerType)
    {
        // Act & Assert
        Enum.IsDefined(typeof(AIProviderType), providerType).Should().BeTrue();
    }

    [Fact]
    public void AIConfiguration_CompleteConfiguration_ShouldPassValidation()
    {
        // Arrange
        var config = new AIConfiguration
        {
            DefaultProvider = AIProviderType.OpenAI,
            MaxTokens = 2000,
            TimeoutSeconds = 30,
            EnableCaching = false,
            CacheExpirationMinutes = 30,
            MaxConversationHistory = 10,
            Providers = new ProviderConfigurations
            {
                OpenAI = new OpenAIConfiguration
                {
                    ApiKey = "sk-test-key",
                    Model = "gpt-4-turbo",
                    Temperature = 0.5
                },
                Anthropic = new AnthropicConfiguration
                {
                    ApiKey = "anthropic-test-key",
                    Model = "claude-3-opus-20240229",
                    Temperature = 0.3
                },
                Google = new GoogleConfiguration
                {
                    ApiKey = "google-test-key",
                    Model = "gemini-pro-vision",
                    Temperature = 0.8
                }
            }
        };

        // Act
        var validationResults = ValidateModel(config);

        // Assert
        validationResults.Should().BeEmpty();
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model);
        Validator.TryValidateObject(model, validationContext, validationResults, true);
        return validationResults;
    }
} 