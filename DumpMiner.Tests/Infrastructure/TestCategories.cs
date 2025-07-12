using System;
using Xunit;

namespace DumpMiner.Tests.Infrastructure
{
    /// <summary>
    /// Test categories for organizing and filtering tests
    /// </summary>
    public static class TestCategories
    {
        /// <summary>
        /// Unit tests - fast, isolated tests that test a single component
        /// </summary>
        public const string Unit = "Unit";

        /// <summary>
        /// Integration tests - tests that verify components work together
        /// </summary>
        public const string Integration = "Integration";

        /// <summary>
        /// AI-related tests - tests that involve AI services
        /// </summary>
        public const string AI = "AI";

        /// <summary>
        /// Configuration tests - tests that verify configuration loading and validation
        /// </summary>
        public const string Configuration = "Configuration";

        /// <summary>
        /// Operation tests - tests that verify debugging operations
        /// </summary>
        public const string Operations = "Operations";

        /// <summary>
        /// ViewModel tests - tests that verify UI ViewModels
        /// </summary>
        public const string ViewModels = "ViewModels";

        /// <summary>
        /// Service tests - tests that verify service layer components
        /// </summary>
        public const string Services = "Services";

        /// <summary>
        /// Performance tests - tests that measure performance characteristics
        /// </summary>
        public const string Performance = "Performance";

        /// <summary>
        /// Slow tests - tests that take significant time to execute
        /// </summary>
        public const string Slow = "Slow";

        /// <summary>
        /// Memory tests - tests that involve memory dump analysis
        /// </summary>
        public const string Memory = "Memory";

        /// <summary>
        /// Threading tests - tests that involve thread safety or concurrency
        /// </summary>
        public const string Threading = "Threading";

        /// <summary>
        /// Security tests - tests that verify security-related functionality
        /// </summary>
        public const string Security = "Security";

        /// <summary>
        /// External dependency tests - tests that require external services
        /// </summary>
        public const string External = "External";

        /// <summary>
        /// File system tests - tests that involve file operations
        /// </summary>
        public const string FileSystem = "FileSystem";

        /// <summary>
        /// Network tests - tests that involve network operations
        /// </summary>
        public const string Network = "Network";

        /// <summary>
        /// Database tests - tests that involve database operations
        /// </summary>
        public const string Database = "Database";

        /// <summary>
        /// Regression tests - tests that verify bug fixes
        /// </summary>
        public const string Regression = "Regression";

        /// <summary>
        /// Smoke tests - basic functionality tests
        /// </summary>
        public const string Smoke = "Smoke";

        /// <summary>
        /// Critical tests - tests for critical functionality
        /// </summary>
        public const string Critical = "Critical";

        /// <summary>
        /// Experimental tests - tests for experimental features
        /// </summary>
        public const string Experimental = "Experimental";
    }

    /// <summary>
    /// Test trait attributes for categorizing tests
    /// </summary>
    public static class TestTraits
    {
        /// <summary>
        /// The trait name used for categorization
        /// </summary>
        public const string Category = "Category";

        /// <summary>
        /// The trait name used for test priority
        /// </summary>
        public const string Priority = "Priority";

        /// <summary>
        /// The trait name used for test owner
        /// </summary>
        public const string Owner = "Owner";

        /// <summary>
        /// The trait name used for test execution time
        /// </summary>
        public const string ExecutionTime = "ExecutionTime";

        /// <summary>
        /// The trait name used for test complexity
        /// </summary>
        public const string Complexity = "Complexity";

        /// <summary>
        /// The trait name used for test environment requirements
        /// </summary>
        public const string Environment = "Environment";
    }

    /// <summary>
    /// Test priority levels
    /// </summary>
    public static class TestPriorities
    {
        /// <summary>
        /// Critical tests that must pass
        /// </summary>
        public const string Critical = "Critical";

        /// <summary>
        /// High priority tests
        /// </summary>
        public const string High = "High";

        /// <summary>
        /// Medium priority tests
        /// </summary>
        public const string Medium = "Medium";

