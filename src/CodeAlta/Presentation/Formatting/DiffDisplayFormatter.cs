using System.Globalization;
using System.Text;
using CodeAlta.Models;
using CodeAlta.Presentation.Styling;
using XenoAtom.Ansi;

namespace CodeAlta.Presentation.Formatting;

internal static class DiffDisplayFormatter
{
    public static void AppendChangeCountsMarkup(StringBuilder builder, int additions, int deletions)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Append('[')
            .Append(UiPalette.GetToolStatusMarkup(ToolCallDisplayStatus.Completed))
            .Append("]+")
            .Append(additions.ToString(CultureInfo.InvariantCulture))
            .Append("[/] [")
            .Append(UiPalette.GetToolStatusMarkup(ToolCallDisplayStatus.Failed))
            .Append("]-")
            .Append(deletions.ToString(CultureInfo.InvariantCulture))
            .Append("[/]");
    }

    public static bool TryGetDiffStats(string? diffText, out int additions, out int deletions)
    {
        additions = 0;
        deletions = 0;
        if (string.IsNullOrWhiteSpace(diffText))
        {
            return false;
        }

        foreach (var line in SplitLines(diffText!))
        {
            if (line.StartsWith("+++", StringComparison.Ordinal) || line.StartsWith("---", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith('+'))
            {
                additions++;
            }
            else if (line.StartsWith('-'))
            {
                deletions++;
            }
        }

        return additions > 0 || deletions > 0;
    }

    public static string GetDiffLineMarkup(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        string? markup = line switch
        {
            _ when line.StartsWith('+') && !line.StartsWith("+++", StringComparison.Ordinal)
                => UiPalette.GetToolStatusMarkup(ToolCallDisplayStatus.Completed),
            _ when line.StartsWith('-') && !line.StartsWith("---", StringComparison.Ordinal)
                => UiPalette.GetToolStatusMarkup(ToolCallDisplayStatus.Failed),
            _ when line.StartsWith("@@", StringComparison.Ordinal)
                => UiPalette.GetToolStatusMarkup(ToolCallDisplayStatus.Running),
            _ when line.StartsWith("diff --git", StringComparison.Ordinal)
                || line.StartsWith("--- ", StringComparison.Ordinal)
                || line.StartsWith("+++ ", StringComparison.Ordinal)
                || line.StartsWith("index ", StringComparison.Ordinal)
                || line.StartsWith("new file mode", StringComparison.Ordinal)
                || line.StartsWith("deleted file mode", StringComparison.Ordinal)
                => UiPalette.MutedMarkup,
            _ => null,
        };

        var escaped = AnsiMarkup.Escape(line);
        return string.IsNullOrWhiteSpace(markup)
            ? escaped
            : $"[{markup}]{escaped}[/]";
    }

    public static string CreateUnifiedDiff(string oldText, string newText, string oldLabel, string newLabel)
    {
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);
        ArgumentException.ThrowIfNullOrWhiteSpace(oldLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(newLabel);

        if (string.Equals(oldText, newText, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var oldLines = SplitLines(oldText.TrimEnd());
        var newLines = SplitLines(newText.TrimEnd());
        var operations = BuildLineOperations(oldLines, newLines);
        var builder = new StringBuilder()
            .Append("--- ").AppendLine(oldLabel)
            .Append("+++ ").AppendLine(newLabel)
            .AppendLine("@@ full prompt @@");

        foreach (var operation in operations)
        {
            builder.Append(operation.Prefix).AppendLine(operation.Text);
        }

        return builder.ToString().TrimEnd();
    }

    public static string CreateDiffCodeBlock(string diffText)
    {
        ArgumentNullException.ThrowIfNull(diffText);
        var fence = CreateFence(diffText);
        return new StringBuilder()
            .Append(fence).AppendLine("diff")
            .AppendLine(diffText.TrimEnd())
            .Append(fence)
            .ToString();
    }

    private static IReadOnlyList<DiffLineOperation> BuildLineOperations(IReadOnlyList<string> oldLines, IReadOnlyList<string> newLines)
    {
        var lengths = new int[oldLines.Count + 1, newLines.Count + 1];
        for (var oldIndex = oldLines.Count - 1; oldIndex >= 0; oldIndex--)
        {
            for (var newIndex = newLines.Count - 1; newIndex >= 0; newIndex--)
            {
                lengths[oldIndex, newIndex] = string.Equals(oldLines[oldIndex], newLines[newIndex], StringComparison.Ordinal)
                    ? lengths[oldIndex + 1, newIndex + 1] + 1
                    : Math.Max(lengths[oldIndex + 1, newIndex], lengths[oldIndex, newIndex + 1]);
            }
        }

        var operations = new List<DiffLineOperation>();
        var i = 0;
        var j = 0;
        while (i < oldLines.Count && j < newLines.Count)
        {
            if (string.Equals(oldLines[i], newLines[j], StringComparison.Ordinal))
            {
                operations.Add(new DiffLineOperation(' ', oldLines[i]));
                i++;
                j++;
            }
            else if (lengths[i + 1, j] >= lengths[i, j + 1])
            {
                operations.Add(new DiffLineOperation('-', oldLines[i]));
                i++;
            }
            else
            {
                operations.Add(new DiffLineOperation('+', newLines[j]));
                j++;
            }
        }

        while (i < oldLines.Count)
        {
            operations.Add(new DiffLineOperation('-', oldLines[i++]));
        }

        while (j < newLines.Count)
        {
            operations.Add(new DiffLineOperation('+', newLines[j++]));
        }

        return operations;
    }

    private static string[] SplitLines(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

    private static string CreateFence(string text)
    {
        var maxRun = 0;
        var currentRun = 0;
        foreach (var character in text)
        {
            if (character == '`')
            {
                currentRun++;
                maxRun = Math.Max(maxRun, currentRun);
            }
            else
            {
                currentRun = 0;
            }
        }

        return new string('`', Math.Max(3, maxRun + 1));
    }

    private readonly record struct DiffLineOperation(char Prefix, string Text);
}
