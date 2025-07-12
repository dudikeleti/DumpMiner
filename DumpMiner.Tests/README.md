# DumpMiner Test Infrastructure Guide

## 🤖 AI Agent Instructions

**IMPORTANT**: When working on tests, you MUST update this document to reflect:
- New test files created
- Coverage improvements
- Infrastructure changes
- Test categories added
- Progress on pending test areas

Update the "Current Test Coverage" and "Pending Test Areas" sections after each test implementation session.

## Overview

This document provides comprehensive guidance for AI agents working on the DumpMiner test infrastructure. The test suite uses modern .NET testing practices with xUnit, FluentAssertions, and Moq to ensure comprehensive coverage of the WPF debugging application.

## Test Infrastructure Architecture

### Core Infrastructure Files

#### 1. BaseTestClass.cs
**Location**: `DumpMiner.Tests/Infrastructure/BaseTestClass.cs`
**Purpose**: Provides common testing functionality for all test classes

**Key Features**:
- Service provider management with dependency injection
- Mock repository with automatic verification
- Test data generation utilities (random strings, addresses, GUIDs)
- Performance measurement tools
- Async testing utilities with timeout support
- Exception handling patterns
- Test lifecycle management with setup/teardown
- Logging infrastructure for tests
- Thread-safe operations

**Usage Pattern**:
```csharp
public class MyOperationTests : BaseTestClass
{
    [Fact]
    public async Task MyTest_Should_Work()
    {
        // Arrange
        var mockService = CreateMock<IMyService>();
        var testData = GenerateRandomString(10);
        
        // Act & Assert
        await MeasurePerformanceAsync("MyOperation", async () => {
            // test logic
        });
    }
}
```

#### 2. TestDataBuilders.cs
**Location**: `DumpMiner.Tests/Infrastructure/TestDataBuilders.cs`
**Purpose**: Provides fluent builders for creating test data

**Available Builders**:
- `OperationModelBuilder` - For creating operation models
- `AIRequestBuilder` - For AI request objects
- `AIResponseBuilder` - For AI response objects
- `DumpContextBuilder` - For dump analysis context
- `AIConfigurationBuilder` - For AI provider configurations
- `ConversationMessageBuilder` - For chat messages
- `TestDataFactory` - Static factory methods for common scenarios

**Usage Pattern**:
```csharp
var operationModel = new OperationModelBuilder()
    .WithName("TestOperation")
    .WithDescription("Test description")
    .WithAIEnabled(true)
    .Build();

var aiRequest = new AIRequestBuilder()
    .WithUserPrompt("Analyze this dump")
    .WithConversationHistory(messages)
    .Build();
```

#### 3. MockFactories.cs
**Location**: `DumpMiner.Tests/Infrastructure/MockFactories.cs`
**Purpose**: Provides pre-configured mock objects for common scenarios

**Available Mocks**:
- `CreateMockAIServiceManager()` - AI service manager with default behaviors
- `CreateMockAIProvider()` - AI provider with configurable responses
- `CreateMockAIOrchestrator()` - AI orchestrator with analysis capabilities
- `CreateMockDebuggerOperation()` - Debugger operation with execution results
- `CreateMockConfigurationService()` - Configuration service with app settings
- Scenario-based mocks (Successful, Failed, Slow, Timeout providers)

**Usage Pattern**:
```csharp
var mockAIService = MockFactories.CreateMockAIServiceManager();
mockAIService.Setup(x => x.AnalyzeAsync(It.IsAny<string>()))
    .ReturnsAsync("Analysis result");
```

#### 4. TestUtilities.cs
**Location**: `DumpMiner.Tests/Infrastructure/TestUtilities.cs`
**Purpose**: Provides common testing utilities and helper methods

**Key Utilities**:
- `WaitForConditionAsync()` - Async condition waiting with timeout
- `CreateTempFile()` / `CreateTempDirectory()` - Temporary file system resources
- `AssertCollectionContains()` - Enhanced collection assertions
- `MeasurePerformance()` - Performance benchmarking
- `ValidateMemoryAddress()` - Memory address validation
- `AssertThrowsAsync<T>()` - Async exception testing

#### 5. TestCategories.cs
**Location**: `DumpMiner.Tests/Infrastructure/TestCategories.cs`
**Purpose**: Provides test categorization and traits for organized test execution

**Available Categories**:
- **Test Types**: Unit, Integration, AI, Performance, Operations, ViewModels, Services
- **Priority Levels**: Critical, High, Medium, Low
- **Execution Times**: VeryFast (<100ms), Fast (<1s), Medium (<10s), Slow (<60s), VerySlow (>60s)
- **Environments**: Windows, Development, CI
- **Composite Attributes**: Common combinations for easy use