        /// <summary>
        /// Low priority tests
        /// </summary>
        public const string Low = "Low";
    }

    /// <summary>
    /// Test execution time classifications
    /// </summary>
    public static class TestExecutionTimes
    {
        /// <summary>
        /// Very fast tests (< 100ms)
        /// </summary>
        public const string VeryFast = "VeryFast";

        /// <summary>
        /// Fast tests (< 1s)
        /// </summary>
        public const string Fast = "Fast";

        /// <summary>
        /// Medium tests (1-10s)
        /// </summary>
        public const string Medium = "Medium";

        /// <summary>
        /// Slow tests (10-60s)
        /// </summary>
        public const string Slow = "Slow";

        /// <summary>
        /// Very slow tests (> 60s)
        /// </summary>
        public const string VerySlow = "VerySlow";
    }

    /// <summary>
    /// Test complexity classifications
    /// </summary>
    public static class TestComplexities
    {
        /// <summary>
        /// Simple tests with minimal setup
        /// </summary>
        public const string Simple = "Simple";

        /// <summary>
        /// Moderate tests with some setup
        /// </summary>
        public const string Moderate = "Moderate";

        /// <summary>
        /// Complex tests with extensive setup
        /// </summary>
        public const string Complex = "Complex";

        /// <summary>
        /// Very complex tests with intricate scenarios
        /// </summary>
        public const string VeryComplex = "VeryComplex";
    }

    /// <summary>
    /// Test environment requirements
    /// </summary>
    public static class TestEnvironments
    {
        /// <summary>
        /// Tests that can run in any environment
        /// </summary>
        public const string Any = "Any";

        /// <summary>
        /// Tests that require Windows
        /// </summary>
        public const string Windows = "Windows";

        /// <summary>
        /// Tests that require Linux
        /// </summary>
        public const string Linux = "Linux";

        /// <summary>
        /// Tests that require macOS
        /// </summary>
        public const string macOS = "macOS";

        /// <summary>
        /// Tests that require development environment
        /// </summary>
        public const string Development = "Development";

        /// <summary>
        /// Tests that require CI/CD environment
        /// </summary>
        public const string CI = "CI";

        /// <summary>
        /// Tests that require staging environment
        /// </summary>
        public const string Staging = "Staging";

        /// <summary>
        /// Tests that require production-like environment
        /// </summary>
        public const string Production = "Production";
    }

    /// <summary>
    /// Attribute for marking unit tests
    /// </summary>
    [TraitAttribute(TestTraits.Category, TestCategories.Unit)]
    public class UnitTestAttribute : Attribute
    {
    }

    /// <summary>
    /// Attribute for marking integration tests
    /// </summary>
    [TraitAttribute(TestTraits.Category, TestCategories.Integration)]
    public class IntegrationTestAttribute : Attribute
    {
    }

    /// <summary>
    /// Attribute for marking AI tests
    /// </summary>
    [TraitAttribute(TestTraits.Category, TestCategories.AI)]
    public class AITestAttribute : Attribute
    {
    }

    /// <summary>
    /// Attribute for marking configuration tests
    /// </summary>
    [TraitAttribute(TestTraits.Category, TestCategories.Configuration)]
    public class ConfigurationTestAttribute : Attribute
    {
    }

    /// <summary>
    /// Attribute for marking operation tests
    /// </summary>
    [TraitAttribute(TestTraits.Category, TestCategories.Operations)]
    public class OperationTestAttribute : Attribute
    {
    }

    /// <summary>
    /// Attribute for marking ViewModel tests
    /// </summary>
    [TraitAttribute(TestTraits.Category, TestCategories.ViewModels)]
    public class ViewModelTestAttribute : Attribute
    {
    }

    /// <summary>
    /// Attribute for marking service tests
    /// </summary>
    [TraitAttribute(TestTraits.Category, TestCategories.Services)]
    public class ServiceTestAttribute : Attribute
    {
    }

    /// <summary>
    /// Attribute for marking performance tests
    /// </summary>
    [TraitAttribute(TestTraits.Category, TestCategories.Performance)]
    public class PerformanceTestAttribute : Attribute
    {
    }

