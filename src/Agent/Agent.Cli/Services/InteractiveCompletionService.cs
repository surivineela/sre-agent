using System.CommandLine;

namespace Agent.Cli.Services;

/// <summary>
/// Provides interactive tab completion functionality for the CLI
/// </summary>
public class InteractiveCompletionService
{
    private readonly RootCommand _rootCommand;
    private readonly List<string> _completionHistory = new();

    public InteractiveCompletionService(RootCommand rootCommand)
    {
        _rootCommand = rootCommand;
    }

    /// <summary>
    /// Starts an interactive command input session with tab completion
    /// </summary>
    public string ReadCommandLine(string prompt = "srectl> ")
    {
        Console.Write(prompt);
        var currentInput = "";
        var cursorPosition = 0;
        
        while (true)
        {
            var keyInfo = Console.ReadKey(true);
            
            switch (keyInfo.Key)
            {
                case ConsoleKey.Enter:
                    Console.WriteLine();
                    if (!string.IsNullOrWhiteSpace(currentInput))
                    {
                        _completionHistory.Add(currentInput);
                    }
                    return currentInput;
                
                case ConsoleKey.Tab:
                    var completions = GetCompletions(currentInput);
                    if (completions.Any())
                    {
                        var selectedCompletion = ShowCompletionMenu(completions, currentInput);
                        if (selectedCompletion != null)
                        {
                            // Clear current line and show new input
                            ClearCurrentLine(prompt, currentInput);
                            currentInput = selectedCompletion;
                            cursorPosition = currentInput.Length;
                            Console.Write(prompt + currentInput);
                        }
                    }
                    break;
                
                case ConsoleKey.Backspace:
                    if (cursorPosition > 0)
                    {
                        currentInput = currentInput.Remove(cursorPosition - 1, 1);
                        cursorPosition--;
                        RedrawLine(prompt, currentInput, cursorPosition);
                    }
                    break;
                
                case ConsoleKey.Delete:
                    if (cursorPosition < currentInput.Length)
                    {
                        currentInput = currentInput.Remove(cursorPosition, 1);
                        RedrawLine(prompt, currentInput, cursorPosition);
                    }
                    break;
                
                case ConsoleKey.LeftArrow:
                    if (cursorPosition > 0)
                    {
                        cursorPosition--;
                        Console.SetCursorPosition(prompt.Length + cursorPosition, Console.CursorTop);
                    }
                    break;
                
                case ConsoleKey.RightArrow:
                    if (cursorPosition < currentInput.Length)
                    {
                        cursorPosition++;
                        Console.SetCursorPosition(prompt.Length + cursorPosition, Console.CursorTop);
                    }
                    break;
                
                case ConsoleKey.Home:
                    cursorPosition = 0;
                    Console.SetCursorPosition(prompt.Length, Console.CursorTop);
                    break;
                
                case ConsoleKey.End:
                    cursorPosition = currentInput.Length;
                    Console.SetCursorPosition(prompt.Length + cursorPosition, Console.CursorTop);
                    break;
                
                case ConsoleKey.UpArrow:
                    // Command history - previous command
                    if (_completionHistory.Count > 0)
                    {
                        var prevCommand = _completionHistory.LastOrDefault();
                        if (prevCommand != null)
                        {
                            ClearCurrentLine(prompt, currentInput);
                            currentInput = prevCommand;
                            cursorPosition = currentInput.Length;
                            Console.Write(prompt + currentInput);
                        }
                    }
                    break;
                
                case ConsoleKey.Escape:
                    // Clear current input
                    ClearCurrentLine(prompt, currentInput);
                    currentInput = "";
                    cursorPosition = 0;
                    Console.Write(prompt);
                    break;
                
                default:
                    // Regular character input
                    if (!char.IsControl(keyInfo.KeyChar))
                    {
                        currentInput = currentInput.Insert(cursorPosition, keyInfo.KeyChar.ToString());
                        cursorPosition++;
                        RedrawLine(prompt, currentInput, cursorPosition);
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Get available completions for the current input
    /// </summary>
    private List<string> GetCompletions(string input)
    {
        var completions = new List<string>();
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length == 0)
        {
            // Root level commands
            completions.AddRange(GetRootCommands());
        }
        else if (parts.Length == 1)
        {
            // Partial command name - filter root commands
            var partial = parts[0];
            completions.AddRange(GetRootCommands().Where(cmd => cmd.StartsWith(partial, StringComparison.OrdinalIgnoreCase)));
        }
        else
        {
            // Subcommands and options
            completions.AddRange(GetSubcommandCompletions(parts));
        }
        
        return completions.Distinct().ToList();
    }

    /// <summary>
    /// Get root level commands
    /// </summary>
    private List<string> GetRootCommands()
    {
        var commands = new List<string>();
        
        foreach (var subcommand in _rootCommand.Subcommands)
        {
            commands.Add(subcommand.Name);
        }
        
        // Add global options
        foreach (var option in _rootCommand.Options)
        {
            commands.Add($"--{option.Name}");
        }
        
        return commands;
    }

    /// <summary>
    /// Get subcommand completions based on the current command context
    /// </summary>
    private List<string> GetSubcommandCompletions(string[] parts)
    {
        var completions = new List<string>();
        var currentCommand = parts[0];
        
        // Find the matching root command
        var rootSubcommand = _rootCommand.Subcommands.FirstOrDefault(c => 
            c.Name.Equals(currentCommand, StringComparison.OrdinalIgnoreCase));
        
        if (rootSubcommand == null)
            return completions;
        
        if (parts.Length == 2)
        {
            // Second level - subcommands of the root command
            var partial = parts[1];
            foreach (var subcommand in rootSubcommand.Subcommands)
            {
                if (subcommand.Name.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
                {
                    completions.Add(subcommand.Name);
                }
            }
            
            // Add options for this command
            foreach (var option in rootSubcommand.Options)
            {
                var optionName = $"--{option.Name}";
                if (optionName.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
                {
                    completions.Add(optionName);
                }
            }
        }
        else if (parts.Length >= 3)
        {
            // Third level and beyond - options for specific subcommands
            var subcommandName = parts[1];
            var subcommand = rootSubcommand.Subcommands.FirstOrDefault(c => 
                c.Name.Equals(subcommandName, StringComparison.OrdinalIgnoreCase));
            
            if (subcommand != null)
            {
                var partial = parts.LastOrDefault() ?? "";
                foreach (var option in subcommand.Options)
                {
                    var optionName = $"--{option.Name}";
                    if (optionName.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
                    {
                        completions.Add(optionName);
                    }
                }
            }
        }
        
        return completions;
    }

    /// <summary>
    /// Show interactive completion menu with arrow key navigation
    /// </summary>
    private string? ShowCompletionMenu(List<string> completions, string currentInput)
    {
        if (completions.Count == 1)
        {
            // Auto-complete if only one option
            return CompleteSingleOption(completions[0], currentInput);
        }

        // Save current cursor position
        var originalCursorTop = Console.CursorTop;
        var originalCursorLeft = Console.CursorLeft;
        
        // Show completions menu
        Console.WriteLine();
        Console.WriteLine($"  Available completions ({completions.Count}):");
        
        var selectedIndex = 0;
        var maxDisplayItems = Math.Min(10, completions.Count); // Show max 10 items at once
        var startIndex = 0;
        
        while (true)
        {
            // Display completion options
            Console.SetCursorPosition(0, originalCursorTop + 2);
            
            for (int i = 0; i < maxDisplayItems; i++)
            {
                var itemIndex = startIndex + i;
                if (itemIndex >= completions.Count) break;
                
                var item = completions[itemIndex];
                var isSelected = itemIndex == selectedIndex;
                
                if (isSelected)
                {
                    Console.BackgroundColor = ConsoleColor.Blue;
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write($"  > {item}");
                }
                else
                {
                    Console.ResetColor();
                    Console.Write($"    {item}");
                }
                
                // Clear to end of line and move to next
                Console.Write(new string(' ', Math.Max(0, Console.WindowWidth - Console.CursorLeft - 1)));
                Console.WriteLine();
            }
            
            Console.ResetColor();
            
            // Show scroll indicators if needed
            if (completions.Count > maxDisplayItems)
            {
                var totalPages = (int)Math.Ceiling((double)completions.Count / maxDisplayItems);
                var currentPage = (startIndex / maxDisplayItems) + 1;
                Console.WriteLine($"  Page {currentPage}/{totalPages} - Use ↑↓ to navigate, Tab/Enter to select, Esc to cancel");
            }
            else
            {
                Console.WriteLine("  Use ↑↓ to navigate, Tab/Enter to select, Esc to cancel");
            }
            
            // Handle navigation
            var keyInfo = Console.ReadKey(true);
            
            switch (keyInfo.Key)
            {
                case ConsoleKey.UpArrow:
                    if (selectedIndex > 0)
                    {
                        selectedIndex--;
                        if (selectedIndex < startIndex)
                        {
                            startIndex = Math.Max(0, startIndex - maxDisplayItems);
                        }
                    }
                    break;
                
                case ConsoleKey.DownArrow:
                    if (selectedIndex < completions.Count - 1)
                    {
                        selectedIndex++;
                        if (selectedIndex >= startIndex + maxDisplayItems)
                        {
                            startIndex = Math.Min(completions.Count - maxDisplayItems, startIndex + maxDisplayItems);
                        }
                    }
                    break;
                
                case ConsoleKey.PageUp:
                    selectedIndex = Math.Max(0, selectedIndex - maxDisplayItems);
                    startIndex = Math.Max(0, startIndex - maxDisplayItems);
                    break;
                
                case ConsoleKey.PageDown:
                    selectedIndex = Math.Min(completions.Count - 1, selectedIndex + maxDisplayItems);
                    startIndex = Math.Min(completions.Count - maxDisplayItems, startIndex + maxDisplayItems);
                    break;
                
                case ConsoleKey.Home:
                    selectedIndex = 0;
                    startIndex = 0;
                    break;
                
                case ConsoleKey.End:
                    selectedIndex = completions.Count - 1;
                    startIndex = Math.Max(0, completions.Count - maxDisplayItems);
                    break;
                
                case ConsoleKey.Enter:
                case ConsoleKey.Tab:
                    // Select current item
                    ClearCompletionMenu(originalCursorTop, maxDisplayItems + 3);
                    return CompleteSingleOption(completions[selectedIndex], currentInput);
                
                case ConsoleKey.Escape:
                    // Cancel completion
                    ClearCompletionMenu(originalCursorTop, maxDisplayItems + 3);
                    return null;
                
                default:
                    // For other keys, cancel completion and handle the key normally
                    ClearCompletionMenu(originalCursorTop, maxDisplayItems + 3);
                    return null;
            }
        }
    }

    /// <summary>
    /// Complete a single option by replacing or extending the current input
    /// </summary>
    private string CompleteSingleOption(string completion, string currentInput)
    {
        var parts = currentInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length == 0)
        {
            return completion;
        }
        
        // Replace the last partial word with the completion
        var lastPart = parts.LastOrDefault() ?? "";
        if (completion.StartsWith(lastPart, StringComparison.OrdinalIgnoreCase))
        {
            // Complete the partial word
            parts[parts.Length - 1] = completion;
            return string.Join(" ", parts) + " ";
        }
        else
        {
            // Add as new word
            return currentInput.TrimEnd() + " " + completion + " ";
        }
    }

    /// <summary>
    /// Clear the completion menu from the console
    /// </summary>
    private void ClearCompletionMenu(int originalCursorTop, int linesToClear)
    {
        // Move to the start of the completion menu
        Console.SetCursorPosition(0, originalCursorTop + 1);
        
        // Clear the lines
        for (int i = 0; i < linesToClear; i++)
        {
            Console.Write(new string(' ', Console.WindowWidth));
            if (i < linesToClear - 1) Console.WriteLine();
        }
        
        // Restore cursor to original input position
        Console.SetCursorPosition(0, originalCursorTop);
    }

    /// <summary>
    /// Clear the current input line
    /// </summary>
    private void ClearCurrentLine(string prompt, string currentInput)
    {
        Console.SetCursorPosition(0, Console.CursorTop);
        Console.Write(new string(' ', prompt.Length + currentInput.Length + 5));
        Console.SetCursorPosition(0, Console.CursorTop);
    }

    /// <summary>
    /// Redraw the input line with the cursor at the specified position
    /// </summary>
    private void RedrawLine(string prompt, string input, int cursorPosition)
    {
        Console.SetCursorPosition(0, Console.CursorTop);
        Console.Write(new string(' ', Console.WindowWidth - 1));
        Console.SetCursorPosition(0, Console.CursorTop);
        Console.Write(prompt + input);
        Console.SetCursorPosition(prompt.Length + cursorPosition, Console.CursorTop);
    }
}