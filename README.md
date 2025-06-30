# DumpMiner

DumpMiner is a powerful tool for inspecting .NET dump files and live processes with **advanced AI integration**. The tool uses the ClrMD library and features intelligent AI analysis powered by OpenAI GPT-4, Anthropic Claude, and Google Gemini.

## 🤖 AI-Powered Analysis

DumpMiner now includes state-of-the-art AI capabilities that can:
- **Automatically investigate** suspicious patterns in memory dumps
- **Provide expert insights** on performance bottlenecks and memory leaks  
- **Call other operations** automatically to deep-dive into issues
- **Act like an experienced debugger** to save you time

## Libraries in use:
- https://github.com/betalgo/openai
- https://github.com/FLindqvist/UI.SyntaxBox
- https://github.com/microsoft/clrmd
- https://github.com/firstfloorsoftware/mui

## 📚 Documentation

- **[AI Integration Guide](DumpMiner/AI-INTEGRATION.md)** - Complete AI setup and technical documentation
- **[AI Setup Guide](DumpMiner/AI-Setup-Guide.md)** - Quick start guide for setting up AI features

## 🚀 Quick Start with AI

1. **Setup**: Edit `DumpMiner/appsettings.json` and add your OpenAI API key
2. **Test**: Run `./test-ai.ps1` to validate your setup
3. **Use**: Load a dump file and click "Ask AI" on any analysis operation

## Testing

Run the comprehensive test suite:
```powershell
./test-ai.ps1
```

Thanks [@gsuberland](https://github.com/gsuberland) for the object extractor.

---

https://github.com/dudikeleti/DumpMiner/assets/8845578/0c5bb3f3-0925-46b7-ab69-5d72baad1367

