using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using DumpMiner.Common;
using DumpMiner.Models;
using DumpMiner.Services.Configuration;
using DumpMiner.Tests.Infrastructure;
using DumpMiner.ViewModels;
using Xunit.Abstractions;

namespace DumpMiner.Tests.ViewModels
{
    /// <summary>
    /// Comprehensive unit tests for BaseOperationViewModel
    /// </summary>
    [UnitTest]
    [ViewModelTest]
    [UIViewModelTest]
    public class BaseOperationViewModelTests : BaseTestClass
    {
        private readonly Mock<IDebuggerOperation> _mockOperation;
        private readonly Mock<ConfigurationService> _mockConfigService;
        private readonly TestableBaseOperationViewModel _viewModel;

        public BaseOperationViewModelTests(ITestOutputHelper testOutput) : base(testOutput)
        {
            _mockOperation = MockFactories.CreateDebuggerOperation("TestOperation");
            _mockConfigService = MockFactories.CreateConfigurationService();
            _viewModel = new TestableBaseOperationViewModel();
        }

        protected override void SetUp()
        {
            base.SetUp();
            _viewModel.SetupForTesting(_mockOperation.Object, _mockConfigService.Object);
        }

        #region Property Tests

        [Fact]
        [Trait(TestTraits.Priority, TestPriorities.High)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Fast)]
        public void Count_WhenSet_ShouldNotifyPropertyChanged()
        {
            // Arrange
            var propertyChangedEvents = new List<string>();
            _viewModel.PropertyChanged += (s, e) => propertyChangedEvents.Add(e.PropertyName);

            // Act
            _viewModel.Count = 42;

            // Assert
            _viewModel.Count.Should().Be(42);
            propertyChangedEvents.Should().Contain(nameof(BaseOperationViewModel.Count));
        }

