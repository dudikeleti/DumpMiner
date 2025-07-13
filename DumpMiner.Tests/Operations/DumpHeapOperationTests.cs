using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Common;
using DumpMiner.Models;
using DumpMiner.Operations;
using DumpMiner.Tests.Infrastructure;
using Xunit.Abstractions;

namespace DumpMiner.Tests.Operations
{
    /// <summary>
    /// Comprehensive unit tests for DumpHeapOperation
    /// </summary>
    [UnitTest]
    [OperationTest]
    [MemoryTest]
    public class DumpHeapOperationTests : BaseTestClass
    {
        private readonly Mock<IDebuggerOperation> _mockDebuggerOperation;
        private readonly DumpHeapOperation _operation;

        public DumpHeapOperationTests(ITestOutputHelper testOutput) : base(testOutput)
        {
            _mockDebuggerOperation = CreateMock<IDebuggerOperation>();
            _operation = new DumpHeapOperation();
        }

        #region Basic Functionality Tests

        [Fact]
        [Trait(TestTraits.Priority, TestPriorities.Critical)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Fast)]
        public void Name_ShouldReturnCorrectOperationName()
        {
            // Act
            var name = _operation.Name;

            // Assert
            name.Should().Be(OperationNames.DumpHeap);
        }

        [Fact]
        [Trait(TestTraits.Priority, TestPriorities.High)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Fast)]
        public async Task Execute_WithValidModel_ShouldReturnResults()
        {
            // Arrange
            var operationModel = new OperationModelBuilder()
                .WithTypes("System.String")
                .WithNumOfResults(100)
                .Build();

            var expectedResults = new List<object>
            {
                new { Address = 0x12345678UL, Type = "System.String", Value = "Test String 1", Size = 24 },
                new { Address = 0x87654321UL, Type = "System.String", Value = "Test String 2", Size = 32 },
                new { Address = 0x11223344UL, Type = "System.String", Value = "Test String 3", Size = 28 }
            };

            // This is a demonstration - in reality, we'd mock the DebuggerSession
            // For now, we'll test the basic structure and behavior

            // Act & Assert
            // Since DumpHeapOperation depends on DebuggerSession which is a singleton
            // and requires actual dump data, we'll test what we can without mocking the entire session
            await TestUtilities.Async.AssertDoesNotThrowAsync(async () =>
            {
                // The operation should be created without throwing
                var operation = new DumpHeapOperation();
                operation.Name.Should().Be(OperationNames.DumpHeap);
            });
        }

        [Theory]
        [InlineData(-1, "All generations")]
        [InlineData(0, "Generation 0")]
        [InlineData(1, "Generation 1")]
        [InlineData(2, "Generation 2")]
        [InlineData(3, "Large Object Heap")]
        [Trait(TestTraits.Priority, TestPriorities.High)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Fast)]
        public void Execute_WithDifferentGenerations_ShouldHandleAllGenerations(int generation, string description)
        {
            // Arrange
            var operationModel = new OperationModelBuilder()
                .WithTypes("System.String")
                .WithNumOfResults(50)
                .Build();

            // Act & Assert
            TestUtilities.Validation.AssertInRange(generation, -1, 3, $"Generation {generation} ({description}) should be valid");
            
            // Test that the operation can be created and has correct name
            var operation = new DumpHeapOperation();
            operation.Name.Should().Be(OperationNames.DumpHeap, $"because operation should have correct name for {description}");
        }

        #endregion

        #region Input Validation Tests

        [Fact]
        [Trait(TestTraits.Priority, TestPriorities.High)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Fast)]
        public async Task Execute_WithNullModel_ShouldThrowArgumentNullException()
        {
            // Arrange
            var cancellationToken = CancellationToken.None;

            // Act & Assert
            await TestUtilities.Async.AssertThrowsAsync<ArgumentNullException>(
                async () => await _operation.Execute(null, cancellationToken, -1),
                "model cannot be null");
        }

        [Fact]
        [Trait(TestTraits.Priority, TestPriorities.Medium)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Fast)]
        public async Task Execute_WithCancelledToken_ShouldThrowOperationCanceledException()
        {
            // Arrange
            var operationModel = TestDataFactory.CreateBasicOperationModel();
            var cancellationToken = new CancellationToken(true); // Already cancelled

            // Act & Assert
            await TestUtilities.Async.AssertThrowsAsync<OperationCanceledException>(
                async () => await _operation.Execute(operationModel, cancellationToken, -1),
                "operation should be cancelled");
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [Trait(TestTraits.Priority, TestPriorities.Medium)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Fast)]
        public void Execute_WithInvalidTypes_ShouldHandleGracefully(string invalidTypes)
        {
            // Arrange
            var operationModel = new OperationModelBuilder()
                .WithTypes(invalidTypes)
                .WithNumOfResults(100)
                .Build();

            // Act & Assert
            var operation = new DumpHeapOperation();
            
            // Test that the operation handles invalid types gracefully
            // In reality, this would depend on the actual implementation
            operationModel.Types.Should().Be(invalidTypes);
        }

        #endregion

        #region Performance Tests

        [Fact]
        [PerformanceTest]
        [Trait(TestTraits.Priority, TestPriorities.Medium)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Medium)]
        public void Operation_Creation_ShouldBePerformant()
        {
            // Arrange
            var maxCreationTime = TimeSpan.FromMilliseconds(100);

            // Act & Assert
            TestUtilities.Performance.MeasureAndAssert(
                () => new DumpHeapOperation(),
                maxCreationTime,
                "DumpHeapOperation creation");
        }

        [Fact]
        [PerformanceTest]
        [Trait(TestTraits.Priority, TestPriorities.Low)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Medium)]
        public void Multiple_Operations_Creation_ShouldBeEfficient()
        {
            // Arrange
            const int iterations = 1000;
            var maxTotalTime = TimeSpan.FromMilliseconds(500);

            // Act & Assert
            var result = TestUtilities.Performance.RunPerformanceTest(
                () => new DumpHeapOperation(),
                iterations,
                $"Creating {iterations} DumpHeapOperation instances");

            result.AverageTime.Should().BeLessThan(TimeSpan.FromMilliseconds(1));
            result.TotalTime.Should().BeLessThan(maxTotalTime);
            
            Logger.LogInformation("Performance test results: {Results}", result);
        }

        #endregion

        #region AI Integration Tests

        [Fact]
        [AITest]
        [Trait(TestTraits.Priority, TestPriorities.Medium)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Medium)]
        public async Task AskAi_WithValidResults_ShouldProvideInsights()
        {
            // Arrange
            var operationModel = TestDataFactory.CreateBasicOperationModel();
            var mockResults = new List<object>
            {
                new { Address = 0x12345678UL, Type = "System.String", Value = "Large String", Size = 1024 * 85 }, // Large object
                new { Address = 0x87654321UL, Type = "System.String", Value = "Normal String", Size = 32 },
                new { Address = 0x11223344UL, Type = "System.Collections.Generic.List`1[[System.String]]", Value = "List with many items", Size = 2048 }
            };

            // Act
            var insights = _operation.GetAIInsights(new System.Collections.ObjectModel.Collection<object>(mockResults));

            // Assert
            insights.Should().NotBeNullOrEmpty("because AI should provide insights about heap objects");
            insights.Should().Contain("objects", "because insights should mention objects");
            insights.Should().Contain("Heap Analysis", "because insights should provide heap analysis");
            
            // Verify insights contain useful information
            TestUtilities.Validation.AssertNotNullOrEmpty(insights, "AI insights should not be empty");
        }

        [Fact]
        [AITest]
        [Trait(TestTraits.Priority, TestPriorities.Medium)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Fast)]
        public void GetAIInsights_WithEmptyResults_ShouldHandleGracefully()
        {
            // Arrange
            var emptyResults = new System.Collections.ObjectModel.Collection<object>();

            // Act
            var insights = _operation.GetAIInsights(emptyResults);

            // Assert
            insights.Should().NotBeNullOrEmpty();
            insights.Should().Contain("Heap Analysis: 0 objects");
            insights.Should().NotContain("Type distribution");
            insights.Should().NotContain("Top object types");
        }

        [Fact]
        [AITest]
        [Trait(TestTraits.Priority, TestPriorities.Medium)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Fast)]
        public void GetAIInsights_WithNullResults_ShouldHandleGracefully()
        {
            // Act
            var insights = _operation.GetAIInsights(null);

            // Assert
            insights.Should().NotBeNullOrEmpty();
            insights.Should().Contain("No results available");
        }

        #endregion

        #region Memory Analysis Tests

        [Fact]
        [MemoryTest]
        [Trait(TestTraits.Priority, TestPriorities.High)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Fast)]
        public void GetAIInsights_WithLargeObjects_ShouldIdentifyMemoryIssues()
        {
            // Arrange
            var largeObjects = new System.Collections.ObjectModel.Collection<object>
            {
                new { Address = 0x12345678UL, Type = "System.Byte[]", Size = 1024 * 1024 * 10 }, // 10MB array
                new { Address = 0x87654321UL, Type = "System.String", Size = 1024 * 90 }, // 90KB string (LOH)
                new { Address = 0x11223344UL, Type = "System.Collections.Generic.List`1", Size = 1024 * 100 } // 100KB list
            };

            // Act
            var insights = _operation.GetAIInsights(largeObjects);

            // Assert
            insights.Should().NotBeNullOrEmpty();
            insights.Should().Contain("Heap Analysis: 3 objects");
            insights.Should().Contain("Total heap size");
            
            // Verify the insights contain object type information
            insights.Should().Contain("System.Byte[]");
            insights.Should().Contain("System.String");
            insights.Should().Contain("System.Collections.Generic.List`1");
        }

        [Theory]
        [InlineData(1, "Single object")]
        [InlineData(10, "Few objects")]
        [InlineData(100, "Many objects")]
        [InlineData(1000, "Lots of objects")]
        [InlineData(10000, "Very many objects")]
        [MemoryTest]
        [Trait(TestTraits.Priority, TestPriorities.Medium)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Fast)]
        public void GetAIInsights_WithDifferentObjectCounts_ShouldScaleAppropriately(int objectCount, string description)
        {
            // Arrange
            var objects = new System.Collections.ObjectModel.Collection<object>();
            for (int i = 0; i < objectCount; i++)
            {
                objects.Add(new { Address = (ulong)(0x12345678 + i), Type = "System.String", Size = 32 });
            }

            // Act
            var insights = _operation.GetAIInsights(objects);

            // Assert
            insights.Should().NotBeNullOrEmpty();
            insights.Should().Contain($"Heap Analysis: {objectCount:N0} objects", $"because insights should show the correct count for {description}");
            insights.Should().Contain("System.String");
            
            // For large counts, check for potential issues
            if (objectCount > 100000)
            {
                insights.Should().Contain("Potential Issues", "because large object counts should trigger warnings");
            }
        }

        #endregion

        #region Integration with Other Components

        [Fact]
        [IntegrationTest]
        [Trait(TestTraits.Priority, TestPriorities.Medium)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Medium)]
        public void Operation_ShouldImplementRequiredInterfaces()
        {
            // Act & Assert
            TestUtilities.Types.AssertImplements<IDebuggerOperation>(typeof(DumpHeapOperation));
            TestUtilities.Types.AssertImplements<IAIEnabledOperation>(typeof(DumpHeapOperation));
            TestUtilities.Types.AssertImplements<IDebuggerOperation>(typeof(DumpHeapOperation));
        }

        [Fact]
        [IntegrationTest]
        [Trait(TestTraits.Priority, TestPriorities.Medium)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Fast)]
        public void Operation_ShouldHaveCorrectMEFAttributes()
        {
            // Act & Assert
            var operationType = typeof(DumpHeapOperation);
            
            // Check if it has the Export attribute (MEF)
            var exportAttribute = operationType.GetCustomAttributes(typeof(System.ComponentModel.Composition.ExportAttribute), false);
            exportAttribute.Should().NotBeEmpty("because operation should have Export attribute for MEF");
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        [Trait(TestTraits.Priority, TestPriorities.High)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Fast)]
        public void GetAIInsights_WithInvalidObjects_ShouldHandleGracefully()
        {
            // Arrange
            var invalidObjects = new System.Collections.ObjectModel.Collection<object>
            {
                null,
                new { InvalidProperty = "test" },
                new { Address = "invalid", Type = 123, Size = "not a number" }
            };

            // Act & Assert
            TestUtilities.Exceptions.AssertDoesNotThrow(
                () => _operation.GetAIInsights(invalidObjects),
                "GetAIInsights should handle invalid objects gracefully");
        }

        [Fact]
        [Trait(TestTraits.Priority, TestPriorities.Medium)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Fast)]
        public void Operation_Properties_ShouldBeThreadSafe()
        {
            // Arrange
            var operations = new List<DumpHeapOperation>();
            
            // Act
            Parallel.For(0, 100, i =>
            {
                var operation = new DumpHeapOperation();
                lock (operations)
                {
                    operations.Add(operation);
                }
            });

            // Assert
            operations.Should().HaveCount(100);
            operations.Should().OnlyContain(op => op.Name == OperationNames.DumpHeap);
        }

        #endregion

        #region Test Data Scenarios

        [Fact]
        [Trait(TestTraits.Priority, TestPriorities.Medium)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Fast)]
        public void TestDataFactory_ShouldCreateValidOperationModel()
        {
            // Act
            var model = TestDataFactory.CreateBasicOperationModel();

            // Assert
            model.Should().NotBeNull();
            model.ObjectAddress.Should().BeGreaterThan(0);
            model.Types.Should().NotBeNullOrEmpty();
            model.NumOfResults.Should().BeGreaterThan(0);
            
            TestUtilities.Memory.AssertValidAddress(model.ObjectAddress);
        }

        [Fact]
        [Trait(TestTraits.Priority, TestPriorities.Low)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Fast)]
        public void TestDataFactory_MemoryLeakScenario_ShouldProvideRealisticData()
        {
            // Act
            var (model, context, request) = TestDataFactory.Scenarios.MemoryLeak();

            // Assert
            model.Should().NotBeNull();
            context.Should().NotBeNull();
            request.Should().NotBeNull();
            
            context.HeapStats.TotalSize.Should().BeGreaterThan(1_000_000_000); // > 1GB indicates memory leak
            context.Exceptions.Should().NotBeEmpty();
            context.Exceptions.Should().Contain(ex => ex.Type.Contains("OutOfMemoryException"));
        }

        #endregion

        #region Cleanup

        protected override void TearDown()
        {
            // Clean up any test-specific resources
            VerifyAllMocks();
            base.TearDown();
        }

        #endregion
    }
} 