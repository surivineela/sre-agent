// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using Agent.Cli.Helpers;

namespace Agent.Cli.Services;

public class ThreadManagerService
{
    private readonly string _currentThreadFile = Path.Combine(CliConfigurationService.UserConfigDir, ".current-thread");
    private readonly ITokenService _tokenService;

    public ThreadManagerService()
    {
        _tokenService = new TokenService();
    }

    public async Task<string?> GetCurrentThreadIdAsync()
    {
        try
        {
            if (File.Exists(_currentThreadFile))
            {
                return await File.ReadAllTextAsync(_currentThreadFile);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task SetCurrentThreadIdAsync(string threadId)
    {
        try
        {
            Directory.CreateDirectory(CliConfigurationService.UserConfigDir);
            await File.WriteAllTextAsync(_currentThreadFile, threadId);
        }
        catch
        {
            // Ignore errors
        }
    }

    /// <summary>
    /// Gets user information from the access token.
    /// </summary>
    /// <returns>A tuple containing UserId (from puid claim) and DisplayName (from name claim)</returns>
    public async Task<(string UserId, string DisplayName)> GetUserInfoAsync()
    {
        try
        {
            var token = await _tokenService.GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                return (Environment.UserName, Environment.UserName);
            }

            // Parse JWT token using JwtSecurityTokenHandler for more robust parsing
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();

            // Check if token can be read
            if (!handler.CanReadToken(token))
            {
                return (Environment.UserName, Environment.UserName);
            }

            var jwtToken = handler.ReadJwtToken(token);

            var userId = Environment.UserName;
            var displayName = Environment.UserName;

            // Extract puid as UserId
            var puidClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "puid");
            if (puidClaim != null && !string.IsNullOrEmpty(puidClaim.Value))
            {
                userId = puidClaim.Value;
            }

            // Extract name as DisplayName
            var nameClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "name");
            if (nameClaim != null && !string.IsNullOrEmpty(nameClaim.Value))
            {
                displayName = nameClaim.Value;
            }

            return (userId, displayName);
        }
        catch
        {
            // Fallback to environment username on any error
            return (Environment.UserName, Environment.UserName);
        }
    }