    /// <summary>
    /// Attribute for marking slow tests
    /// </summary>
    [TraitAttribute(TestTraits.Category, TestCategories.Slow)]
    public class SlowTestAttribute : Attribute
    {
    }

    /// <summary>
    /// Attribute for marking memory tests
    /// </summary>
    [TraitAttribute(TestTraits.Category, TestCategories.Memory)]
    public class MemoryTestAttribute : Attribute
    {
    }

    /// <summary>
    /// Attribute for marking threading tests
    /// </summary>
    [TraitAttribute(TestTraits.Category, TestCategories.Threading)]
    public class ThreadingTestAttribute : Attribute
    {
    }

    /// <summary>
    /// Attribute for marking security tests
    /// </summary>
    [TraitAttribute(TestTraits.Category, TestCategories.Security)]
    public class SecurityTestAttribute : Attribute
    {
    }

    /// <summary>
    /// Attribute for marking external dependency tests
    /// </summary>
    [TraitAttribute(TestTraits.Category, TestCategories.External)]
    public class ExternalTestAttribute : Attribute
    {
    }

    /// <summary>
    /// Attribute for marking file system tests
    /// </summary>
    [TraitAttribute(TestTraits.Category, TestCategories.FileSystem)]
    public class FileSystemTestAttribute : Attribute
    {
    }

    /// <summary>
    /// Attribute for marking network tests
    /// </summary>
    [TraitAttribute(TestTraits.Category, TestCategories.Network)]
    public class NetworkTestAttribute : Attribute
    {
    }

    /// <summary>
    /// Attribute for marking regression tests
    /// </summary>
    [TraitAttribute(TestTraits.Category, TestCategories.Regression)]
    public class RegressionTestAttribute : Attribute
    {
    }

    /// <summary>
    /// Attribute for marking smoke tests
    /// </summary>
    [TraitAttribute(TestTraits.Category, TestCategories.Smoke)]
    public class SmokeTestAttribute : Attribute
    {
    }

    /// <summary>
    /// Attribute for marking critical tests
    /// </summary>
    [TraitAttribute(TestTraits.Category, TestCategories.Critical)]
    public class CriticalTestAttribute : Attribute
    {
    }

    /// <summary>
    /// Attribute for marking experimental tests
    /// </summary>
    [TraitAttribute(TestTraits.Category, TestCategories.Experimental)]
    public class ExperimentalTestAttribute : Attribute
    {
    }

    /// <summary>
    /// Attribute for setting test priority
    /// </summary>
    public class TestPriorityAttribute : Attribute
    {
        public TestPriorityAttribute(string priority) 
        {
            Priority = priority;
        }

        public string Priority { get; }
    }

    /// <summary>
    /// Attribute for setting test execution time
    /// </summary>
    public class TestExecutionTimeAttribute : Attribute
    {
        public TestExecutionTimeAttribute(string executionTime) 
        {
            ExecutionTime = executionTime;
        }

        public string ExecutionTime { get; }
    }

    /// <summary>
    /// Attribute for setting test complexity
    /// </summary>
    public class TestComplexityAttribute : Attribute
    {
        public TestComplexityAttribute(string complexity) 
        {
            Complexity = complexity;
        }

        public string Complexity { get; }
    }

    /// <summary>
    /// Attribute for setting test environment requirements
    /// </summary>
    public class TestEnvironmentAttribute : Attribute
    {
        public TestEnvironmentAttribute(string environment) 
        {
            Environment = environment;
        }

        public string Environment { get; }
    }

    /// <summary>
    /// Attribute for setting test owner
    /// </summary>
    public class TestOwnerAttribute : Attribute
    {
        public TestOwnerAttribute(string owner) 
        {
            Owner = owner;
        }

        public string Owner { get; }
    }

    /// <summary>
    /// Composite attribute for marking fast unit tests
    /// </summary>
    public class FastUnitTestAttribute : Attribute
    {
        public const string Category = TestCategories.Unit;
        public const string ExecutionTime = TestExecutionTimes.Fast;
        public const string Priority = TestPriorities.High;
    }

