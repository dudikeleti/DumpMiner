using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;

namespace DumpMiner.Common
{
    /// <summary>
    /// Thread-safe progress reporter with automatic time estimation and speed calculation
    /// </summary>
    public class ProgressReporter : IProgressReporter
    {
        private readonly object _lockObject = new object();
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly SynchronizationContext _syncContext;
        
        private int _lastPercentage;
        private long _lastProcessedCount;
        private DateTime _lastUpdateTime;
        private string _currentPhase;
        private double _averageProcessingSpeed;
        private readonly CircularBuffer<double> _speedHistory = new CircularBuffer<double>(10);

        public event EventHandler<ProgressEventArgs> ProgressChanged;

        public ProgressReporter()
        {
            _syncContext = SynchronizationContext.Current ?? 
                          (Application.Current?.Dispatcher != null ? 
                           new System.Windows.Threading.DispatcherSynchronizationContext(Application.Current.Dispatcher) : 
                           null);
            _stopwatch.Start();
            _lastUpdateTime = DateTime.Now;
        }

        public void ReportProgress(int percentage, string currentItem = null, string statusMessage = null)
        {
            lock (_lockObject)
            {
                var args = new ProgressEventArgs
                {
                    Percentage = Math.Clamp(percentage, 0, 100),
                    CurrentItem = currentItem,
                    StatusMessage = statusMessage,
                    PhaseName = _currentPhase,
                    EstimatedTimeRemaining = CalculateEstimatedTimeRemaining(percentage),
                    ProcessingSpeed = _averageProcessingSpeed
                };

                FireProgressChanged(args);
                _lastPercentage = percentage;
            }
        }

        public void ReportProgress(long current, long total, string itemType, string statusMessage = null)
        {
            lock (_lockObject)
            {
                var percentage = total > 0 ? (int)((current * 100) / total) : 0;
                var currentItem = $"{current:N0} of {total:N0} {itemType}";
                
                // Calculate processing speed
                var now = DateTime.Now;
                var timeDiff = now - _lastUpdateTime;
                if (timeDiff.TotalSeconds > 0 && current > _lastProcessedCount)
                {
                    var itemsProcessed = current - _lastProcessedCount;
                    var speed = itemsProcessed / timeDiff.TotalSeconds;
                    _speedHistory.Add(speed);
                    _averageProcessingSpeed = _speedHistory.Average();
                }

                var args = new ProgressEventArgs
                {
                    Percentage = Math.Clamp(percentage, 0, 100),
                    CurrentItem = currentItem,
                    StatusMessage = statusMessage,
                    PhaseName = _currentPhase,
                    CurrentCount = current,
                    TotalCount = total,
                    ItemType = itemType,
                    EstimatedTimeRemaining = CalculateEstimatedTimeRemaining(current, total),
                    ProcessingSpeed = _averageProcessingSpeed
                };

                FireProgressChanged(args);
                _lastPercentage = percentage;
                _lastProcessedCount = current;
                _lastUpdateTime = now;
            }
        }

        public void ReportProgress(int percentage, string currentItem, string statusMessage, 
            TimeSpan? estimatedTimeRemaining, double processingSpeed = 0)
        {
            lock (_lockObject)
            {
                var args = new ProgressEventArgs
                {
                    Percentage = Math.Clamp(percentage, 0, 100),
                    CurrentItem = currentItem,
                    StatusMessage = statusMessage,
                    PhaseName = _currentPhase,
                    EstimatedTimeRemaining = estimatedTimeRemaining,
                    ProcessingSpeed = processingSpeed > 0 ? processingSpeed : _averageProcessingSpeed
                };

                FireProgressChanged(args);
                _lastPercentage = percentage;
            }
        }

        public void ReportPhase(string phaseName, string phaseDescription)
        {
            lock (_lockObject)
            {
                _currentPhase = phaseName;
                
                var args = new ProgressEventArgs
                {
                    PhaseName = phaseName,
                    PhaseDescription = phaseDescription,
                    Percentage = _lastPercentage,
                    ProcessingSpeed = _averageProcessingSpeed
                };

                FireProgressChanged(args);
            }
        }

        public void ReportCompleted(long totalProcessed, TimeSpan totalTime)
        {
            lock (_lockObject)
            {
                _stopwatch.Stop();
                
                var args = new ProgressEventArgs
                {
                    Percentage = 100,
                    IsCompleted = true,
                    TotalProcessed = totalProcessed,
                    TotalTime = totalTime,
                    StatusMessage = $"Completed processing {totalProcessed:N0} items in {FormatTimeSpan(totalTime)}",
                    ProcessingSpeed = totalTime.TotalSeconds > 0 ? totalProcessed / totalTime.TotalSeconds : 0
                };

                FireProgressChanged(args);
            }
        }

        private TimeSpan? CalculateEstimatedTimeRemaining(int percentage)
        {
            if (percentage <= 0 || percentage >= 100 || _stopwatch.Elapsed.TotalSeconds < 1)
                return null;

            var elapsedSeconds = _stopwatch.Elapsed.TotalSeconds;
            var remainingPercentage = 100 - percentage;
            var secondsPerPercent = elapsedSeconds / percentage;
            var estimatedRemainingSeconds = remainingPercentage * secondsPerPercent;

            return TimeSpan.FromSeconds(estimatedRemainingSeconds);
        }

        private TimeSpan? CalculateEstimatedTimeRemaining(long current, long total)
        {
            if (current <= 0 || current >= total || _averageProcessingSpeed <= 0)
                return null;

            var remaining = total - current;
            var estimatedSeconds = remaining / _averageProcessingSpeed;
            return TimeSpan.FromSeconds(estimatedSeconds);
        }

        private void FireProgressChanged(ProgressEventArgs args)
        {
            if (ProgressChanged == null) return;

            if (_syncContext != null)
            {
                _syncContext.Post(_ => ProgressChanged?.Invoke(this, args), null);
            }
            else
            {
                ProgressChanged?.Invoke(this, args);
            }
        }

        private static string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalDays >= 1)
                return $"{timeSpan.Days}d {timeSpan.Hours}h {timeSpan.Minutes}m";
            if (timeSpan.TotalHours >= 1)
                return $"{timeSpan.Hours}h {timeSpan.Minutes}m {timeSpan.Seconds}s";
            if (timeSpan.TotalMinutes >= 1)
                return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
            return $"{timeSpan.TotalSeconds:F1}s";
        }
    }

    /// <summary>
    /// Circular buffer for calculating rolling averages
    /// </summary>
    public class CircularBuffer<T>
    {
        private readonly T[] _buffer;
        private readonly int _capacity;
        private int _count;
        private int _head;

        public CircularBuffer(int capacity)
        {
            _capacity = capacity;
            _buffer = new T[capacity];
        }

        public void Add(T item)
        {
            _buffer[_head] = item;
            _head = (_head + 1) % _capacity;
            if (_count < _capacity)
                _count++;
        }

        public double Average()
        {
            if (_count == 0 || typeof(T) != typeof(double))
                return 0;

            double sum = 0;
            for (int i = 0; i < _count; i++)
            {
                sum += Convert.ToDouble(_buffer[i]);
            }
            return sum / _count;
        }
    }
} 