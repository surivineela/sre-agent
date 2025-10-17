// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using Agent.Cli.Helpers;

namespace Agent.Cli.Services;

/// <summary>
/// Enhanced progress indicators and user feedback service
/// </summary>
public static class ProgressService
{
    // Removed spinner frame arrays - now using ConsoleUI.GetSpinnerFrame()

    /// <summary>
    /// Show a multi-step progress indicator with current step status
    /// </summary>
    public static class MultiStepProgress
    {
        private static int _currentStep = 0;
        private static string[] _steps = [];
        private static DateTime _stepStart = DateTime.Now;
        private static List<(string step, TimeSpan duration, bool success)> _completedSteps = [];

        public static void Initialize(string[] steps)
        {
            _steps = steps;
            _currentStep = 0;
            _stepStart = DateTime.Now;
            _completedSteps.Clear();

            ConsoleUI.WriteSection("Starting Process", ConsoleColor.Cyan);
            ShowCurrentProgress();
        }

        public static void NextStep(string? customMessage = null)
        {
            if (_currentStep < _steps.Length)
            {
                var duration = DateTime.Now - _stepStart;
                _completedSteps.Add((_steps[_currentStep], duration, true));
                ConsoleUI.WriteStatus(true, $"Completed: {_steps[_currentStep]}");
            }

            _currentStep++;
            _stepStart = DateTime.Now;

            if (_currentStep < _steps.Length)
            {
                ShowCurrentProgress(customMessage);
            }
            else
            {
                ShowCompletionSummary();
            }
        }

        public static void Fail(string error)
        {
            if (_currentStep < _steps.Length)
            {
                var duration = DateTime.Now - _stepStart;
                _completedSteps.Add((_steps[_currentStep], duration, false));
            }
            ConsoleUI.WriteStatus(false, error);
            Console.WriteLine();
        }

        private static void ShowCompletionSummary()
        {
            Console.WriteLine();
            ConsoleUI.WriteSection("Process Complete", ConsoleColor.Green);

            foreach (var (step, duration, success) in _completedSteps)
            {
                var timeStr = duration.TotalSeconds < 1
                    ? $"{duration.TotalMilliseconds:F0}ms"
                    : $"{duration.TotalSeconds:F1}s";
                var status = success ? "✓" : "✗";
                ConsoleUI.WriteBullet($"{step} ({timeStr})", success ? ConsoleColor.Green : ConsoleColor.Red);
            }
            Console.WriteLine();
        }

        private static void ShowCurrentProgress(string? customMessage = null)
        {
            var message = customMessage ?? _steps[_currentStep];
            ConsoleUI.WriteKeyValue($"Step {_currentStep + 1}/{_steps.Length}", message, 15, ConsoleColor.Cyan, ConsoleColor.White);
            Console.WriteLine();

            // Show upcoming steps
            for (int i = 0; i < _steps.Length; i++)
            {
                var status = i < _currentStep ? "Complete" : (i == _currentStep ? "In Progress" : "Pending");
                var color = i < _currentStep ? ConsoleColor.Green : (i == _currentStep ? ConsoleColor.Yellow : ConsoleColor.DarkGray);
                ConsoleUI.WriteBullet($"{_steps[i]} ({status})", color);
            }
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Animated spinner with contextual messages
    /// </summary>
    public static class AnimatedSpinner
    {
        private static bool _isRunning = false;
        private static CancellationTokenSource? _cancellationTokenSource;
        private static string _message = "";

        public static void Start(string message)
        {
            _message = message;
            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();

            Task.Run(() => SpinnerLoop(_cancellationTokenSource.Token));
        }

        public static void UpdateMessage(string newMessage)
        {
            _message = newMessage;
        }

        public static void Stop(string? finalMessage = null)
        {
            _isRunning = false;
            _cancellationTokenSource?.Cancel();

            // Clear the spinner line
            ConsoleUI.ClearLine();

            if (finalMessage != null)
            {
                Console.WriteLine(finalMessage);
            }
        }

        private static async Task SpinnerLoop(CancellationToken cancellationToken)
        {
            int frameIndex = 0;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            while (_isRunning && !cancellationToken.IsCancellationRequested)
            {
                var frame = ConsoleUI.GetSpinnerFrame(frameIndex);
                var elapsed = stopwatch.Elapsed;
                var timeDisplay = elapsed.TotalSeconds > 60
                    ? $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}"
                    : $"{elapsed.TotalSeconds:F1}s";

                Console.Write($"\r{frame} {_message} ({timeDisplay})");

                frameIndex++;
                await Task.Delay(120, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Progress bar for file operations or batch processing
    /// </summary>
    public static void ShowProgressBar(int current, int total, string operation = "Processing")
    {
        if (total <= 0) return;

        var percentage = (double)current / total;
        var barWidth = 30;
        var filled = (int)(percentage * barWidth);

        var bar = new StringBuilder();
        bar.Append('[');

        for (int i = 0; i < barWidth; i++)
        {
            bar.Append(i < filled ? ConsoleUI.Chars.Full[0] : '.');
        }

        bar.Append(']');

        var percentageText = $"{percentage:P0}".PadLeft(4);
        var countText = $"({current}/{total})".PadLeft(10);

        Console.Write($"\r{operation} {bar} {percentageText} {countText}");

        if (current == total)
        {
            Console.WriteLine(); // New line when complete
        }
    }

    /// <summary>
    /// Success feedback with celebration
    /// </summary>
    public static void ShowSuccess(string message, string? details = null, bool celebrate = true)
    {
        ConsoleUI.WriteStatus(true, message);

        if (!string.IsNullOrEmpty(details))
        {
            Console.WriteLine($"   {details}");
        }
        Console.WriteLine();
    }

    /// <summary>
    /// Warning with helpful context
    /// </summary>
    public static void ShowWarning(string message, string? suggestion = null)
    {
        ConsoleUI.WriteInfo($"Warning: {message}", ConsoleColor.Yellow);
        if (!string.IsNullOrEmpty(suggestion))
        {
            ConsoleUI.WriteBullet($"Suggestion: {suggestion}", ConsoleColor.DarkYellow);
        }
        Console.WriteLine();
    }

    /// <summary>
    /// Error with actionable suggestions
    /// </summary>
    public static void ShowError(string message, string[]? suggestions = null)
    {
        ConsoleUI.WriteStatus(false, message);

        if (suggestions != null && suggestions.Length > 0)
        {
            Console.WriteLine();
            ConsoleUI.WriteInfo("Try these solutions:", ConsoleColor.Cyan);
            foreach (var suggestion in suggestions)
            {
                ConsoleUI.WriteBullet(suggestion, ConsoleColor.Gray);
            }
        }
        Console.WriteLine();
    }

    /// <summary>
    /// Info message with icon
    /// </summary>
    public static void ShowInfo(string message, string icon = "ℹ️")
    {
        ConsoleUI.WriteInfo(message);
    }

    /// <summary>
    /// Show a confirmation prompt with enhanced styling
    /// </summary>
    public static bool ConfirmAction(string action, string? warning = null, bool defaultYes = false)
    {
        var message = action;
        if (!string.IsNullOrEmpty(warning))
        {
            message += $" (Warning: {warning})";
        }

        return ConsoleUI.Confirm(message, defaultYes);
    }

    /// <summary>
    /// Show operation timing information
    /// </summary>
    public static void ShowTiming(string operation, TimeSpan duration)
    {
        ConsoleUI.WriteDuration(duration, operation);
    }
}
