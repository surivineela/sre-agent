// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;

namespace Agent.Cli.Helpers;

/// <summary>
/// Portable console UI system with graceful fallbacks for different terminal capabilities
/// </summary>
public static class ConsoleUI
{
    public static readonly Palette Chars;
    public static readonly bool SupportsColor;

    /// <summary>
    /// Test injection point for Console.ReadLine. When set, this function is called instead of Console.ReadLine().
    /// Used by E2E tests to simulate user input without requiring interactive console.
    /// </summary>
    public static Func<string?>? ReadLineHandler { get; set; }

    static ConsoleUI()
    {
        // Force UTF-8 when possible, but gracefully fall back (works in bash/cmd/PowerShell)
        try { Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false); } catch { /* ignore */ }

        bool unicodeOk = CanRoundTrip("✓──└┘█•→…—");
        Chars = unicodeOk ? Palette.Unicode : Palette.Ascii;

        // Respect NO_COLOR and redirection
        SupportsColor = !Console.IsOutputRedirected && Environment.GetEnvironmentVariable("NO_COLOR") is null;
    }

    private static bool CanRoundTrip(string s)
    {
        var enc = Console.OutputEncoding;
        var b = enc.GetBytes(s);
        var s2 = enc.GetString(b);
        return s2 == s;
    }

    public record Palette(
        string H, string V, string TL, string TR, string BL, string BR, string T, string B, string L, string R, string X,
        string Bullet, string ArrowRight, string Check, string Cross,
        string Full, string OneEighth, string TwoEighths, string ThreeEighths, string FourEighths, string FiveEighths, string SixEighths, string SevenEighths,
        string Ellipsis, string Dash
    )
    {
        public static readonly Palette Unicode = new(
            "─", "│", "┌", "┐", "└", "┘", "┬", "┴", "├", "┤", "┼",
            "•", "→", "✓", "✗",
            "█", "▏", "▎", "▍", "▌", "▋", "▊", "▉",
            "…", "—"
        );

        public static readonly Palette Ascii = new(
            "-", "|", "+", "+", "+", "+", "+", "+", "+", "+", "+",
            "*", ">", "[OK]", "[FAIL]",
            "#", ".", ".", ".", "=", "=", "=", "#",
            "...", "--"
        );
    }

    /// <summary>
    /// Executes an action with specified console color, then restores original color.
    /// Output: Executes the provided action with temporary color change (no direct output).
    /// </summary>
    public static void WithColor(ConsoleColor color, Action body)
    {
        var old = Console.ForegroundColor;
        if (SupportsColor) Console.ForegroundColor = color;
        try { body(); }
        finally { if (SupportsColor) Console.ForegroundColor = old; }
    }

    /// <summary>
    /// Draw a panel with title and content.
    /// Output: Bordered box with title on top line, separator, and content on bottom.
    /// Example: ┌──────┐
    ///          │ Title│
    ///          ├──────┤
    ///          │ Text │
    ///          └──────┘
    /// </summary>
    public static void DrawPanel(string title, string content, ConsoleColor titleColor = ConsoleColor.White)
    {
        int width = Math.Max(Math.Max(title.Length + 4, content.Length + 4), 20);
        string h = new string(Chars.H[0], width - 2);

        WithColor(titleColor, () =>
        {
            Console.WriteLine($"{Chars.TL}{h}{Chars.TR}");
            Console.WriteLine($"{Chars.V} {title.PadRight(width - 4)} {Chars.V}");
        });

        Console.WriteLine($"{Chars.L}{new string(Chars.H[0], width - 2)}{Chars.R}");
        Console.WriteLine($"{Chars.V} {content.PadRight(width - 4)} {Chars.V}");
        Console.WriteLine($"{Chars.BL}{new string(Chars.H[0], width - 2)}{Chars.BR}");
    }

    /// <summary>
    /// Draw a simple border line.
    /// Output: Horizontal line of specified length (e.g., "────────────────").
    /// </summary>
    public static void DrawLine(int length = 60, ConsoleColor color = ConsoleColor.Gray)
    {
        WithColor(color, () => Console.WriteLine(new string(Chars.H[0], length)));
    }

    /// <summary>
    /// Show a progress bar with precise fractional display.
    /// Output: Inline progress bar with carriage return (e.g., " 75% [███████▌        ] Processing files   ").
    /// </summary>
    public static void Progress(double percentage, string label, int width = -1)
    {
        if (width == -1)
            width = Console.IsOutputRedirected ? 40 : Math.Max(20, Console.WindowWidth - 20 - label.Length);

        int barWidth = Math.Max(10, width - 8);
        int full = (int)(percentage * barWidth);
        double frac = percentage * barWidth - full;

        string partial = Chars switch
        {
            { Full: "█" } when frac >= 7.0 / 8 => Chars.SevenEighths,
            { Full: "█" } when frac >= 6.0 / 8 => Chars.SixEighths,
            { Full: "█" } when frac >= 5.0 / 8 => Chars.FiveEighths,
            { Full: "█" } when frac >= 4.0 / 8 => Chars.FourEighths,
            { Full: "█" } when frac >= 3.0 / 8 => Chars.ThreeEighths,
            { Full: "█" } when frac >= 2.0 / 8 => Chars.TwoEighths,
            { Full: "█" } when frac >= 1.0 / 8 => Chars.OneEighth,
            _ => ""
        };

        string barStr = new string(Chars.Full[0], full) + partial;
        if (barStr.Length < barWidth) barStr = barStr.PadRight(barWidth, ' ');

        int percent = (int)(percentage * 100);
        Console.Write($"\r{percent,3}% [{barStr}] {label}   ");
    }

    /// <summary>
    /// Write a status message with appropriate symbol.
    /// Output: Single line with check/cross symbol (e.g., "✓ Operation completed" or "✗ Operation failed").
    /// </summary>
    public static void WriteStatus(bool success, string message, ConsoleColor? color = null)
    {
        var symbol = success ? Chars.Check : Chars.Cross;
        var defaultColor = success ? ConsoleColor.Green : ConsoleColor.Red;

        WithColor(color ?? defaultColor, () =>
        {
            Console.WriteLine($"{symbol} {message}");
        });
    }

    /// <summary>
    /// Write an info message with bullet point.
    /// Output: Single line with bullet (e.g., "• Information message").
    /// </summary>
    public static void WriteInfo(string message, ConsoleColor color = ConsoleColor.Cyan)
    {
        WithColor(color, () => Console.WriteLine($"{Chars.Bullet} {message}"));
    }

    /// <summary>
    /// Renders an "Examples:" block with consistent spacing and colors.
    /// Output: "Examples:" header followed by comment lines (prefixed with #) and command lines, with one trailing blank line.
    /// Example:
    ///   Examples:
    ///     # Create a new tool
    ///     srectl tool create --name mytool
    /// </summary>
    public static void WriteExamples((string Comment, string Command)[] examples, int indent = 2)
    {
        WithColor(ConsoleColor.DarkGray, () => Console.WriteLine("Examples:"));
        string pad = new string(' ', indent);

        for (int i = 0; i < examples.Length; i++)
        {
            var (comment, command) = examples[i];

            if (!string.IsNullOrWhiteSpace(comment))
                WithColor(ConsoleColor.DarkGray, () => Console.WriteLine($"{pad}# {comment}"));

            WithColor(ConsoleColor.White, () => Console.WriteLine($"{pad}{command}"));
            // no per-item blank line; keeps the block compact
        }

        Console.WriteLine(); // exactly one blank line after the examples block
    }

    /// <summary>
    /// One-shot renderer for a subcommand row + optional examples.
    /// Output: Key-value pair on one line (e.g., "create        : Creates a new tool"), optionally followed by examples block and blank line.
    /// </summary>
    public static void WriteSubcommand(
        string name,
        string description,
        (string Comment, string Command)[]? examples = null,
        int nameWidth = 15)
    {
        WriteKeyValue(name, description, nameWidth, ConsoleColor.Yellow);

        if (examples is { Length: > 0 })
        {
            Console.WriteLine();                 // space before "Examples:"
            WriteExamples(examples);             // includes one trailing blank line
        }
        else
        {
            Console.WriteLine();                 // one blank line between subcommands
        }
    }

    /// <summary>
    /// Write a bullet point for lists.
    /// Output: Indented line with bullet (e.g., "  • List item").
    /// </summary>
    public static void WriteBullet(string message, ConsoleColor color = ConsoleColor.Gray, int indent = 2)
    {
        string indentStr = new string(' ', indent);
        WithColor(color, () => Console.WriteLine($"{indentStr}{Chars.Bullet} {message}"));
    }

    /// <summary>
    /// Write a tree-style hierarchical item.
    /// Output: Tree branch connector with message (e.g., "├── Item" or "└── Last item").
    /// </summary>
    public static void WriteTreeItem(string message, bool isLast = false, int level = 0, ConsoleColor color = ConsoleColor.Gray)
    {
        string prefix = new string(' ', level * 2);
        string connector = isLast ? Chars.BL : Chars.L;
        string line = new string(Chars.H[0], 2);
        WithColor(color, () => Console.WriteLine($"{prefix}{connector}{line} {message}"));
    }

    /// <summary>
    /// Write plain text with optional color.
    /// Output: Single line with message (e.g., "This is a message").
    /// </summary>
    public static void Write(string message, ConsoleColor? color = null)
    {
        if (color.HasValue) WithColor(color.Value, () => Console.WriteLine(message));
        else Console.WriteLine(message);
    }

    /// <summary>
    /// Write a debug message in light gray for consistency across the CLI.
    /// Output: Single line in gray color (e.g., "Debug information").
    /// </summary>
    public static void WriteDebug(string message) => WithColor(ConsoleColor.Gray, () => Console.WriteLine(message));

    /// <summary>
    /// Write text without newline.
    /// Output: Inline text without line break (e.g., "Prompt: " waiting for input).
    /// </summary>
    public static void WriteInline(string message, ConsoleColor? color = null)
    {
        if (color.HasValue) WithColor(color.Value, () => Console.Write(message));
        else Console.Write(message);
        Console.Out.Flush(); // ensure the text shows up before input
    }

    /// <summary>
    /// Spinner animation frame (ASCII-safe).
    /// Output: Returns one character from spinner sequence ("|", "/", "-", "\").
    /// </summary>
    public static string GetSpinnerFrame(int frameIndex)
        => new[] { "|", "/", "-", "\\" }[frameIndex % 4];

    /// <summary>
    /// Section header with underline.
    /// Output: Title line followed by underline of equal length (e.g., "Commands\n────────"), with optional blank lines before/after.
    /// </summary>
    public static void WriteSection(string title, ConsoleColor color = ConsoleColor.White, bool topMargin = false, bool bottomMargin = false)
    {
        if (topMargin) Console.WriteLine();
        WithColor(color, () =>
        {
            Console.WriteLine(title);
            Console.WriteLine(new string(Chars.H[0], title.Length));
        });
        if (bottomMargin) Console.WriteLine();
    }

    /// <summary>
    /// Show a command example with proper formatting.
    /// Output: Single line with bullet, description, and command (e.g., "• Example: srectl tool create").
    /// </summary>
    public static void WriteCommand(string description, string command, ConsoleColor descColor = ConsoleColor.Gray, ConsoleColor cmdColor = ConsoleColor.Yellow)
    {
        WithColor(descColor, () => Console.Write($"{Chars.Bullet} {description}: "));
        WithColor(cmdColor, () => Console.WriteLine(command));
    }

    /// <summary>
    /// Show key-value pairs in a structured format.
    /// Output: Single line with padded key and value (e.g., "Name          : mytool").
    /// </summary>
    public static void WriteKeyValue(string key, string value, int keyWidth = 15, ConsoleColor keyColor = ConsoleColor.Cyan, ConsoleColor valueColor = ConsoleColor.White)
    {
        WithColor(keyColor, () => Console.Write($"{key.PadRight(keyWidth)}: "));
        WithColor(valueColor, () => Console.WriteLine(value));
    }

    /// <summary>
    /// Yes/No prompt.
    /// Output: Inline prompt with options (e.g., "? Delete this file? [y/N] "), waits for user input, returns bool.
    /// </summary>
    public static bool Confirm(string message, bool defaultYes = false)
    {
        var options = defaultYes ? "[Y/n]" : "[y/N]";
        WriteInline($"? {message} {options} ", ConsoleColor.Yellow);

        // Use test injection if available (for E2E tests)
        var input = (ReadLineHandler?.Invoke() ?? Console.ReadLine())?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(input)) return defaultYes;
        return input.StartsWith('y');
    }

    /// <summary>
    /// Clear the current line for spinner updates.
    /// Output: Carriage return + spaces to overwrite current line + carriage return (clears line).
    /// </summary>
    public static void ClearLine()
    {
        if (!Console.IsOutputRedirected)
            Console.Write("\r" + new string(' ', Math.Min(Console.WindowWidth - 1, 120)) + "\r");
    }

    /// <summary>
    /// Timestamp writer.
    /// Output: Single line with formatted timestamp (e.g., "[2025-12-06 14:30:00]").
    /// </summary>
    public static void WriteTimestamp(DateTime timestamp, ConsoleColor color = ConsoleColor.DarkGray)
        => WithColor(color, () => Console.WriteLine($"[{timestamp:yyyy-MM-dd HH:mm:ss}]"));

    /// <summary>
    /// Duration writer.
    /// Output: Single line with operation and formatted duration (e.g., "Operation completed in 1.5s" or "Operation completed in 2m 30s").
    /// </summary>
    public static void WriteDuration(TimeSpan duration, string operation = "Operation", ConsoleColor color = ConsoleColor.DarkGray)
    {
        var timeDisplay = duration.TotalSeconds < 1
            ? $"{duration.TotalMilliseconds:F0}ms"
            : duration.TotalMinutes >= 1
                ? $"{duration.Minutes}m {duration.Seconds}s"
                : $"{duration.TotalSeconds:F1}s";

        WithColor(color, () => Console.WriteLine($"{operation} completed in {timeDisplay}"));
    }

    /// <summary>
    /// Group of commands with consistent spacing (no per-item blank line).
    /// Output: Section header with underline, followed by key-value pairs for each command, and one trailing blank line.
    /// Example:
    ///   Commands
    ///   ────────
    ///   create        : Creates a new tool
    ///   delete        : Deletes a tool
    /// </summary>
    public static void WriteCommandGroup(string groupName, (string name, string description)[] commands)
    {
        WriteSection(groupName);
        foreach (var (name, description) in commands)
            WriteKeyValue(name, description, 15, ConsoleColor.Yellow);
        Console.WriteLine();
    }

    /// <summary>
    /// Capture ConsoleUI output to a string (for list commands).
    /// Output: Returns all console output from the action as a string (no direct console output).
    /// </summary>
    public static string CaptureOutput(Action outputAction)
    {
        using var writer = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            outputAction();
            return writer.ToString().TrimEnd();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