**Usage Pattern**:
```csharp
[Fact]
[TestCategory.Unit]
[TestCategory.Operations]
[TestCategory.Critical]
public void MyTest_Should_Work() { }

// Or use composite attributes
[Fact]
[CriticalUnitTest]
public void AnotherTest_Should_Work() { }
```

### Configuration

#### appsettings.test.json
**Location**: `DumpMiner.Tests/appsettings.test.json`
**Purpose**: Test-specific configuration settings

**Key Sections**:
- Logging configuration with detailed levels
- AI provider settings for testing
- Test execution parameters
- Mock behavior configuration
- Performance benchmarks
- Validation settings

## Current Test Coverage

### ✅ Fully Covered Areas

#### AI Configuration & Providers
- **AIConfigurationTests.cs** (248 lines)
  - Complete validation testing for all AI configuration classes
  - Provider configuration validation
  - Default value verification
  - Range validation for all numeric properties

- **AnthropicProviderTests.cs** (331 lines)
  - Provider initialization and configuration
  - Cost estimation for different models and scenarios
  - Error handling for invalid configurations
  - Model-specific behavior testing

#### AI Service Integration
- **AIServiceIntegrationTests.cs** (316 lines)
  - End-to-end AI service testing
  - Integration between components
  - Real-world scenario testing

#### Test Infrastructure
- **BaseTestClass.cs** - Complete implementation
- **TestDataBuilders.cs** - Complete implementation
- **MockFactories.cs** - Complete implementation
- **TestUtilities.cs** - Complete implementation
- **TestCategories.cs** - Complete implementation

### 🔄 Partially Covered Areas

#### Operations (10% Coverage)
- **DumpHeapOperationTests.cs** (Example implementation)
  - Basic functionality testing
  - Input validation
  - Performance testing
  - AI integration testing
  - Memory analysis validation
  - Error handling scenarios
  - Thread safety testing

#### ViewModels (10% Coverage)
- **BaseOperationViewModelTests.cs** (Example implementation)
  - Property change notification testing
  - Command execution validation
  - AI integration testing
  - Performance testing for UI operations
  - Memory management and disposal
  - Thread safety testing

### ❌ Pending Test Areas (High Priority)

#### Operations (90% Remaining)
**Location**: `DumpMiner/Operations/`
**Priority**: Critical

**Pending Operations**:
1. **AutomatedAnalysisOperation.cs** - AI-powered automated analysis
2. **DeadlockDetectionOperation.cs** - Deadlock detection and analysis
3. **DumpClrStackOperation.cs** - CLR stack analysis
4. **DumpComparisonOperation.cs** - Dump comparison functionality
5. **DumpExceptionsOperation.cs** - Exception analysis
6. **DumpFinalizerQueueOperation.cs** - Finalizer queue inspection
7. **DumpGcHandlesOperation.cs** - GC handle analysis
8. **DumpHeapSegmentsOperation.cs** - Heap segment analysis
9. **DumpHeapStatOperation.cs** - Heap statistics
10. **DumpLargeObjectsOperation.cs** - Large object heap analysis
11. **DumpMemoryRegionsOperation.cs** - Memory region analysis
12. **DumpMethodsOperation.cs** - Method analysis
13. **DumpModulesOperation.cs** - Module inspection
14. **DumpObjectOperation.cs** - Object inspection
15. **DumpSourceCodeOperation.cs** - Source code correlation
16. **DumpSyncBlockOperation.cs** - Synchronization block analysis
17. **DumpTypeInfoOperation.cs** - Type information analysis
18. **GetObjectRootOperation.cs** - Object root analysis
19. **GetObjectSizeOperation.cs** - Object size calculation
20. **JitAnalysisOperation.cs** - JIT compilation analysis
21. **MemoryLeakDetectionOperation.cs** - Memory leak detection
22. **TargetProcessInfoOperation.cs** - Target process information
23. **TypeFromHandleOperation.cs** - Type handle resolution

#### ViewModels (90% Remaining)
**Location**: `DumpMiner/ViewModels/`
**Priority**: High

**Pending ViewModels**:
1. **AISettingsViewModel.cs** - AI configuration UI
2. **AppearanceViewModel.cs** - Appearance settings
3. **AttachDetachViewModel.cs** - Process attachment/detachment
4. **DumpAnalyzerViewModel.cs** - Main dump analysis UI
5. **DumpHeapOperationViewModel.cs** - Heap operation UI
6. **DumpLargeObjectsViewModel.cs** - Large objects UI
7. **DumpOptionsViewModel.cs** - Dump options UI
8. **GeneralSettingsViewModel.cs** - General settings UI
9. **OperationTypesViewModel.cs** - Operation types UI

