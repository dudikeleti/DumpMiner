# 🤖 AI Integration Setup Guide

## Overview
DumpMiner now includes advanced AI integration for analyzing memory dumps using OpenAI GPT-4, Anthropic Claude, and Google Gemini. This guide will help you set up and use these features.

## ✅ Features Available
- **CLR Stack Analysis**: Get AI insights on call stacks and thread states
- **Source Code Review**: AI analysis of decompiled C# code for potential issues  
- **Assembly Code Analysis**: Expert insights on JIT-compiled assembly code
- **Multiple AI Providers**: OpenAI, Anthropic, Google (OpenAI is production-ready)
- **Intelligent Caching**: Avoid duplicate API calls and costs
- **Error Handling**: Graceful fallbacks and helpful error messages
- **Automated Investigation**: AI automatically calls related operations for deeper analysis

## 🔧 Setup Instructions

### Step 1: Get API Keys

#### OpenAI
1. Go to [OpenAI API](https://platform.openai.com/api-keys)
2. Create an account and add billing information
3. Generate a new API key
4. Copy the key (starts with `sk-`)
5. Paste it into the `ApiKey` field for OpenAI

#### Anthropic
1. Go to [Anthropic Console](https://console.anthropic.com/)
2. Create an account and set up billing
3. Generate an API key
4. Set `IsEnabled: true` for Anthropic provider

#### Google Gemini (Optional)  
1. Go to [Google AI Studio](https://makersuite.google.com/app/apikey)
2. Create an API key
3. Set `IsEnabled: true` for Google provider

### Step 2: Test the Integration
1. Open DumpMiner
2. Load a memory dump file
3. Run one of the operations
4. Click the **"Ask AI"** button
5. Wait for the AI analysis

## 💡 Usage Tips

### Best Practices
- **Start Small**: Test with smaller dumps first to understand API costs
- **Enable Caching**: Reduces duplicate API calls and saves money
- **Monitor Costs**: OpenAI charges per token - track your usage
- **API Limits**: Respect rate limits and quotas
- **Use Appropriate Models**: Choose models based on complexity needs

### Cost Management
- **OpenAI o3**: ~$0.005 per 1K input tokens + $0.015 per 1K output tokens
- **Anthropic Claude**: ~$3.00 per 1M input tokens + $15.00 per 1M output tokens
- **Google Gemini**: ~$0.000075 per 1K input tokens + $0.0003 per 1K output tokens
- **Typical Analysis**: 4,000-16,000 tokens per analysis ($0.10-$0.50 depending on model)
- **Use Caching**: Identical queries are cached to avoid re-analysis
- **Set Limits**: Configure `MaxTokens` to control response length

### Troubleshooting

#### "AI service is not available"
- Check your API key is correct and valid
- Verify `IsEnabled: true` for your chosen provider
- Ensure you have billing set up with the AI provider
- Check your internet connection

#### "Error getting AI analysis"
- Check the error message for specific details
- Verify API key has not expired
- Check if you've exceeded rate limits
- Ensure sufficient account balance
- Verify timeout settings (180s default)

#### "Unable to get AI analysis"
- The AI service is working but couldn't generate a response
- Try reducing the complexity of your request
- Check if the data is too large (reduce MaxTokens)

## 🚀 Advanced Configuration

### Multiple Providers
You can enable multiple providers and set fallbacks:
1. Set multiple providers to `IsEnabled: true`
2. The system will try the default provider first
3. If it fails, it will try other available providers

### Model Selection

#### OpenAI Models
- **o3**: Deep reasoning and complex debugging scenarios
- **gpt-4.1**: General-purpose coding and analysis
- **gpt-4o**: Balanced performance and cost
- **o4-mini**: Fast responses for simple tasks
- **o3-mini**: Quick analysis with good accuracy
- **gpt-4.5**: Enhanced reasoning capabilities

#### Anthropic Models
- **claude-sonnet-4**: Advanced reasoning (recommended for complex debugging)
- **claude-sonnet-3.7**: Balanced performance
- **claude-opus-4**: Highest quality analysis
- **claude-sonnet-3.5**: Fast help with simple tasks

#### Google Models
- **gemini-2.5-pro**: Deep reasoning and large context (2M tokens)
- **gemini-2.0-flash**: Fast general-purpose analysis (1M tokens)

### Performance Tuning
- **Temperature**: 0.2 for consistent technical analysis (optimized for coding/debugging)
- **TopP**: 0.1 (OpenAI), 0.99 (Anthropic), 0.95 (Google) - optimized for each provider
- **MaxTokens**: 16000 for comprehensive debugging analysis
- **CacheExpirationMinutes**: 60 minutes for balance between consistency and freshness
- **MaxAutoFunctionCalls**: 5 levels of automated investigation
- **OrchestrationTimeoutSeconds**: 600 seconds total timeout for complex analyses

### Stack Analysis Configuration
- **MaxDetailedThreads**: 10 threads analyzed in detail
- **MaxFramesPerThread**: 30 stack frames per thread
- **MaxTotalPayloadChars**: 40,000 characters maximum payload
- **FilterSystemCode**: true to focus on application code

## 📊 Example Analysis Results

### CLR Stack Analysis
```
The call stack shows a potential deadlock scenario:
- Thread 1 is waiting on a lock in OrderProcessor.ProcessOrder()
- High memory allocation in StringUtils.FormatData() suggests string concatenation in a loop
- Consider using StringBuilder for string operations
- Review synchronization logic to prevent deadlocks
```

### Source Code Analysis  
```
Memory leak detected in EventHandler subscription:
- Line 45: Event handler not properly unsubscribed
- Recommendation: Implement IDisposable and unsubscribe in Dispose()
- Performance issue: LINQ operation in tight loop (line 67)
- Consider caching the compiled expression
```

### Assembly Analysis
```
Performance bottleneck identified:
- Excessive memory allocations in the loop at 0x12345678
- Boxing/unboxing detected in value type operations
- Recommendation: Use generic collections to avoid boxing
- Consider struct layouts for better cache performance
```

## 🔍 How It Works

1. **Data Extraction**: DumpMiner extracts relevant data from memory dumps
2. **Context Building**: AI service builds appropriate context for analysis
3. **Prompt Generation**: Specialized prompts are created for each operation type
4. **AI Analysis**: Request is sent to configured AI provider
5. **Function Calling**: AI can automatically call other operations for deeper investigation
6. **Response Processing**: AI response is formatted and displayed
7. **Caching**: Results are cached to avoid duplicate expensive API calls

## 🛡️ Security & Privacy

- **API Keys**: Stored locally in appsettings.json
- **Data**: Memory dump data is sent to AI providers for analysis
- **Caching**: Responses cached locally only
- **No Persistence**: AI providers don't store your data permanently (per their policies)

## 📋 Next Steps

1. Set up your OpenAI API key (recommended for best results)
2. Test with a small memory dump
3. Explore different AI providers based on your needs
4. Customize prompts and settings for your use cases
5. Monitor usage and costs using the built-in tracking

Happy debugging with AI-powered insights! 🚀 