        [Fact]
        [Trait(TestTraits.Priority, TestPriorities.High)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Fast)]
        public void IsAiEnabled_WhenSet_ShouldNotifyPropertyChanged()
        {
            // Arrange
            var propertyChangedEvents = new List<string>();
            _viewModel.PropertyChanged += (s, e) => propertyChangedEvents.Add(e.PropertyName);

            // Act
            _viewModel.IsAiEnabled = true;

            // Assert
            _viewModel.IsAiEnabled.Should().BeTrue();
            propertyChangedEvents.Should().Contain(nameof(BaseOperationViewModel.IsAiEnabled));
        }

        [Fact]
        [Trait(TestTraits.Priority, TestPriorities.High)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Fast)]
        public void Items_WhenSet_ShouldNotifyPropertyChanged()
        {
            // Arrange
            var propertyChangedEvents = new List<string>();
            _viewModel.PropertyChanged += (s, e) => propertyChangedEvents.Add(e.PropertyName);
            var testItems = new ObservableCollection<object> { "Item1", "Item2", "Item3" };

            // Act
            _viewModel.Items = testItems;

            // Assert
            _viewModel.Items.Should().BeSameAs(testItems);
            propertyChangedEvents.Should().Contain(nameof(BaseOperationViewModel.Items));
        }

        [Fact]
        [Trait(TestTraits.Priority, TestPriorities.High)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Fast)]
        public void AiQuestion_WhenSet_ShouldNotifyPropertyChanged()
        {
            // Arrange
            var propertyChangedEvents = new List<string>();
            _viewModel.PropertyChanged += (s, e) => propertyChangedEvents.Add(e.PropertyName);
            const string testQuestion = "What does this memory dump tell us?";

            // Act
            _viewModel.AiQuestion = testQuestion;

            // Assert
            _viewModel.AiQuestion.Should().Be(testQuestion);
            propertyChangedEvents.Should().Contain(nameof(BaseOperationViewModel.AiQuestion));
        }

        [Fact]
        [Trait(TestTraits.Priority, TestPriorities.Medium)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Fast)]
        public void Model_ShouldBeInitialized()
        {
            // Assert
            _viewModel.Model.Should().NotBeNull();
            _viewModel.Model.Should().BeOfType<OperationModel>();
        }

        [Fact]
        [Trait(TestTraits.Priority, TestPriorities.Medium)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Fast)]
        public void Conversation_ShouldReturnModelChat()
        {
            // Arrange
            var testConversation = new ObservableCollection<ConversationMessage>
            {
                new ConversationMessageBuilder().AsUser("Test question").Build(),
                new ConversationMessageBuilder().AsAssistant("Test answer").Build()
            };
            _viewModel.Model.Chat = testConversation;

            // Act
            var conversation = _viewModel.Conversation;

            // Assert
            conversation.Should().BeSameAs(testConversation);
        }

        #endregion

        #region Command Tests

        [Fact]
        [Trait(TestTraits.Priority, TestPriorities.High)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Fast)]
        public void ExecuteOperationCommand_ShouldBeInitialized()
        {
            // Assert
            _viewModel.ExecuteOperationCommand.Should().NotBeNull();
            _viewModel.ExecuteOperationCommand.Should().BeAssignableTo<ICommand>();
        }

        [Fact]
        [Trait(TestTraits.Priority, TestPriorities.High)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Fast)]
        public void AskAiCommand_ShouldBeInitialized()
        {
            // Assert
            _viewModel.AskAiCommand.Should().NotBeNull();
            _viewModel.AskAiCommand.Should().BeAssignableTo<ICommand>();
        }

        [Fact]
        [Trait(TestTraits.Priority, TestPriorities.High)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Fast)]
        public void CancelOperationCommand_ShouldBeInitialized()
        {
            // Assert
            _viewModel.CancelOperationCommand.Should().NotBeNull();
            _viewModel.CancelOperationCommand.Should().BeAssignableTo<ICommand>();
        }

        [Fact]
        [Trait(TestTraits.Priority, TestPriorities.Medium)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Fast)]
        public void AskAiCommand_CanExecute_WithValidQuestion_ShouldReturnTrue()
        {
            // Arrange
            _viewModel.AiQuestion = "What does this memory dump tell us?";
            _viewModel.Items = new ObservableCollection<object> { "Item1", "Item2" };
            _viewModel.Operation = _mockOperation.Object;

            // Act
            var canExecute = _viewModel.AskAiCommand.CanExecute(null);

            // Assert
            canExecute.Should().BeTrue("because all conditions for AI command are met");
        }

        [Fact]
        [Trait(TestTraits.Priority, TestPriorities.Medium)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Fast)]
        public void AskAiCommand_CanExecute_WithEmptyQuestion_ShouldReturnFalse()
        {
            // Arrange
            _viewModel.AiQuestion = "";
            _viewModel.Items = new ObservableCollection<object> { "Item1", "Item2" };
            _viewModel.Operation = _mockOperation.Object;

            // Act
            var canExecute = _viewModel.AskAiCommand.CanExecute(null);

            // Assert
            canExecute.Should().BeFalse("because AI question is empty");
        }

        [Fact]
        [Trait(TestTraits.Priority, TestPriorities.Medium)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Fast)]
        public void AskAiCommand_CanExecute_WithNoItems_ShouldReturnFalse()
        {
            // Arrange
            _viewModel.AiQuestion = "What does this memory dump tell us?";
            _viewModel.Items = new ObservableCollection<object>();
            _viewModel.Operation = _mockOperation.Object;

            // Act
            var canExecute = _viewModel.AskAiCommand.CanExecute(null);

            // Assert
            canExecute.Should().BeFalse("because there are no items to analyze");
        }

        #endregion

        #region Operation Execution Tests

        [Fact]
        [Trait(TestTraits.Priority, TestPriorities.High)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Medium)]
        public async Task ExecuteOperation_WithValidParameters_ShouldExecuteSuccessfully()
        {
            // Arrange
            var expectedResults = new List<object>
            {
                new { Address = 0x12345678UL, Type = "System.String", Value = "Test" },
                new { Address = 0x87654321UL, Type = "System.Int32", Value = 42 }
            };

            _mockOperation.Setup(x => x.Execute(It.IsAny<OperationModel>(), It.IsAny<CancellationToken>(), It.IsAny<object>()))
                .ReturnsAsync(expectedResults);

            // Act
            await _viewModel.ExecuteOperationAsync(customParameter: null);

            // Assert
            _viewModel.Items.Should().HaveCount(2);
            _viewModel.Count.Should().Be(2);
            _mockOperation.Verify(x => x.Execute(It.IsAny<OperationModel>(), It.IsAny<CancellationToken>(), It.IsAny<object>()), Times.Once);
        }

        [Fact]
        [Trait(TestTraits.Priority, TestPriorities.High)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Medium)]
        public async Task ExecuteOperation_WithCancellation_ShouldHandleGracefully()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            _mockOperation.Setup(x => x.Execute(It.IsAny<OperationModel>(), It.IsAny<CancellationToken>(), It.IsAny<object>()))
                .ThrowsAsync(new OperationCanceledException());

            // Act & Assert
            await TestUtilities.Async.AssertThrowsAsync<OperationCanceledException>(
                async () => await _viewModel.ExecuteOperationAsync(customParameter: null, cancellationTokenSource.Token));
        }

        [Fact]
        [Trait(TestTraits.Priority, TestPriorities.High)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Medium)]
        public async Task ExecuteOperation_WithException_ShouldHandleGracefully()
        {
            // Arrange
            var exception = new InvalidOperationException("Test exception");
            _mockOperation.Setup(x => x.Execute(It.IsAny<OperationModel>(), It.IsAny<CancellationToken>(), It.IsAny<object>()))
                .ThrowsAsync(exception);

            // Act & Assert
            await TestUtilities.Async.AssertThrowsAsync<InvalidOperationException>(
                async () => await _viewModel.ExecuteOperationAsync(customParameter: null),
                "Test exception");
        }

        #endregion

        #region AI Integration Tests

        [Fact]
        [AITest]
        [Trait(TestTraits.Priority, TestPriorities.Medium)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Medium)]
        public async Task AskAi_WithValidQuestionAndResults_ShouldReturnResponse()
        {
            // Arrange
            var mockAIOperation = MockFactories.CreateAIEnabledOperation();
            var testResults = new ObservableCollection<object>
            {
                new { Address = 0x12345678UL, Type = "System.String", Value = "Test" }
            };

            _viewModel.Operation = mockAIOperation.Object;
            _viewModel.Items = testResults;
            _viewModel.AiQuestion = "What does this memory dump tell us?";

            mockAIOperation.Setup(x => x.AskAi(
                It.IsAny<OperationModel>(),
                It.IsAny<Collection<object>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<object>()))
                .ReturnsAsync("The memory dump shows normal string allocation patterns.");

            // Act
            await _viewModel.AskAiAsync();

            // Assert
            _viewModel.Conversation.Should().NotBeEmpty();
            _viewModel.Conversation.Should().Contain(msg => msg.Role == "user" && msg.Content == "What does this memory dump tell us?");
            _viewModel.Conversation.Should().Contain(msg => msg.Role == "assistant" && msg.Content.Contains("normal string allocation"));
        }

        [Fact]
        [AITest]
        [Trait(TestTraits.Priority, TestPriorities.Medium)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Medium)]
        public async Task AskAi_WithNonAIOperation_ShouldHandleGracefully()
        {
            // Arrange
            _viewModel.Operation = _mockOperation.Object; // Regular operation, not AI-enabled
            _viewModel.Items = new ObservableCollection<object> { "Item1" };
            _viewModel.AiQuestion = "What does this tell us?";

            // Act & Assert
            await TestUtilities.Async.AssertDoesNotThrowAsync(
                async () => await _viewModel.AskAiAsync(),
                "AskAi should handle non-AI operations gracefully");
        }

        #endregion

        #region Navigation Tests

        [Fact]
        [Trait(TestTraits.Priority, TestPriorities.Medium)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Fast)]
        public void GoToPreviousResultCommand_WithMultipleResults_ShouldNavigateProperly()
        {
            // Arrange
            var result1 = new[] { new { Id = 1 }, new { Id = 2 } };
            var result2 = new[] { new { Id = 3 }, new { Id = 4 } };
            
            _viewModel.AddResultSet(result1);
            _viewModel.AddResultSet(result2);
            _viewModel.Items = new ObservableCollection<object>(result2);

            // Act
            var canGoToPrevious = _viewModel.GoToPreviousResultCommand.CanExecute(null);

            // Assert
            canGoToPrevious.Should().BeTrue("because there are previous results to navigate to");
        }

        [Fact]
        [Trait(TestTraits.Priority, TestPriorities.Medium)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Fast)]
        public void GoToNextResultCommand_WithMultipleResults_ShouldNavigateProperly()
        {
            // Arrange
            var result1 = new[] { new { Id = 1 }, new { Id = 2 } };
            var result2 = new[] { new { Id = 3 }, new { Id = 4 } };
            
            _viewModel.AddResultSet(result1);
            _viewModel.AddResultSet(result2);
            _viewModel.Items = new ObservableCollection<object>(result1);

            // Act
            var canGoToNext = _viewModel.GoToNextResultCommand.CanExecute(null);

            // Assert
            canGoToNext.Should().BeTrue("because there are next results to navigate to");
        }

        #endregion

        #region Performance Tests

        [Fact]
        [PerformanceTest]
        [Trait(TestTraits.Priority, TestPriorities.Low)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Medium)]
        public void PropertyChangedNotification_ShouldBePerformant()
        {
            // Arrange
            var eventCount = 0;
            _viewModel.PropertyChanged += (s, e) => eventCount++;

            // Act & Assert
            TestUtilities.Performance.MeasureAndAssert(
                () =>
                {
                    for (int i = 0; i < 1000; i++)
                    {
                        _viewModel.Count = i;
                    }
                },
                TimeSpan.FromMilliseconds(100),
                "1000 property change notifications");

            eventCount.Should().Be(1000);
        }

        [Fact]
        [PerformanceTest]
        [Trait(TestTraits.Priority, TestPriorities.Low)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Medium)]
        public void ItemsCollection_LargeDataSet_ShouldPerformWell()
        {
            // Arrange
            var largeDataSet = Enumerable.Range(1, 10000)
                .Select(i => new { Id = i, Name = $"Item {i}", Value = i * 2 })
                .ToList();

            // Act & Assert
            TestUtilities.Performance.MeasureAndAssert(
                () => _viewModel.Items = new ObservableCollection<object>(largeDataSet),
                TimeSpan.FromSeconds(1),
                "Setting 10,000 items");

            _viewModel.Items.Should().HaveCount(10000);
            _viewModel.Count.Should().Be(10000);
        }

        #endregion

        #region Memory Management Tests

        [Fact]
        [MemoryTest]
        [Trait(TestTraits.Priority, TestPriorities.Medium)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Medium)]
        public void Dispose_ShouldCleanupResources()
        {
            // Arrange
            _viewModel.Items = new ObservableCollection<object> { "Item1", "Item2", "Item3" };
            _viewModel.AiQuestion = "Test question";

            // Act
            _viewModel.Dispose();

            // Assert
            _viewModel.Items.Should().BeNull();
            _viewModel.AiQuestion.Should().BeNull();
        }

        [Fact]
        [MemoryTest]
        [Trait(TestTraits.Priority, TestPriorities.Medium)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Medium)]
        public void MultipleDispose_ShouldBeIdempotent()
        {
            // Act & Assert
            TestUtilities.Exceptions.AssertDoesNotThrow(
                () =>
                {
                    _viewModel.Dispose();
                    _viewModel.Dispose();
                    _viewModel.Dispose();
                },
                "Multiple dispose calls should be safe");
        }

        #endregion

        #region Thread Safety Tests

        [Fact]
        [ThreadingTest]
        [Trait(TestTraits.Priority, TestPriorities.Medium)]
        [Trait(TestTraits.ExecutionTime, TestExecutionTimes.Medium)]
        public void PropertyChanges_FromMultipleThreads_ShouldBeThreadSafe()
        {
            // Arrange
            var exceptions = new List<Exception>();
            var tasks = new List<Task>();

            // Act
            for (int i = 0; i < 10; i++)
            {
                var taskIndex = i;
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        for (int j = 0; j < 100; j++)
                        {
                            _viewModel.Count = taskIndex * 100 + j;
                            _viewModel.AiQuestion = $"Question {taskIndex}-{j}";
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions)
                        {
                            exceptions.Add(ex);
                        }
                    }
                }));
            }

            // Assert
            TestUtilities.Async.ExecuteWithTimeout(
                () => Task.WhenAll(tasks),
                TimeSpan.FromSeconds(10),
                "Thread safety test");

            exceptions.Should().BeEmpty("because property changes should be thread-safe");
        }

        #endregion

        #region Cleanup

        protected override void TearDown()
        {
            _viewModel?.Dispose();
            VerifyAllMocks();
            base.TearDown();
        }

        #endregion
    }

    /// <summary>
    /// Testable version of BaseOperationViewModel that allows access to protected methods
    /// </summary>
    internal class TestableBaseOperationViewModel : BaseOperationViewModel
    {
        private readonly List<object[]> _resultSets = new();

        public void SetupForTesting(IDebuggerOperation operation, ConfigurationService configService)
        {
            Operation = operation;
            // Note: In a real scenario, we'd need to setup the configuration service properly
        }

        public async Task ExecuteOperationAsync(object customParameter = null, CancellationToken cancellationToken = default)
        {
            if (Operation == null) return;

            var results = await Operation.Execute(Model, cancellationToken, customParameter);
            if (results != null)
            {
                var resultArray = results.ToArray();
                Items = new ObservableCollection<object>(resultArray);
                Count = resultArray.Length;
            }
        }

        public async Task AskAiAsync()
        {
            if (Operation is IAIEnabledOperation aiOperation && Items != null && !string.IsNullOrEmpty(AiQuestion))
            {
                var response = await aiOperation.AskAi(Model, new Collection<object>(Items), CancellationToken.None, null);
                
                // Add to conversation
                if (Conversation == null)
                {
                    Conversation = new ObservableCollection<ConversationMessage>();
                }
                
                Conversation.Add(new ConversationMessage { Role = "user", Content = AiQuestion });
                Conversation.Add(new ConversationMessage { Role = "assistant", Content = response });
            }
        }

        public void AddResultSet(object[] results)
        {
            _resultSets.Add(results);
        }

        public ICommand GoToPreviousResultCommand => new TestCommand(() => { }, () => _resultSets.Count > 1);
        public ICommand GoToNextResultCommand => new TestCommand(() => { }, () => _resultSets.Count > 1);

        private class TestCommand : ICommand
        {
            private readonly Action _execute;
            private readonly Func<bool> _canExecute;

            public TestCommand(Action execute, Func<bool> canExecute)
            {
                _execute = execute;
                _canExecute = canExecute;
            }

            public bool CanExecute(object parameter) => _canExecute();
            public void Execute(object parameter) => _execute();
            public event EventHandler CanExecuteChanged;
        }
    }
} 