    /// <summary>
    /// Composite attribute for marking slow integration tests
    /// </summary>
    public class SlowIntegrationTestAttribute : Attribute
    {
        public const string Category = TestCategories.Integration;
        public const string ExecutionTime = TestExecutionTimes.Slow;
        public const string Priority = TestPriorities.Medium;
    }

    /// <summary>
    /// Composite attribute for marking critical smoke tests
    /// </summary>
    public class CriticalSmokeTestAttribute : Attribute
    {
        public const string Category = TestCategories.Smoke;
        public const string Priority = TestPriorities.Critical;
        public const string ExecutionTime = TestExecutionTimes.Fast;
    }

    /// <summary>
    /// Composite attribute for marking AI performance tests
    /// </summary>
    public class AIPerformanceTestAttribute : Attribute
    {
        public const string Category = TestCategories.AI + "," + TestCategories.Performance;
        public const string ExecutionTime = TestExecutionTimes.Medium;
        public const string Priority = TestPriorities.Medium;
    }

    /// <summary>
    /// Composite attribute for marking memory operation tests
    /// </summary>
    public class MemoryOperationTestAttribute : Attribute
    {
        public const string Category = TestCategories.Memory + "," + TestCategories.Operations;
        public const string ExecutionTime = TestExecutionTimes.Medium;
        public const string Priority = TestPriorities.High;
    }

    /// <summary>
    /// Composite attribute for marking UI ViewModel tests
    /// </summary>
    public class UIViewModelTestAttribute : Attribute
    {
        public const string Category = TestCategories.ViewModels;
        public const string ExecutionTime = TestExecutionTimes.Fast;
        public const string Priority = TestPriorities.High;
        public const string Environment = TestEnvironments.Windows;
    }

    /// <summary>
    /// Composite attribute for marking external AI service tests
    /// </summary>
    public class ExternalAIServiceTestAttribute : Attribute
    {
        public const string Category = TestCategories.AI + "," + TestCategories.External;
        public const string ExecutionTime = TestExecutionTimes.Slow;
        public const string Priority = TestPriorities.Low;
        public const string Environment = TestEnvironments.Development;
    }
}

/// <summary>
/// Test collection definitions for parallel execution control
/// </summary>
namespace DumpMiner.Tests.Collections
{
    /// <summary>
    /// Collection for AI tests that should not run in parallel
    /// </summary>
    [CollectionDefinition("AI Tests")]
    public class AITestCollection : ICollectionFixture<AITestFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }

    /// <summary>
    /// Collection for file system tests that should not run in parallel
    /// </summary>
    [CollectionDefinition("File System Tests")]
    public class FileSystemTestCollection : ICollectionFixture<FileSystemTestFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }

    /// <summary>
    /// Collection for performance tests that should not run in parallel
    /// </summary>
    [CollectionDefinition("Performance Tests")]
    public class PerformanceTestCollection : ICollectionFixture<PerformanceTestFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }

    /// <summary>
    /// Fixture for AI tests
    /// </summary>
    public class AITestFixture : IDisposable
    {
        public AITestFixture()
        {
            // Setup code for AI tests
        }

        public void Dispose()
        {
            // Cleanup code for AI tests
        }
    }

    /// <summary>
    /// Fixture for file system tests
    /// </summary>
    public class FileSystemTestFixture : IDisposable
    {
        public string TempDirectory { get; private set; }

        public FileSystemTestFixture()
        {
            TempDirectory = Path.Combine(Path.GetTempPath(), $"DumpMinerTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(TempDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(TempDirectory))
            {
                Directory.Delete(TempDirectory, true);
            }
        }
    }

    /// <summary>
    /// Fixture for performance tests
    /// </summary>
    public class PerformanceTestFixture : IDisposable
    {
        public PerformanceTestFixture()
        {
            // Setup code for performance tests
            // e.g., ensure minimum system resources
        }

        public void Dispose()
        {
            // Cleanup code for performance tests
        }
    }
} 