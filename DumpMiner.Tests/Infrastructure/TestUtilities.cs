using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace DumpMiner.Tests.Infrastructure
{
    /// <summary>
    /// Comprehensive test utilities for common testing scenarios
    /// </summary>
    public static class TestUtilities
    {
        /// <summary>
        /// Async testing utilities
        /// </summary>
        public static class Async
        {
            /// <summary>
            /// Executes an async action and verifies it completes within timeout
            /// </summary>
            public static async Task<T> ExecuteWithTimeout<T>(Func<Task<T>> action, TimeSpan timeout, string description = null)
            {
                using var cts = new CancellationTokenSource(timeout);
                try
                {
                    return await action();
                }
                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                {
                    throw new TimeoutException($"Operation timed out after {timeout.TotalMilliseconds}ms: {description ?? "async operation"}");
                }
            }

            /// <summary>
            /// Executes an async action and verifies it completes within timeout
            /// </summary>
            public static async Task ExecuteWithTimeout(Func<Task> action, TimeSpan timeout, string description = null)
            {
                using var cts = new CancellationTokenSource(timeout);
                try
                {
                    await action();
                }
                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                {
                    throw new TimeoutException($"Operation timed out after {timeout.TotalMilliseconds}ms: {description ?? "async operation"}");
                }
            }

            /// <summary>
            /// Waits for a condition to be true within a timeout period
            /// </summary>
            public static async Task WaitForCondition(Func<bool> condition, TimeSpan timeout, TimeSpan pollInterval = default)
            {
                if (pollInterval == default)
                    pollInterval = TimeSpan.FromMilliseconds(100);

                var deadline = DateTime.UtcNow.Add(timeout);
                
                while (DateTime.UtcNow < deadline)
                {
                    if (condition())
                        return;
                    
                    await Task.Delay(pollInterval);
                }
                
                throw new TimeoutException($"Condition was not met within {timeout.TotalMilliseconds}ms");
            }

            /// <summary>
            /// Waits for an async condition to be true within a timeout period
            /// </summary>
            public static async Task WaitForCondition(Func<Task<bool>> condition, TimeSpan timeout, TimeSpan pollInterval = default)
            {
                if (pollInterval == default)
                    pollInterval = TimeSpan.FromMilliseconds(100);

                var deadline = DateTime.UtcNow.Add(timeout);
                
                while (DateTime.UtcNow < deadline)
                {
                    if (await condition())
                        return;
                    
                    await Task.Delay(pollInterval);
                }
                
                throw new TimeoutException($"Async condition was not met within {timeout.TotalMilliseconds}ms");
            }

            /// <summary>
            /// Verifies that an async operation throws a specific exception
            /// </summary>
            public static async Task<TException> AssertThrowsAsync<TException>(Func<Task> action, string expectedMessage = null)
                where TException : Exception
            {
                var exception = await Assert.ThrowsAsync<TException>(action);
                
                if (!string.IsNullOrEmpty(expectedMessage))
                {
                    exception.Message.Should().Contain(expectedMessage);
                }
                
                return exception;
            }

            /// <summary>
            /// Verifies that an async operation does not throw any exception
            /// </summary>
            public static async Task AssertDoesNotThrowAsync(Func<Task> action)
            {
                var exception = await Record.ExceptionAsync(action);
                exception.Should().BeNull();
            }

            /// <summary>
            /// Runs multiple async operations concurrently and waits for all to complete
            /// </summary>
            public static async Task<T[]> RunConcurrentlyAsync<T>(params Func<Task<T>>[] actions)
            {
                var tasks = actions.Select(action => Task.Run(action)).ToArray();
                return await Task.WhenAll(tasks);
            }

            /// <summary>
            /// Runs multiple async operations concurrently and waits for all to complete
            /// </summary>
            public static async Task RunConcurrentlyAsync(params Func<Task>[] actions)
            {
                var tasks = actions.Select(action => Task.Run(action)).ToArray();
                await Task.WhenAll(tasks);
            }
        }

        /// <summary>
        /// File and directory utilities for testing
        /// </summary>
        public static class Files
        {
            /// <summary>
            /// Creates a temporary directory for testing
            /// </summary>
            public static string CreateTempDirectory()
            {
                var tempPath = Path.Combine(Path.GetTempPath(), $"DumpMinerTests_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempPath);
                return tempPath;
            }

            /// <summary>
            /// Creates a temporary file with the specified content
            /// </summary>
            public static string CreateTempFile(string content = null, string extension = ".txt")
            {
                var tempPath = Path.Combine(Path.GetTempPath(), $"DumpMinerTest_{Guid.NewGuid():N}{extension}");
                File.WriteAllText(tempPath, content ?? "Test content");
                return tempPath;
            }

            /// <summary>
            /// Creates a temporary JSON file with the specified content
            /// </summary>
            public static string CreateTempJsonFile(object content)
            {
                var jsonContent = System.Text.Json.JsonSerializer.Serialize(content, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                return CreateTempFile(jsonContent, ".json");
            }

            /// <summary>
            /// Safely deletes a file or directory, ignoring errors
            /// </summary>
            public static void SafeDelete(string path)
            {
                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                    else if (Directory.Exists(path))
                        Directory.Delete(path, true);
                }
                catch
                {
                    // Ignore deletion errors in tests
                }
            }

            /// <summary>
            /// Creates a disposable temporary file
            /// </summary>
            public static IDisposable CreateDisposableTempFile(string content = null, string extension = ".txt", out string filePath)
            {
                filePath = CreateTempFile(content, extension);
                return new DisposableFile(filePath);
            }

            /// <summary>
            /// Creates a disposable temporary directory
            /// </summary>
            public static IDisposable CreateDisposableTempDirectory(out string directoryPath)
            {
                directoryPath = CreateTempDirectory();
                return new DisposableDirectory(directoryPath);
            }

            private class DisposableFile : IDisposable
            {
                private readonly string _filePath;
                
                public DisposableFile(string filePath)
                {
                    _filePath = filePath;
                }
                
                public void Dispose()
                {
                    SafeDelete(_filePath);
                }
            }

            private class DisposableDirectory : IDisposable
            {
                private readonly string _directoryPath;
                
                public DisposableDirectory(string directoryPath)
                {
                    _directoryPath = directoryPath;
                }
                
                public void Dispose()
                {
                    SafeDelete(_directoryPath);
                }
            }
        }

        /// <summary>
        /// Collection testing utilities
        /// </summary>
        public static class Collections
        {
            /// <summary>
            /// Verifies that a collection has the expected count
            /// </summary>
            public static void AssertCount<T>(IEnumerable<T> collection, int expectedCount)
            {
                collection.Should().HaveCount(expectedCount);
            }

            /// <summary>
            /// Verifies that a collection contains specific items
            /// </summary>
            public static void AssertContains<T>(IEnumerable<T> collection, params T[] expectedItems)
            {
                foreach (var item in expectedItems)
                {
                    collection.Should().Contain(item);
                }
            }

            /// <summary>
            /// Verifies that a collection does not contain specific items
            /// </summary>
            public static void AssertDoesNotContain<T>(IEnumerable<T> collection, params T[] unexpectedItems)
            {
                foreach (var item in unexpectedItems)
                {
                    collection.Should().NotContain(item);
                }
            }

            /// <summary>
            /// Verifies that a collection is ordered by a specific property
            /// </summary>
            public static void AssertOrdered<T, TKey>(IEnumerable<T> collection, Func<T, TKey> keySelector, bool ascending = true)
                where TKey : IComparable<TKey>
            {
                var list = collection.ToList();
                if (list.Count <= 1) return;

                for (int i = 1; i < list.Count; i++)
                {
                    var current = keySelector(list[i]);
                    var previous = keySelector(list[i - 1]);
                    
                    if (ascending)
                    {
                        current.Should().BeGreaterOrEqualTo(previous);
                    }
                    else
                    {
                        current.Should().BeLessOrEqualTo(previous);
                    }
                }
            }

            /// <summary>
            /// Verifies that all items in a collection match a predicate
            /// </summary>
            public static void AssertAll<T>(IEnumerable<T> collection, Func<T, bool> predicate, string description = null)
            {
                collection.Should().AllSatisfy(item => predicate(item).Should().BeTrue(description));
            }

            /// <summary>
            /// Verifies that at least one item in a collection matches a predicate
            /// </summary>
            public static void AssertAny<T>(IEnumerable<T> collection, Func<T, bool> predicate, string description = null)
            {
                collection.Should().Contain(item => predicate(item), description);
            }

            /// <summary>
            /// Verifies that no items in a collection match a predicate
            /// </summary>
            public static void AssertNone<T>(IEnumerable<T> collection, Func<T, bool> predicate, string description = null)
            {
                collection.Should().NotContain(item => predicate(item), description);
            }
        }

        /// <summary>
        /// Performance testing utilities
        /// </summary>
        public static class Performance
        {
            /// <summary>
            /// Measures execution time and verifies it's within acceptable bounds
            /// </summary>
            public static TimeSpan MeasureAndAssert(Action action, TimeSpan maxDuration, string description = null)
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                action();
                stopwatch.Stop();
                
                var elapsed = stopwatch.Elapsed;
                elapsed.Should().BeLessOrEqualTo(maxDuration, 
                    $"because {description ?? "operation"} should complete within {maxDuration.TotalMilliseconds}ms");
                
                return elapsed;
            }

            /// <summary>
            /// Measures async execution time and verifies it's within acceptable bounds
            /// </summary>
            public static async Task<TimeSpan> MeasureAndAssertAsync(Func<Task> action, TimeSpan maxDuration, string description = null)
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                await action();
                stopwatch.Stop();
                
                var elapsed = stopwatch.Elapsed;
                elapsed.Should().BeLessOrEqualTo(maxDuration, 
                    $"because {description ?? "operation"} should complete within {maxDuration.TotalMilliseconds}ms");
                
                return elapsed;
            }

            /// <summary>
            /// Runs a performance test with multiple iterations
            /// </summary>
            public static PerformanceResult RunPerformanceTest(Action action, int iterations = 100, string description = null)
            {
                var times = new List<TimeSpan>();
                
                for (int i = 0; i < iterations; i++)
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    action();
                    stopwatch.Stop();
                    times.Add(stopwatch.Elapsed);
                }
                
                return new PerformanceResult
                {
                    Description = description ?? "performance test",
                    Iterations = iterations,
                    MinTime = times.Min(),
                    MaxTime = times.Max(),
                    AverageTime = TimeSpan.FromTicks((long)times.Average(t => t.Ticks)),
                    MedianTime = times.OrderBy(t => t.Ticks).Skip(iterations / 2).First(),
                    TotalTime = TimeSpan.FromTicks(times.Sum(t => t.Ticks))
                };
            }

            /// <summary>
            /// Runs an async performance test with multiple iterations
            /// </summary>
            public static async Task<PerformanceResult> RunPerformanceTestAsync(Func<Task> action, int iterations = 100, string description = null)
            {
                var times = new List<TimeSpan>();
                
                for (int i = 0; i < iterations; i++)
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    await action();
                    stopwatch.Stop();
                    times.Add(stopwatch.Elapsed);
                }
                
                return new PerformanceResult
                {
                    Description = description ?? "async performance test",
                    Iterations = iterations,
                    MinTime = times.Min(),
                    MaxTime = times.Max(),
                    AverageTime = TimeSpan.FromTicks((long)times.Average(t => t.Ticks)),
                    MedianTime = times.OrderBy(t => t.Ticks).Skip(iterations / 2).First(),
                    TotalTime = TimeSpan.FromTicks(times.Sum(t => t.Ticks))
                };
            }
        }

        /// <summary>
        /// Validation testing utilities
        /// </summary>
        public static class Validation
        {
            /// <summary>
            /// Validates that an object meets specific criteria
            /// </summary>
            public static void AssertValid<T>(T obj, Func<T, bool> validator, string description = null)
            {
                validator(obj).Should().BeTrue(description ?? "object should be valid");
            }

            /// <summary>
            /// Validates that an object's properties have expected values
            /// </summary>
            public static void AssertProperties<T>(T obj, object expectedProperties)
            {
                obj.Should().BeEquivalentTo(expectedProperties, options => options.ExcludingMissingMembers());
            }

            /// <summary>
            /// Validates that a string matches a specific pattern
            /// </summary>
            public static void AssertMatches(string actual, string pattern, string description = null)
            {
                actual.Should().MatchRegex(pattern, description ?? "string should match pattern");
            }

            /// <summary>
            /// Validates that a number is within a specific range
            /// </summary>
            public static void AssertInRange<T>(T value, T min, T max, string description = null) 
                where T : IComparable<T>
            {
                value.Should().BeInRange(min, max, description ?? "value should be in range");
            }

            /// <summary>
            /// Validates that a value is not null or empty
            /// </summary>
            public static void AssertNotNullOrEmpty(string value, string description = null)
            {
                value.Should().NotBeNullOrEmpty(description ?? "value should not be null or empty");
            }

            /// <summary>
            /// Validates that a collection is not null or empty
            /// </summary>
            public static void AssertNotNullOrEmpty<T>(IEnumerable<T> collection, string description = null)
            {
                collection.Should().NotBeNullOrEmpty(description ?? "collection should not be null or empty");
            }
        }

        /// <summary>
        /// Memory address testing utilities
        /// </summary>
        public static class Memory
        {
            /// <summary>
            /// Generates a valid-looking memory address
            /// </summary>
            public static ulong GenerateAddress(bool highBit = false)
            {
                var random = new Random();
                var address = (ulong)random.Next(0x100000, 0x7FFFFFFF);
                
                if (highBit)
                {
                    address |= 0x8000000000000000UL;
                }
                
                return address;
            }

            /// <summary>
            /// Validates that an address looks reasonable
            /// </summary>
            public static void AssertValidAddress(ulong address, string description = null)
            {
                address.Should().BeGreaterThan(0x1000UL, description ?? "address should be greater than 0x1000");
                address.Should().BeLessThan(0x8000000000000000UL, description ?? "address should be less than 0x8000000000000000");
            }

            /// <summary>
            /// Validates that an address is aligned to a specific boundary
            /// </summary>
            public static void AssertAligned(ulong address, int alignment, string description = null)
            {
                (address % (ulong)alignment).Should().Be(0UL, description ?? $"address should be aligned to {alignment} bytes");
            }
        }

        /// <summary>
        /// Exception testing utilities
        /// </summary>
        public static class Exceptions
        {
            /// <summary>
            /// Verifies that an action throws a specific exception type
            /// </summary>
            public static TException AssertThrows<TException>(Action action, string expectedMessage = null)
                where TException : Exception
            {
                var exception = Assert.Throws<TException>(action);
                
                if (!string.IsNullOrEmpty(expectedMessage))
                {
                    exception.Message.Should().Contain(expectedMessage);
                }
                
                return exception;
            }

            /// <summary>
            /// Verifies that an action does not throw any exception
            /// </summary>
            public static void AssertDoesNotThrow(Action action, string description = null)
            {
                var exception = Record.Exception(action);
                exception.Should().BeNull(description ?? "action should not throw an exception");
            }

            /// <summary>
            /// Verifies that an action throws any exception
            /// </summary>
            public static Exception AssertThrowsAny(Action action, string description = null)
            {
                var exception = Record.Exception(action);
                exception.Should().NotBeNull(description ?? "action should throw an exception");
                return exception;
            }
        }

        /// <summary>
        /// Type testing utilities
        /// </summary>
        public static class Types
        {
            /// <summary>
            /// Verifies that a type implements a specific interface
            /// </summary>
            public static void AssertImplements<TInterface>(Type type)
            {
                type.Should().Implement<TInterface>();
            }

            /// <summary>
            /// Verifies that a type has a specific attribute
            /// </summary>
            public static void AssertHasAttribute<TAttribute>(Type type) where TAttribute : Attribute
            {
                type.Should().BeDecoratedWith<TAttribute>();
            }

            /// <summary>
            /// Verifies that a type is assignable from another type
            /// </summary>
            public static void AssertAssignableFrom<TBase>(Type derivedType)
            {
                typeof(TBase).Should().BeAssignableFrom(derivedType);
            }
        }
    }

    /// <summary>
    /// Performance test result
    /// </summary>
    public class PerformanceResult
    {
        public string Description { get; set; }
        public int Iterations { get; set; }
        public TimeSpan MinTime { get; set; }
        public TimeSpan MaxTime { get; set; }
        public TimeSpan AverageTime { get; set; }
        public TimeSpan MedianTime { get; set; }
        public TimeSpan TotalTime { get; set; }

        public override string ToString()
        {
            return $"{Description}: {Iterations} iterations, Avg: {AverageTime.TotalMilliseconds:F2}ms, " +
                   $"Min: {MinTime.TotalMilliseconds:F2}ms, Max: {MaxTime.TotalMilliseconds:F2}ms";
        }
    }
} 