#### Services (Partial Coverage)
**Location**: `DumpMiner/Services/`
**Priority**: High

**Pending Services**:
1. **AIServiceManager.cs** - Core AI service management
2. **ConfigurationService.cs** - Application configuration
3. **CrossPlatformDumpService.cs** - Cross-platform dump handling
4. **SymbolManager.cs** - Symbol management
5. **AIOrchestrator.cs** - AI orchestration logic
6. **AICacheService.cs** - AI response caching
7. **ComprehensiveDumpAnalyzer.cs** - Comprehensive analysis

## Test Development Guidelines

### 1. Test Class Structure
```csharp
[TestCategory.Unit]
[TestCategory.Operations] // or ViewModels, Services
public class MyOperationTests : BaseTestClass
{
    private readonly MyOperation _operation;
    private readonly Mock<IDependency> _mockDependency;

    public MyOperationTests()
    {
        _mockDependency = CreateMock<IDependency>();
        _operation = new MyOperation(_mockDependency.Object);
    }

    [Fact]
    [TestCategory.Critical]
    public async Task Execute_WithValidInput_ShouldReturnSuccess()
    {
        // Arrange
        var input = new OperationModelBuilder()
            .WithName("TestOperation")
            .Build();

        // Act
        var result = await _operation.ExecuteAsync(input);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }
}
```

### 2. Test Naming Conventions
- **Method Format**: `MethodName_Scenario_ExpectedBehavior`
- **Examples**:
  - `Execute_WithValidInput_ShouldReturnSuccess`
  - `Execute_WithNullInput_ShouldThrowArgumentNullException`
  - `Execute_WithAIEnabled_ShouldCallAIService`

### 3. Test Categories Usage
- Always categorize tests appropriately
- Use composite attributes for common combinations
- Consider execution time when choosing categories
- Use priority levels for test execution ordering

### 4. Performance Testing
```csharp
[Fact]
[TestCategory.Performance]
public async Task Execute_Performance_ShouldCompleteWithinTimeLimit()
{
    // Arrange
    var input = CreateTestData();

    // Act & Assert
    await MeasurePerformanceAsync("ExecuteOperation", async () =>
    {
        var result = await _operation.ExecuteAsync(input);
        result.Should().NotBeNull();
    }, maxExecutionTime: TimeSpan.FromSeconds(5));
}
```

### 5. AI Integration Testing
```csharp
[Fact]
[TestCategory.AI]
public async Task Execute_WithAIEnabled_ShouldAnalyzeResults()
{
    // Arrange
    var mockAIService = MockFactories.CreateMockAIServiceManager();
    var expectedAnalysis = "Analysis result";
    mockAIService.Setup(x => x.AnalyzeAsync(It.IsAny<string>()))
        .ReturnsAsync(expectedAnalysis);

    // Act
    var result = await _operation.ExecuteAsync(input);

    // Assert
    result.AIAnalysis.Should().Be(expectedAnalysis);
    mockAIService.Verify(x => x.AnalyzeAsync(It.IsAny<string>()), Times.Once);
}
```

## Development Workflow

### 1. Before Starting
1. Review this document
2. Check current test coverage
3. Identify priority areas
4. Plan test implementation

### 2. Test Implementation Process
1. Create test class extending `BaseTestClass`
2. Add appropriate test categories
3. Implement core functionality tests
4. Add edge case and error handling tests
5. Include performance tests where relevant
6. Add AI integration tests for AI-enabled operations
7. Update this document with new coverage

### 3. Test Validation
1. Ensure all tests pass
2. Verify test categories are correct
3. Check performance benchmarks
4. Validate mock usage and verification
5. Review test documentation

## Running Tests

### Command Line
```bash
# Run all tests
dotnet test

# Run specific category
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Operations"
dotnet test --filter "Priority=Critical"

# Run performance tests
dotnet test --filter "Category=Performance"

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"
```

### Visual Studio
- Use Test Explorer to run tests
- Filter by categories using traits
- Monitor test execution time
- Review test output and coverage

### Validation Scripts

#### 1. system-validation.ps1 - System Validation Script
**Location**: `system-validation.ps1` (project root)
**Purpose**: Comprehensive system validation for CI/CD pipeline and development workflow

**What it validates**:
- Configuration files and AI setup
- Operation inheritance (BaseAIOperation usage)
- AI service architecture files
- Test infrastructure completeness
- Build success (main project and tests)
- Unit test execution with category breakdown
- UI integration components