    /// <summary>
    /// Tracks and displays messages from a thread without user interaction.
    /// </summary>
    /// <param name="threadId">The thread ID to track</param>
    /// <returns>Error message string if failed, null if successful</returns>
    public async Task<string?> TrackChatSessionAsync(string threadId)
    {
        try
        {
            var apiService = new ApiService();
            var (userId, displayName) = await GetUserInfoAsync();
            var session = new ChatSession(threadId, userId, displayName, agentName: null);

            session.DisplayHeader();

            // Set up console cancellation handling
            var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cancellationTokenSource.Cancel();
            };

            try
            {
                const string exitHint = "(Ctrl + C to exit)";
                var lastMessageTime = DateTime.UtcNow;
                const int maxIdleSeconds = 180; // 3 minutes

                var state = "Connecting";
                session.ShowState($"{state} {exitHint}");

                while (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var (collection, error) = await apiService.ListThreadMessagesAsync(threadId);

                    session.ClearStateLine();

                    if (error != null)
                    {
                        return $"Failed to get messages: {error}";
                    }

                    state = collection?.State ?? "Connecting";

                    if (collection?.Value != null)
                    {
                        var hasNewMessages = session.UpdateMessages(collection.Value, state);
                        if (hasNewMessages)
                        {
                            lastMessageTime = DateTime.UtcNow;
                        }
                    }

                    // Display appropriate state message
                    var displayState = state.Equals("Idle", StringComparison.OrdinalIgnoreCase)
                        ? "Waiting for new message"
                        : state;
                    session.ShowState($"{displayState} {exitHint}");

                    // Check if idle for too long
                    if ((DateTime.UtcNow - lastMessageTime).TotalSeconds > maxIdleSeconds)
                    {
                        session.ClearStateLine();
                        Console.WriteLine();
                        Console.WriteLine("No new messages for 3 minutes. Exiting tracking mode.");
                        break;
                    }

                    // If state is idle, we can still continue tracking but check for timeout
                    await Task.Delay(1000, cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when user presses Ctrl+C
            }
            finally
            {
                // Ensure cleanup happens
                session.ClearStateLine();

                Console.WriteLine();
                Console.WriteLine("Tracking session ended.");
                Console.WriteLine($"Thread ID: {threadId}");
            }

            return null;
        }
        catch (Exception ex)
        {
            return $"Failed to track chat session: {ex.Message}";
        }
    }

    /// <summary>
    /// Starts an interactive chat session for a thread.
    /// </summary>
    /// <param name="threadId">The thread ID to continue, or null to create a new thread</param>
    /// <param name="agentName">The agent name to preselect for the session</param>
    /// <param name="message">Optional message to send automatically after starting the session</param>
    /// <returns>Error message string if failed, null if successful</returns>
    public async Task<string?> StartChatSessionAsync(string? threadId, string? agentName = null, string? message = null)
    {
        try
        {
            var apiService = new ApiService();
            var (userId, displayName) = await GetUserInfoAsync();
            var session = new ChatSession(threadId, userId, displayName, agentName);

            // Delay header display if creating new thread with initial message
            // Otherwise show it immediately
            var shouldDisplayHeader = !string.IsNullOrEmpty(threadId) || string.IsNullOrWhiteSpace(message);
            if (shouldDisplayHeader)
            {
                session.DisplayHeader();
            }

            // Set up console cancellation handling
            var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cancellationTokenSource.Cancel();
            };

            try
            {
                while (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    // Poll for messages first (handles both initial load and updates)
                    if (!string.IsNullOrEmpty(session.ThreadId))
                    {
                        var pollingStarted = DateTime.UtcNow;
                        const int maxPollingSeconds = 60;

                        while ((DateTime.UtcNow - pollingStarted).TotalSeconds < maxPollingSeconds)
                        {
                            var (collection, error) = await apiService.ListThreadMessagesAsync(session.ThreadId);

                            if (error != null)
                            {
                                session.ClearStateLine();
                                return $"Failed to get messages: {error}";
                            }

                            var state = collection?.State ?? "Connecting";

                            if (collection?.Value != null)
                            {
                                session.ClearStateLine();
                                session.UpdateMessages(collection.Value, state);
                                session.ShowState(state);
                            }

                            if (state == "Idle")
                            {
                                session.ClearStateLine();
                                break;
                            }

                            await Task.Delay(500);
                        }
                    }

                    // Get user message: either from initial message parameter or prompt user
                    string? userMessage;
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        // Use the initial message and clear it so we only use it once
                        userMessage = message;
                        message = null;
                    }
                    else
                    {
                        // Show input prompt and read user input
                        session.DisplayInputPrompt();
                        var inputTask = Task.Run(() => Console.ReadLine(), cancellationTokenSource.Token);

                        try
                        {
                            userMessage = await inputTask;
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }

                        // Skip empty messages
                        if (string.IsNullOrWhiteSpace(userMessage))
                        {
                            session.ClearInputPrompt();
                            continue;
                        }

                        // Check for exit commands
                        if (userMessage.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                            userMessage.Trim().Equals("quit", StringComparison.OrdinalIgnoreCase) ||
                            userMessage.Trim().Equals("/exit", StringComparison.OrdinalIgnoreCase) ||
                            userMessage.Trim().Equals("/quit", StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }

                        // Check for /agent command
                        if (userMessage.Trim().StartsWith("/agent", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = userMessage.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length == 1)
                            {
                                session.AgentName = null;
                            }
                            else
                            {
                                session.AgentName = parts[1].Trim();
                            }

                            session.ClearInputPrompt();
                            continue;
                        }

                        // Clear input prompt before processing
                        session.ClearInputPrompt();
                    }

                    // Prefix message with agent name if selected
                    var messageToSend = userMessage;
                    if (!string.IsNullOrEmpty(session.AgentName))
                    {
                        messageToSend = $"@{session.AgentName}: {userMessage}";
                    }

                    if (string.IsNullOrEmpty(session.ThreadId))
                    {
                        // Create new thread
                        session.ShowState("Sending message");

                        var (thread, error) = await apiService.CreateThreadAsync(
                            messageToSend, userId, displayName, session.AgentName);

                        session.ClearStateLine();

                        if (thread == null || !string.IsNullOrEmpty(error))
                        {
                            session.ClearStateLine();
                            return $"Failed to create thread: {error}";
                        }

                        session.ThreadId = thread.Id;

                        // Display header now if we delayed it earlier
                        if (!shouldDisplayHeader)
                        {
                            session.DisplayHeader();
                            shouldDisplayHeader = true;
                        }
                    }
                    else
                    {
                        // Send message to existing thread
                        session.ShowState("Sending message");

                        var (threadMessage, error) = await apiService.SendThreadMessageAsync(
                            session.ThreadId, messageToSend, userId, displayName, session.AgentName);

                        if (threadMessage == null || error != null)
                        {
                            session.ClearStateLine();
                            return $"Failed to send message: {error}";
                        }
                    }

                    await SetCurrentThreadIdAsync(session.ThreadId);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when user presses Ctrl+C
            }
            finally
            {
                // Ensure cleanup happens
                session.ClearStateLine();

                Console.WriteLine();
                Console.WriteLine("Chat session ended.");
                if (!string.IsNullOrEmpty(session.ThreadId))
                {
                    Console.WriteLine($"Thread ID: {session.ThreadId}");
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            return $"Failed to start chat session: {ex.Message}";
        }
    }

    /// <summary>
    /// Sends a message to a thread without waiting for response or starting an interactive session.
    /// </summary>
    /// <param name="threadId">The thread ID to send message to, or null to create a new thread</param>
    /// <param name="agentName">The agent name for new threads</param>
    /// <param name="message">The message to send</param>
    /// <returns>Error message string if failed, null if successful</returns>
    public async Task<string?> SendMessageWithoutWaitAsync(string? threadId, string? agentName, string message)
    {
        try
        {
            var apiService = new ApiService();
            var (userId, displayName) = await GetUserInfoAsync();

            // Create new thread or send to existing thread
            if (string.IsNullOrEmpty(threadId))
            {
                DebugLogger.Debug("Thread", "Creating new thread with message");
                var (thread, error) = await apiService.CreateThreadAsync(message, userId, displayName, agentName);
                if (thread != null)
                {
                    ConsoleUI.WriteStatus(true, $"Message sent. Thread ID: {thread.Id}");
                    await SetCurrentThreadIdAsync(thread.Id);
                    return null;
                }
                return error ?? "Failed to create thread";
            }
            else
            {
                DebugLogger.Debug("Thread", $"Sending message to thread {threadId}");
                var (threadMessage, error) = await apiService.SendThreadMessageAsync(threadId, message, userId, displayName, agentName);
                if (threadMessage != null)
                {
                    ConsoleUI.WriteStatus(true, $"Message sent to thread {threadId}");
                    await SetCurrentThreadIdAsync(threadId);
                    return null;
                }
                return error ?? "Failed to send message";
            }
        }
        catch (Exception ex)
        {
            return $"Failed to send message: {ex.Message}";
        }
    }

    /// <summary>
    /// Sends a message and waits for agent response, then exits (non-interactive).
    /// </summary>
    /// <param name="threadId">The thread ID to send message to, or null to create a new thread</param>
    /// <param name="agentName">The agent name for new threads</param>
    /// <param name="message">The message to send</param>
    /// <returns>Error message string if failed, null if successful</returns>
    public async Task<string?> SendMessageAndWaitForResponseAsync(string? threadId, string? agentName, string message)
    {
        try
        {
            var apiService = new ApiService();
            var (userId, displayName) = await GetUserInfoAsync();
            var session = new ChatSession(threadId, userId, displayName, agentName);

            // Prefix message with agent name if specified
            var messageToSend = message;
            if (!string.IsNullOrEmpty(agentName))
            {
                messageToSend = $"@{agentName}: {message}";
            }

            // Set up console cancellation handling
            var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cancellationTokenSource.Cancel();
            };

            try
            {
                // Create new thread or send to existing thread
                if (string.IsNullOrEmpty(threadId))
                {
                    DebugLogger.Debug("Thread", "Creating new thread with message");
                    var (thread, error) = await apiService.CreateThreadAsync(messageToSend, userId, displayName, agentName);
                    if (thread == null || !string.IsNullOrEmpty(error))
                    {
                        return $"Failed to create thread: {error}";
                    }
                    session.ThreadId = thread.Id;
                    await SetCurrentThreadIdAsync(thread.Id);
                }
                else
                {
                    DebugLogger.Debug("Thread", $"Sending message to thread {threadId}");
                    var (threadMessage, error) = await apiService.SendThreadMessageAsync(threadId, messageToSend, userId, displayName, agentName);
                    if (threadMessage == null || error != null)
                    {
                        return $"Failed to send message: {error}";
                    }
                    session.ThreadId = threadId;
                    await SetCurrentThreadIdAsync(threadId);
                }

                // Display header after thread is created/confirmed
                session.DisplayHeader();

                // Poll for messages until state becomes Idle
                const int maxPollingSeconds = 300; // 5 minutes max
                var pollingStarted = DateTime.UtcNow;

                while (!cancellationTokenSource.Token.IsCancellationRequested &&
                       (DateTime.UtcNow - pollingStarted).TotalSeconds < maxPollingSeconds)
                {
                    var (collection, error) = await apiService.ListThreadMessagesAsync(session.ThreadId!);

                    if (error != null)
                    {
                        session.ClearStateLine();
                        return $"Failed to get messages: {error}";
                    }

                    var state = collection?.State ?? "Connecting";

                    if (collection?.Value != null)
                    {
                        session.ClearStateLine();
                        session.UpdateMessages(collection.Value, state);

                        if (state != "Idle")
                        {
                            session.ShowState(state);
                        }
                    }

                    if (state == "Idle")
                    {
                        session.ClearStateLine();
                        break;
                    }

                    await Task.Delay(500, cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                session.ClearStateLine();
                Console.WriteLine();
                Console.WriteLine("Operation cancelled.");
            }

            return null;
        }
        catch (Exception ex)
        {
            return $"Failed to send message: {ex.Message}";
        }
    }

    /// <summary>
    /// Manages the state of a chat session including messages and cursor positions.
    /// </summary>
    private class ChatSession
    {
        public string? ThreadId { get; set; }
        public string UserId { get; }
        public string DisplayName { get; }

        private readonly List<TrackedMessage> _trackedMessages = [];
        private string? _lastPrintedMessageId;
        private Timer? _stateAnimationTimer;
        private string _currentState = "";
        private int _animationFrame = 0;

        public string? AgentName { get; set; }

        public ChatSession(string? threadId, string userId, string displayName, string? agentName = null)
        {
            ThreadId = threadId;
            UserId = userId;
            DisplayName = displayName;
            AgentName = agentName;
        }

        public void DisplayHeader()
        {
            var userLine = $"User: {DisplayName} ({UserId})";
            var threadLine = $"Thread: {ThreadId ?? "New thread will be created"}";
            var width = Math.Max(Math.Max(userLine.Length, threadLine.Length), 50) + 4; // Add padding

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"┌{new string('─', width - 2)}┐");
            Console.WriteLine($"│ {userLine.PadRight(width - 3)}│");
            Console.WriteLine($"│ {threadLine.PadRight(width - 3)}│");
            Console.WriteLine($"└{new string('─', width - 2)}┘");
            Console.ResetColor();

            Console.WriteLine();
        }

        public void DisplayInputPrompt()
        {
            // Print input box in blue
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("┌───────────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ Type your messages and press Enter to send.                       │");
            Console.WriteLine("│                                                                   │");
            Console.WriteLine("│ Command:                                                          │");
            Console.WriteLine("│    /agent <name> : Pick a subagent to assist you with tasks       │");
            Console.WriteLine("│    /agent        : Reset the subagent to default agent            │");
            Console.WriteLine("│    /exit, /quit  : Exit the conversation                          │");
            Console.WriteLine("└───────────────────────────────────────────────────────────────────┘");
            Console.ResetColor();

            // Print 'You:' prompt in green
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("You: ");
            Console.ResetColor();

            // Print agent name in cyan if selected
            if (!string.IsNullOrEmpty(AgentName))
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"[@{AgentName}] ");
                Console.ResetColor();
            }
        }

        public void ClearInputPrompt()
        {
            int inputPromptLines = 9;

            if (DebugLogger.IsDebugEnabled)
            {
                Console.WriteLine($"[Debug] Skip clearing input prompt for debugging.");
            }
            else
            {
                // Get current position and go back 8 lines to clear the input prompt
                var currentTop = Console.CursorTop;
                var startLine = currentTop - inputPromptLines;

                // Clear each line by explicitly positioning and overwriting
                for (int i = 0; i < inputPromptLines; i++)
                {
                    Console.SetCursorPosition(0, startLine + i);
                    Console.Write(new string(' ', Console.WindowWidth));
                }
                Console.SetCursorPosition(0, startLine);
            }
        }

        public bool UpdateMessages(List<Models.ThreadMessageV1> newMessages, string state)
        {
            var hasChanges = false;
            var orderedMessages = newMessages.OrderBy(m => m.TimeStamp).ToList();
            var foundLastPrinted = _lastPrintedMessageId == null;

            // Update tracked messages
            foreach (var msg in orderedMessages)
            {
                if (msg.IsComplete == false)
                {
                    // Skip incomplete messages
                    continue;
                }

                var existing = _trackedMessages.FirstOrDefault(m => m.Id == msg.Id);

                if (existing == null)
                {
                    var newMessage = new TrackedMessage
                    {
                        Id = msg.Id,
                        Text = msg.Text,
                        Role = msg.Author.Role,
                        DisplayName = msg.Author.DisplayName,
                        Timestamp = msg.TimeStamp
                    };

                    // New message
                    _trackedMessages.Add(newMessage);
                    PrintTrackedMessages(newMessage);

                    hasChanges = true;
                }
            }

            return hasChanges;
        }

        private void PrintTrackedMessages(TrackedMessage message)
        {
            // Use author's display name from the message, fall back to "SRE Agent" or current session DisplayName
            var roleLabel = message.Role.Equals("SREAgent", StringComparison.OrdinalIgnoreCase)
                ? "SRE Agent"
                : (!string.IsNullOrEmpty(message.DisplayName) ? message.DisplayName : DisplayName);
            var timestamp = message.Timestamp.ToString("HH:mm:ss");
            var roleColor = message.Role.Equals("SREAgent", StringComparison.OrdinalIgnoreCase)
                ? ConsoleColor.Cyan
                : ConsoleColor.Green;

            // Build header line for separator length calculation
            var headerLine = $"{roleLabel} ({timestamp})";

            // Print role label in color
            Console.ForegroundColor = roleColor;
            Console.Write(roleLabel);
            Console.ResetColor();

            // Print timestamp in dark gray
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($" ({timestamp})");
            Console.ResetColor();

            // Print separator line matching header length using ─
            Console.ForegroundColor = roleColor;
            Console.WriteLine(new string('─', headerLine.Length));
            Console.ResetColor();

            // Print message text with 2-space indent, handling newlines and wrapping at window width
            var maxWidth = Console.WindowWidth - 2; // Account for 2-space indent
            var lines = message.Text.Split('\n');

            foreach (var line in lines)
            {
                var trimmedLine = line.TrimEnd('\r'); // Handle \r\n line endings

                if (string.IsNullOrEmpty(trimmedLine))
                {
                    Console.WriteLine("  "); // Empty line with indent
                    continue;
                }

                var words = trimmedLine.Split(' ');
                var currentLine = new StringBuilder("  "); // 2-space indent

                foreach (var word in words)
                {
                    // Check if adding this word would exceed the width
                    if (currentLine.Length + word.Length + 1 > maxWidth && currentLine.Length > 2)
                    {
                        Console.WriteLine(currentLine.ToString().TrimEnd());
                        currentLine.Clear();
                        currentLine.Append("  "); // Reset with indent
                    }

                    currentLine.Append(word);
                    currentLine.Append(' ');
                }

                // Print remaining text
                if (currentLine.Length > 2)
                {
                    Console.WriteLine(currentLine.ToString().TrimEnd());
                }
            }

            Console.WriteLine();

            _lastPrintedMessageId = message.Id;
        }

        public void ShowState(string state)
        {
            _currentState = state;

            // Start animation timer if not already running
            if (_stateAnimationTimer == null)
            {
                _animationFrame = 0;
                _stateAnimationTimer = new Timer(
                    _ => AnimateState(),
                    null,
                    TimeSpan.Zero,
                    TimeSpan.FromMilliseconds(300));
            }
        }

        private void AnimateState()
        {
            try
            {
                // Progressive animation: ●, ●●, ●●●
                var dotCount = (_animationFrame % 3) + 1;
                var dots = new string('●', dotCount).PadRight(3, ' ');

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write($"\r{_currentState} {dots}");
                Console.ResetColor();

                _animationFrame++;
            }
            catch
            {
                // Ignore any console errors during animation
            }
        }

        public void ClearStateLine()
        {
            // Stop animation timer
            if (_stateAnimationTimer != null)
            {
                _stateAnimationTimer.Dispose();
                _stateAnimationTimer = null;
                Console.Write("\r" + new string(' ', Console.WindowWidth));
                Console.Write("\r");
            }
        }

        public void ClearScreen()
        {
            Console.Clear();
        }

        private class TrackedMessage
        {
            public string Id { get; set; } = "";
            public string Text { get; set; } = "";
            public string Role { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public DateTime Timestamp { get; set; }
        }
    }
}
