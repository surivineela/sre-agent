using System.Text;
using System.Text.RegularExpressions;

namespace Agent.Plugins.Helpers;

public static class MarkdownEmojiSanitizer
{
    // Remove emojis with local, context-aware repair to preserve Markdown structure,
    // then normalize emphasis/list spacing to avoid broken Markdown.
    public static string RemoveEmojisPreserveMarkdown(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var sb = new StringBuilder(input.Length);
        bool pendingSeparator = false;
        Rune? lastKept = null;

        foreach (var r in input.EnumerateRunes())
        {
            if (IsEmojiRune(r))
            {
                pendingSeparator = true;
                continue;
            }

            if (pendingSeparator)
            {
                if (lastKept.HasValue &&
                    IsWordLike(lastKept.Value) &&
                    IsWordLike(r) &&
                    !IsEmphasisMarker(lastKept.Value) &&
                    !IsEmphasisMarker(r) &&
                    !IsWsOrNl(lastKept.Value) &&
                    !IsWsOrNl(r))
                {
                    sb.Append(' ');
                    lastKept = new Rune(' ');
                }
                pendingSeparator = false;
            }

            sb.Append(r);
            lastKept = r;
        }

        var s = sb.ToString();

        // Whitespace normalization
        // - Remove trailing spaces before newline (preserve newline style)
        s = Regex.Replace(s, "[ \t]+(\r?\n)", "$1");
        // - Collapse only in-line multiple spaces between non-space chars
        s = Regex.Replace(s, @"(?<=\S) {2,}(?=\S)", " ");

        // Markdown list spacing (conservative fixes)
        // Ensure one space after unordered list markers when the next token is an emphasis opener
        s = Regex.Replace(s, @"(?m)^(?<m>[ \t]*[-+*])[ \t]*(?=\*\*|__)", "${m} ");
        // Ensure one space after ordered list markers when the next token is an emphasis opener
        s = Regex.Replace(s, @"(?m)^(?<m>[ \t]*\d+\.)[ \t]*(?=\*\*|__)", "${m} ");

        // Seam-aware normalization for strong/bold spans: keep inner tight, adjust spaces outside
        s = NormalizeBoldSeams(s);   // **strong**
        s = NormalizeStrongSeams(s); // __strong__

        return s;
    }

    private static readonly Regex BoldSpan = new(@"\*\*(.+?)\*\*", RegexOptions.Singleline);
    private static readonly Regex StrongSpan = new(@"__(.+?)__", RegexOptions.Singleline);

    private static string NormalizeBoldSeams(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder(s.Length + 16);
        int last = 0;
        foreach (Match m in BoldSpan.Matches(s))
        {
            // append text before span
            if (m.Index > last)
            {
                sb.Append(s, last, m.Index - last);
            }

            // prepare inner (trim spaces inside ** **)
            var inner = m.Groups[1].Value;
            inner = Regex.Replace(inner, @"^[ \t]+|[ \t]+$", "");

            // ensure exactly one space before opener when needed
            if (sb.Length > 0)
            {
                var prev = sb[^1];
                if (prev != ' ' && prev != '\n' && prev != '\r' && prev != '\t')
                {
                    sb.Append(' ');
                }
            }

            // write normalized bold span
            sb.Append("**").Append(inner).Append("**");

            // ensure a space after closer when followed immediately by a letter/number
            int after = m.Index + m.Length;
            if (after < s.Length)
            {
                char next = s[after];
                if (!char.IsWhiteSpace(next) && char.IsLetterOrDigit(next))
                {
                    sb.Append(' ');
                }
            }

            last = m.Index + m.Length;
        }

        // append remainder
        if (last < s.Length)
        {
            sb.Append(s, last, s.Length - last);
        }

        return sb.ToString();
    }

    private static string NormalizeStrongSeams(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder(s.Length + 16);
        int last = 0;
        foreach (Match m in StrongSpan.Matches(s))
        {
            if (m.Index > last)
            {
                sb.Append(s, last, m.Index - last);
            }

            var inner = m.Groups[1].Value;
            inner = Regex.Replace(inner, @"^[ \t]+|[ \t]+$", "");

            if (sb.Length > 0)
            {
                var prev = sb[^1];
                if (prev != ' ' && prev != '\n' && prev != '\r' && prev != '\t')
                {
                    sb.Append(' ');
                }
            }

            sb.Append("__").Append(inner).Append("__");

            int after = m.Index + m.Length;
            if (after < s.Length)
            {
                char next = s[after];
                if (!char.IsWhiteSpace(next) && char.IsLetterOrDigit(next))
                {
                    sb.Append(' ');
                }
            }

            last = m.Index + m.Length;
        }

        if (last < s.Length)
        {
            sb.Append(s, last, s.Length - last);
        }

        return sb.ToString();
    }

    private static bool IsWordLike(Rune r) => char.IsLetterOrDigit((char)r.Value);
    private static bool IsWsOrNl(Rune r)
    {
        var c = (char)r.Value;
        return c == ' ' || c == '\t' || c == '\r' || c == '\n';
    }
    private static bool IsEmphasisMarker(Rune r)
    {
        var c = (char)r.Value;
        return c == '*' || c == '_';
    }

    public static bool IsEmojiRune(Rune r)
    {
        var v = r.Value;

        if (v == 0x200D || v == 0xFE0F) return true;               // ZWJ / VS16
        if (v >= 0x1F1E6 && v <= 0x1F1FF) return true;              // Regional indicators
        if (v >= 0x1F3FB && v <= 0x1F3FF) return true;              // Skin tones
        if (v >= 0x2600 && v <= 0x26FF) return true;                // Misc Symbols
        if (v >= 0x2700 && v <= 0x27BF) return true;                // Dingbats
        if (v >= 0x1F300 && v <= 0x1F5FF) return true;              // Misc Symbols & Pictographs
        if (v >= 0x1F600 && v <= 0x1F64F) return true;              // Emoticons
        if (v >= 0x1F680 && v <= 0x1F6FF) return true;              // Transport & Map
        if (v >= 0x1F700 && v <= 0x1F77F) return true;              // Alchemical Symbols
        if (v >= 0x1F780 && v <= 0x1F7FF) return true;              // Geometric Shapes Extended
        if (v >= 0x1F900 && v <= 0x1F9FF) return true;              // Supplemental Symbols & Pictographs
        if (v >= 0x1FA70 && v <= 0x1FAFF) return true;              // Symbols & Pictographs Extended-A

        return false;
    }
}
