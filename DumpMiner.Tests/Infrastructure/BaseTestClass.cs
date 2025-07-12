using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace DumpMiner.Tests.Infrastructure
{
    /// <summary>
    /// Base test class providing comprehensive testing infrastructure, utilities, and common patterns
    /// for all unit tests in the DumpMiner project.
    /// </summary>
    /// <remarks>
    /// This class follows modern C# testing patterns and provides:
    /// - Consistent test lifecycle management
    /// - Comprehensive assertion utilities
    /// - Mock management and verification
    /// - Logging infrastructure for tests
    /// - Service provider management
    /// - Test data validation
    /// - Performance measurement utilities
    /// - Error handling patterns
    /// </remarks>
    public abstract class BaseTestClass : IDisposable
    {
        /// <summary>
        /// Test output helper for writing test diagnostic information
        /// </summary>
        protected ITestOutputHelper TestOutput { get; }

        /// <summary>
        /// Logger instance for test diagnostics
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Service provider for dependency injection in tests
        /// </summary>
        protected IServiceProvider ServiceProvider { get; private set; }

        /// <summary>
        /// Mock repository for managing all mocks in a test
        /// </summary>
        protected MockRepository MockRepository { get; private set; }

        /// <summary>
        /// Cancellation token source for test cancellation scenarios
        /// </summary>
        protected CancellationTokenSource CancellationTokenSource { get; private set; }

        /// <summary>
        /// Collection of all created mocks for verification
        /// </summary>
        protected List<Mock> CreatedMocks { get; private set; }

        /// <summary>
        /// Random instance for test data generation
        /// </summary>
        protected Random Random { get; private set; }

        /// <summary>
        /// Test execution start time for performance measurements
        /// </summary>
        protected DateTime TestStartTime { get; private set; }

        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the base test class
        /// </summary>
        /// <param name="testOutput">Test output helper for diagnostics</param>
        protected BaseTestClass(ITestOutputHelper testOutput)
        {
            TestOutput = testOutput ?? throw new ArgumentNullException(nameof(testOutput));
            TestStartTime = DateTime.UtcNow;
            
            // Initialize test infrastructure
            MockRepository = new MockRepository(MockBehavior.Strict);
            CreatedMocks = new List<Mock>();
            CancellationTokenSource = new CancellationTokenSource();
            Random = new Random(42); // Fixed seed for reproducible tests
            
            // Set up logging
            Logger = CreateTestLogger();
            
            // Set up service provider
            ServiceProvider = CreateServiceProvider();
            
            Logger.LogInformation("Test initialized: {TestName}", GetType().Name);
        }

        #region Service Provider Management

        /// <summary>
        /// Creates a test-specific service provider with common services
        /// </summary>
        protected virtual IServiceProvider CreateServiceProvider()
        {
            var services = new ServiceCollection();
            
            // Add logging
            services.AddLogging(builder => builder.AddProvider(new TestLoggerProvider(TestOutput)));
            
            // Add common test services
            ConfigureTestServices(services);
            
            return services.BuildServiceProvider();
        }

        /// <summary>
        /// Override this method to configure test-specific services
        /// </summary>
        /// <param name="services">Service collection to configure</param>
        protected virtual void ConfigureTestServices(IServiceCollection services)
        {
            // Default implementation - no additional services
        }

        /// <summary>
        /// Gets a service from the test service provider
        /// </summary>
        /// <typeparam name="T">Service type</typeparam>
        /// <returns>Service instance</returns>
        protected T GetService<T>() where T : class
        {
            return ServiceProvider.GetService<T>();
        }

        /// <summary>
        /// Gets a required service from the test service provider
        /// </summary>
        /// <typeparam name="T">Service type</typeparam>
        /// <returns>Service instance</returns>
        protected T GetRequiredService<T>() where T : class
        {
            return ServiceProvider.GetRequiredService<T>();
        }

        #endregion

        #region Mock Management

        /// <summary>
        /// Creates a mock of the specified type with strict behavior
        /// </summary>
        /// <typeparam name="T">Type to mock</typeparam>
        /// <returns>Mock instance</returns>
        protected Mock<T> CreateMock<T>() where T : class
        {
            var mock = MockRepository.Create<T>();
            CreatedMocks.Add(mock);
            return mock;
        }

        /// <summary>
        /// Creates a mock of the specified type with loose behavior
        /// </summary>
        /// <typeparam name="T">Type to mock</typeparam>
        /// <returns>Mock instance</returns>
        protected Mock<T> CreateLooseMock<T>() where T : class
        {
            var mock = new Mock<T>(MockBehavior.Loose);
            CreatedMocks.Add(mock);
            return mock;
        }

        /// <summary>
        /// Creates a mock logger for the specified type
        /// </summary>
        /// <typeparam name="T">Type for logger</typeparam>
        /// <returns>Mock logger</returns>
        protected Mock<ILogger<T>> CreateMockLogger<T>()
        {
            return CreateLooseMock<ILogger<T>>();
        }

        /// <summary>
        /// Verifies all created mocks
        /// </summary>
        protected void VerifyAllMocks()
        {
            foreach (var mock in CreatedMocks)
            {
                mock.Verify();
            }
        }

        /// <summary>
        /// Verifies no additional calls were made on any mock
        /// </summary>
        protected void VerifyNoOtherCalls()
        {
            foreach (var mock in CreatedMocks)
            {
                mock.VerifyNoOtherCalls();
            }
        }

        #endregion

        #region Test Data Generation

        /// <summary>
        /// Generates a random string of specified length
        /// </summary>
        /// <param name="length">String length</param>
        /// <returns>Random string</returns>
        protected string GenerateRandomString(int length = 10)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[Random.Next(s.Length)]).ToArray());
        }

        /// <summary>
        /// Generates a random email address
        /// </summary>
        /// <returns>Random email</returns>
        protected string GenerateRandomEmail()
        {
            return $"{GenerateRandomString(5).ToLower()}@{GenerateRandomString(5).ToLower()}.com";
        }

        /// <summary>
        /// Generates a random integer within specified range
        /// </summary>
        /// <param name="min">Minimum value</param>
        /// <param name="max">Maximum value</param>
        /// <returns>Random integer</returns>
        protected int GenerateRandomInt(int min = 1, int max = 1000)
        {
            return Random.Next(min, max);
        }

        /// <summary>
        /// Generates a random long within specified range
        /// </summary>
        /// <param name="min">Minimum value</param>
        /// <param name="max">Maximum value</param>
        /// <returns>Random long</returns>
        protected long GenerateRandomLong(long min = 1, long max = 1000000)
        {
            return Random.NextInt64(min, max);
        }

        /// <summary>
        /// Generates a random memory address
        /// </summary>
        /// <returns>Random memory address</returns>
        protected ulong GenerateRandomAddress()
        {
            return (ulong)Random.NextInt64(0x100000, 0x7FFFFFFF);
        }

        /// <summary>
        /// Generates a random GUID
        /// </summary>
        /// <returns>Random GUID</returns>
        protected Guid GenerateRandomGuid()
        {
            return Guid.NewGuid();
        }

        #endregion

        #region Assertion Utilities

        /// <summary>
        /// Asserts that an action throws an exception of the specified type
        /// </summary>
        /// <typeparam name="TException">Expected exception type</typeparam>
        /// <param name="action">Action to execute</param>
        /// <param name="expectedMessage">Expected exception message (optional)</param>
        /// <returns>The thrown exception</returns>
        protected TException AssertThrows<TException>(Action action, string expectedMessage = null)
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
        /// Asserts that an async action throws an exception of the specified type
        /// </summary>
        /// <typeparam name="TException">Expected exception type</typeparam>
        /// <param name="action">Async action to execute</param>
        /// <param name="expectedMessage">Expected exception message (optional)</param>
        /// <returns>The thrown exception</returns>
        protected async Task<TException> AssertThrowsAsync<TException>(Func<Task> action, string expectedMessage = null)
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
        /// Asserts that a collection contains exactly the expected items
        /// </summary>
        /// <typeparam name="T">Item type</typeparam>
        /// <param name="collection">Collection to check</param>
        /// <param name="expectedItems">Expected items</param>
        protected void AssertCollectionEquals<T>(IEnumerable<T> collection, params T[] expectedItems)
        {
            collection.Should().BeEquivalentTo(expectedItems);
        }

        /// <summary>
        /// Asserts that a value is within the specified range
        /// </summary>
        /// <param name="value">Value to check</param>
        /// <param name="min">Minimum value</param>
        /// <param name="max">Maximum value</param>
        protected void AssertInRange(int value, int min, int max)
        {
            value.Should().BeInRange(min, max);
        }

        /// <summary>
        /// Asserts that a value is within the specified range
        /// </summary>
        /// <param name="value">Value to check</param>
        /// <param name="min">Minimum value</param>
        /// <param name="max">Maximum value</param>
        protected void AssertInRange(long value, long min, long max)
        {
            value.Should().BeInRange(min, max);
        }

        /// <summary>
        /// Asserts that a value is within the specified range
        /// </summary>
        /// <param name="value">Value to check</param>
        /// <param name="min">Minimum value</param>
        /// <param name="max">Maximum value</param>
        protected void AssertInRange(decimal value, decimal min, decimal max)
        {
            value.Should().BeInRange(min, max);
        }

        #endregion

        #region Performance Measurement

        /// <summary>
        /// Measures the execution time of an action
        /// </summary>
        /// <param name="action">Action to measure</param>
        /// <param name="description">Description of the action</param>
        /// <returns>Elapsed time</returns>
        protected TimeSpan MeasureExecutionTime(Action action, string description = null)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            
            var elapsed = stopwatch.Elapsed;
            Logger.LogInformation("Execution time for {Description}: {Elapsed}ms", 
                description ?? "action", elapsed.TotalMilliseconds);
            
            return elapsed;
        }

        /// <summary>
        /// Measures the execution time of an async action
        /// </summary>
        /// <param name="action">Async action to measure</param>
        /// <param name="description">Description of the action</param>
        /// <returns>Elapsed time</returns>
        protected async Task<TimeSpan> MeasureExecutionTimeAsync(Func<Task> action, string description = null)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await action();
            stopwatch.Stop();
            
            var elapsed = stopwatch.Elapsed;
            Logger.LogInformation("Execution time for {Description}: {Elapsed}ms", 
                description ?? "action", elapsed.TotalMilliseconds);
            
            return elapsed;
        }

        /// <summary>
        /// Asserts that an action completes within the specified timeout
        /// </summary>
        /// <param name="action">Action to execute</param>
        /// <param name="timeout">Maximum allowed execution time</param>
        /// <param name="description">Description of the action</param>
        protected void AssertExecutionTime(Action action, TimeSpan timeout, string description = null)
        {
            var elapsed = MeasureExecutionTime(action, description);
            elapsed.Should().BeLessOrEqualTo(timeout, 
                $"because {description ?? "action"} should complete within {timeout.TotalMilliseconds}ms");
        }

        /// <summary>
        /// Asserts that an async action completes within the specified timeout
        /// </summary>
        /// <param name="action">Async action to execute</param>
        /// <param name="timeout">Maximum allowed execution time</param>
        /// <param name="description">Description of the action</param>
        protected async Task AssertExecutionTimeAsync(Func<Task> action, TimeSpan timeout, string description = null)
        {
            var elapsed = await MeasureExecutionTimeAsync(action, description);
            elapsed.Should().BeLessOrEqualTo(timeout, 
                $"because {description ?? "action"} should complete within {timeout.TotalMilliseconds}ms");
        }

        #endregion

        #region Test Lifecycle

        /// <summary>
        /// Called before each test method
        /// Override this method to set up test-specific state
        /// </summary>
        protected virtual void SetUp()
        {
            // Default implementation - no setup
        }

        /// <summary>
        /// Called after each test method
        /// Override this method to clean up test-specific state
        /// </summary>
        protected virtual void TearDown()
        {
            // Default implementation - no teardown
        }

        /// <summary>
        /// Gets test execution statistics
        /// </summary>
        /// <returns>Test statistics</returns>
        protected TestExecutionStats GetTestStats()
        {
            return new TestExecutionStats
            {
                TestName = GetType().Name,
                StartTime = TestStartTime,
                EndTime = DateTime.UtcNow,
                ExecutionTime = DateTime.UtcNow - TestStartTime,
                MocksCreated = CreatedMocks.Count,
                ServicesRegistered = ServiceProvider.GetServices<object>().Count()
            };
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Creates a test logger that writes to the test output
        /// </summary>
        /// <returns>Test logger instance</returns>
        private ILogger CreateTestLogger()
        {
            return new TestLogger(TestOutput, GetType().Name);
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes of test resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of test resources
        /// </summary>
        /// <param name="disposing">Whether disposing from user code</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    TearDown();
                    
                    var stats = GetTestStats();
                    Logger.LogInformation("Test completed: {TestName} in {ExecutionTime}ms", 
                        stats.TestName, stats.ExecutionTime.TotalMilliseconds);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error during test cleanup");
                }
                finally
                {
                    CancellationTokenSource?.Dispose();
                    ServiceProvider?.Dispose();
                    MockRepository?.Dispose();
                    
                    _disposed = true;
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Test execution statistics
    /// </summary>
    public class TestExecutionStats
    {
        public string TestName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public int MocksCreated { get; set; }
        public int ServicesRegistered { get; set; }
    }

    /// <summary>
    /// Test logger that writes to test output
    /// </summary>
    public class TestLogger : ILogger
    {
        private readonly ITestOutputHelper _testOutput;
        private readonly string _categoryName;

        public TestLogger(ITestOutputHelper testOutput, string categoryName)
        {
            _testOutput = testOutput;
            _categoryName = categoryName;
        }

        public IDisposable BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var message = formatter(state, exception);
            _testOutput.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{logLevel}] [{_categoryName}] {message}");
            
            if (exception != null)
            {
                _testOutput.WriteLine($"Exception: {exception}");
            }
        }
    }

    /// <summary>
    /// Test logger provider for dependency injection
    /// </summary>
    public class TestLoggerProvider : ILoggerProvider
    {
        private readonly ITestOutputHelper _testOutput;

        public TestLoggerProvider(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new TestLogger(_testOutput, categoryName);
        }

        public void Dispose()
        {
            // No resources to dispose
        }
    }
} 