**Usage**:
```powershell
# Run complete system validation
.\system-validation.ps1

# The script provides colored output showing:
# ✅ Successes
# ⚠️ Issues found
# 📊 Implementation progress
# 🚀 Next steps
```

**Key Features**:
- Runs critical tests first for quick feedback
- Shows test results by category (Unit, Integration, AI, Operations, etc.)
- Validates test infrastructure files
- Provides actionable recommendations

#### 2. TestConfiguration.cs - AI Configuration Diagnostic
**Location**: `TestConfiguration.cs` (project root)
**Purpose**: Standalone diagnostic tool for AI configuration validation

**What it tests**:
- Configuration file existence and structure
- AI configuration loading and validation
- Provider configurations (OpenAI, Anthropic, Google)
- AI service creation and availability
- Basic AI functionality readiness

**Usage**:
```bash
# Compile and run diagnostic
dotnet run --project TestConfiguration.cs

# Provides detailed logging output:
# 📁 Configuration Files
# ⚙️ AI Configuration Loading
# 🔧 AI Service Creation
# 🚀 AI Service Availability
# 🧠 Basic AI Functionality
```

**Key Features**:
- Detailed logging with structured output
- Validates all provider configurations
- Tests actual AI service instantiation
- Non-destructive testing (read-only operations)
- Helpful for troubleshooting AI setup issues

## Best Practices

### 1. Test Organization
- Group related tests in the same class
- Use clear, descriptive test names
- Organize tests by functionality, not by test type
- Keep tests focused on single responsibilities

### 2. Mock Management
- Use MockFactories for common scenarios
- Verify mock interactions appropriately
- Reset mocks between tests when needed
- Use scenario-based mocks for complex testing

### 3. Test Data
- Use TestDataBuilders for consistent test data
- Create realistic test scenarios
- Include edge cases and boundary conditions
- Use TestDataFactory for common scenarios

### 4. Performance Considerations
- Use appropriate test categories for execution time
- Set reasonable performance expectations
- Test both normal and stress scenarios
- Monitor memory usage in performance tests

### 5. AI Testing
- Test both AI-enabled and AI-disabled scenarios
- Mock AI services appropriately
- Test error handling for AI failures
- Include cost estimation testing

## Test Types and Tools

### 1. Unit Tests (DumpMiner.Tests/)
- **Purpose**: Isolated testing of individual components
- **Framework**: xUnit with FluentAssertions and Moq
- **Infrastructure**: BaseTestClass, TestDataBuilders, MockFactories
- **Execution**: `dotnet test DumpMiner.Tests`
- **Categories**: Unit, Integration, AI, Performance, Operations, ViewModels, Services

### 2. Diagnostic Tools (Project Root)
- **system-validation.ps1**: Complete system validation script for CI/CD and development
- **TestConfiguration.cs**: AI configuration diagnostic tool
- **Purpose**: System-level validation and troubleshooting
- **Usage**: Independent of unit test framework

### 3. Integration Tests
- **Location**: `DumpMiner.Tests/Integration/`
- **Purpose**: End-to-end testing of AI services
- **Example**: `AIServiceIntegrationTests.cs`
- **Execution**: `dotnet test --filter "Category=Integration"`

## Troubleshooting

### Common Issues
1. **Test Timeouts**: Increase timeout values or optimize test logic
2. **Mock Verification Failures**: Check mock setup and usage
3. **Async Test Issues**: Use proper async testing patterns
4. **Performance Test Failures**: Adjust performance expectations
5. **AI Test Failures**: Verify AI service mocking
6. **Configuration Issues**: Run `TestConfiguration.cs` diagnostic tool
7. **Build Failures**: Run `system-validation.ps1` for comprehensive validation

### Debugging Tips
1. Use test output for debugging information
2. Enable detailed logging in test configuration
3. Use breakpoints in test methods
4. Check test data generation
5. Verify mock behaviors
6. Run diagnostic tools for system-level issues
7. Use category filters to isolate problematic tests

---

## Document Maintenance

**Last Updated**: [Current Date]
**Updated By**: AI Agent
**Version**: 1.1

### Change Log
- Initial document creation with comprehensive test infrastructure
- Added examples for all major test patterns
- Documented current coverage and pending areas
- Provided complete development workflow
- **v1.1**: Renamed test-ai.ps1 to system-validation.ps1 to better reflect comprehensive system validation purpose

### Next Review
- Review after each major test implementation session
- Update coverage percentages
- Add new test categories as needed
- Refine best practices based on experience 