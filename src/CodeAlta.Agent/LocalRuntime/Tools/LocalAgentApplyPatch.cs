using System.Text;

namespace CodeAlta.Agent.LocalRuntime.Tools;

internal static class LocalAgentApplyPatch
{
    public static AgentToolResult Apply(string input, string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        if (!TryParse(input, out var document, out var error))
        {
            return new AgentToolResult(false, [new AgentToolResultItem.Text(error)], error);
        }

        var rootPath = Path.GetFullPath(workingDirectory);
        var summaries = new List<string>(document.Operations.Count);

        foreach (var operation in document.Operations)
        {
            switch (operation)
            {
                case AddFileOperation addFile:
                {
                    var path = ResolvePatchPath(rootPath, addFile.Path);
                    if (File.Exists(path))
                    {
                        return Failure($"Cannot add '{addFile.Path}' because it already exists.");
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    var newline = Environment.NewLine;
                    var content = string.Join(newline, addFile.Lines);
                    if (addFile.Lines.Count > 0)
                    {
                        content += newline;
                    }

                    File.WriteAllText(path, content);
                    summaries.Add($"A {addFile.Path}");
                    break;
                }
                case DeleteFileOperation deleteFile:
                {
                    var path = ResolvePatchPath(rootPath, deleteFile.Path);
                    if (!File.Exists(path))
                    {
                        return Failure($"Cannot delete '{deleteFile.Path}' because it does not exist.");
                    }

                    File.Delete(path);
                    summaries.Add($"D {deleteFile.Path}");
                    break;
                }
                case UpdateFileOperation updateFile:
                {
                    var sourcePath = ResolvePatchPath(rootPath, updateFile.Path);
                    if (!File.Exists(sourcePath))
                    {
                        return Failure($"Cannot update '{updateFile.Path}' because it does not exist.");
                    }

                    var originalText = File.ReadAllText(sourcePath);
                    var newline = DetectNewline(originalText);
                    var hadTrailingNewline = HasTrailingNewline(originalText);
                    var lines = SplitLines(originalText);
                    if (!TryApplyHunks(lines, updateFile.Hunks, out var updatedLines, out error))
                    {
                        return Failure($"Failed to apply patch to '{updateFile.Path}': {error}");
                    }

                    var updatedText = JoinLines(updatedLines, newline, hadTrailingNewline);
                    if (updateFile.MoveTo is null)
                    {
                        File.WriteAllText(sourcePath, updatedText);
                        summaries.Add($"M {updateFile.Path}");
                        break;
                    }

                    var destinationPath = ResolvePatchPath(rootPath, updateFile.MoveTo);
                    if (File.Exists(destinationPath))
                    {
                        return Failure($"Cannot move '{updateFile.Path}' to '{updateFile.MoveTo}' because the destination already exists.");
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                    File.WriteAllText(destinationPath, updatedText);
                    File.Delete(sourcePath);
                    summaries.Add($"R {updateFile.Path} -> {updateFile.MoveTo}");
                    break;
                }
                default:
                    return Failure($"Unsupported patch operation '{operation.GetType().Name}'.");
            }
        }

        var summaryText = summaries.Count == 0
            ? "Patch applied with no file changes."
            : $"Patch applied:{Environment.NewLine}{string.Join(Environment.NewLine, summaries)}";
        return new AgentToolResult(true, [new AgentToolResultItem.Text(summaryText)]);
    }

    public static IReadOnlyList<string> GetTouchedPaths(string input, string workingDirectory)
    {
        if (!TryParse(input, out var document, out _))
        {
            return [];
        }

        var rootPath = Path.GetFullPath(workingDirectory);
        var paths = new List<string>();
        foreach (var operation in document.Operations)
        {
            switch (operation)
            {
                case AddFileOperation addFile:
                    paths.Add(ResolvePatchPath(rootPath, addFile.Path));
                    break;
                case DeleteFileOperation deleteFile:
                    paths.Add(ResolvePatchPath(rootPath, deleteFile.Path));
                    break;
                case UpdateFileOperation updateFile:
                    paths.Add(ResolvePatchPath(rootPath, updateFile.Path));
                    if (updateFile.MoveTo is not null)
                    {
                        paths.Add(ResolvePatchPath(rootPath, updateFile.MoveTo));
                    }

                    break;
            }
        }

        return paths;
    }

    private static AgentToolResult Failure(string message)
        => new(false, [new AgentToolResultItem.Text(message)], message);

    private static string ResolvePatchPath(string workingDirectory, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException($"Patch paths must be relative: '{relativePath}'.");
        }

        var fullPath = Path.GetFullPath(Path.Combine(workingDirectory, relativePath));
        if (!fullPath.StartsWith(workingDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Patch path '{relativePath}' escapes the working directory.");
        }

        return fullPath;
    }

    private static bool TryApplyHunks(
        List<string> lines,
        IReadOnlyList<PatchHunk> hunks,
        out List<string> updatedLines,
        out string error)
    {
        updatedLines = [.. lines];
        var currentIndex = 0;

        foreach (var hunk in hunks)
        {
            var oldLines = hunk.Lines
                .Where(static line => line.Kind is PatchLineKind.Context or PatchLineKind.Remove)
                .Select(static line => line.Text)
                .ToArray();
            var newLines = hunk.Lines
                .Where(static line => line.Kind is PatchLineKind.Context or PatchLineKind.Add)
                .Select(static line => line.Text)
                .ToArray();

            var matchIndex = oldLines.Length == 0
                ? currentIndex
                : FindMatch(updatedLines, oldLines, currentIndex);
            if (matchIndex < 0)
            {
                matchIndex = FindMatch(updatedLines, oldLines, 0);
            }

            if (matchIndex < 0)
            {
                error = "The hunk context was not found in the target file.";
                return false;
            }

            updatedLines.RemoveRange(matchIndex, oldLines.Length);
            updatedLines.InsertRange(matchIndex, newLines);
            currentIndex = matchIndex + newLines.Length;
        }

        error = string.Empty;
        return true;
    }

    private static int FindMatch(IReadOnlyList<string> lines, IReadOnlyList<string> hunkLines, int startIndex)
    {
        if (hunkLines.Count == 0)
        {
            return Math.Clamp(startIndex, 0, lines.Count);
        }

        for (var candidate = Math.Max(0, startIndex); candidate <= lines.Count - hunkLines.Count; candidate++)
        {
            var matches = true;
            for (var index = 0; index < hunkLines.Count; index++)
            {
                if (!string.Equals(lines[candidate + index], hunkLines[index], StringComparison.Ordinal))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return candidate;
            }
        }

        return -1;
    }

    private static bool TryParse(string input, out PatchDocument document, out string error)
    {
        var normalized = input.Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n');
        var state = new ParseState(lines);

        if (!state.TryReadExact("*** Begin Patch"))
        {
            document = PatchDocument.Empty;
            error = "Patch input must start with '*** Begin Patch'.";
            return false;
        }

        var operations = new List<PatchOperation>();
        while (!state.IsAtEnd)
        {
            if (state.CurrentLine is "*** End Patch")
            {
                state.Advance();
                document = new PatchDocument(operations);
                error = string.Empty;
                return true;
            }

            if (state.CurrentLine is null)
            {
                break;
            }

            if (state.CurrentLine.StartsWith("*** Add File: ", StringComparison.Ordinal))
            {
                var path = state.CurrentLine["*** Add File: ".Length..];
                state.Advance();
                var contentLines = new List<string>();
                while (!state.IsAtEnd && !IsFileHeaderOrEnd(state.CurrentLine))
                {
                    if (!state.CurrentLine.StartsWith('+'))
                    {
                        document = PatchDocument.Empty;
                        error = $"Added file lines must start with '+': '{state.CurrentLine}'.";
                        return false;
                    }

                    contentLines.Add(state.CurrentLine[1..]);
                    state.Advance();
                }

                operations.Add(new AddFileOperation(path, contentLines));
                continue;
            }

            if (state.CurrentLine.StartsWith("*** Delete File: ", StringComparison.Ordinal))
            {
                var path = state.CurrentLine["*** Delete File: ".Length..];
                operations.Add(new DeleteFileOperation(path));
                state.Advance();
                continue;
            }

            if (state.CurrentLine.StartsWith("*** Update File: ", StringComparison.Ordinal))
            {
                var path = state.CurrentLine["*** Update File: ".Length..];
                state.Advance();

                string? moveTo = null;
                if (!state.IsAtEnd && state.CurrentLine.StartsWith("*** Move to: ", StringComparison.Ordinal))
                {
                    moveTo = state.CurrentLine["*** Move to: ".Length..];
                    state.Advance();
                }

                var hunks = new List<PatchHunk>();
                while (!state.IsAtEnd && !IsFileHeaderOrEnd(state.CurrentLine))
                {
                    if (!state.CurrentLine.StartsWith("@@", StringComparison.Ordinal))
                    {
                        document = PatchDocument.Empty;
                        error = $"Expected hunk header, found '{state.CurrentLine}'.";
                        return false;
                    }

                    state.Advance();
                    var hunkLines = new List<PatchLine>();
                    while (!state.IsAtEnd)
                    {
                        if (state.CurrentLine is "*** End of File")
                        {
                            state.Advance();
                            break;
                        }

                        if (state.CurrentLine.StartsWith("@@", StringComparison.Ordinal) ||
                            IsFileHeaderOrEnd(state.CurrentLine))
                        {
                            break;
                        }

                        if (state.CurrentLine.Length == 0)
                        {
                            document = PatchDocument.Empty;
                            error = "Hunk lines must begin with ' ', '+' or '-'.";
                            return false;
                        }

                        var kind = state.CurrentLine[0] switch
                        {
                            ' ' => PatchLineKind.Context,
                            '+' => PatchLineKind.Add,
                            '-' => PatchLineKind.Remove,
                            _ => PatchLineKind.Invalid,
                        };
                        if (kind == PatchLineKind.Invalid)
                        {
                            document = PatchDocument.Empty;
                            error = $"Invalid hunk line '{state.CurrentLine}'.";
                            return false;
                        }

                        hunkLines.Add(new PatchLine(kind, state.CurrentLine[1..]));
                        state.Advance();
                    }

                    if (hunkLines.Count == 0)
                    {
                        document = PatchDocument.Empty;
                        error = "Each hunk must contain at least one line.";
                        return false;
                    }

                    hunks.Add(new PatchHunk(hunkLines));
                }

                if (hunks.Count == 0)
                {
                    document = PatchDocument.Empty;
                    error = $"Updated file '{path}' did not contain any hunks.";
                    return false;
                }

                operations.Add(new UpdateFileOperation(path, moveTo, hunks));
                continue;
            }

            document = PatchDocument.Empty;
            error = $"Unexpected patch line '{state.CurrentLine}'.";
            return false;
        }

        document = PatchDocument.Empty;
        error = "Patch input must end with '*** End Patch'.";
        return false;
    }

    private static bool IsFileHeaderOrEnd(string? line)
        => line is not null && (line.StartsWith("*** Add File: ", StringComparison.Ordinal) ||
                                line.StartsWith("*** Delete File: ", StringComparison.Ordinal) ||
                                line.StartsWith("*** Update File: ", StringComparison.Ordinal) ||
                                string.Equals(line, "*** End Patch", StringComparison.Ordinal));

    private static string DetectNewline(string text)
        => text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    private static bool HasTrailingNewline(string text)
        => text.EndsWith("\r\n", StringComparison.Ordinal) || text.EndsWith('\n');

    private static List<string> SplitLines(string text)
    {
        if (text.Length == 0)
        {
            return [];
        }

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n').ToList();
        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    private static string JoinLines(IReadOnlyList<string> lines, string newline, bool hadTrailingNewline)
    {
        if (lines.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var index = 0; index < lines.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(newline);
            }

            builder.Append(lines[index]);
        }

        if (hadTrailingNewline)
        {
            builder.Append(newline);
        }

        return builder.ToString();
    }

    private sealed record PatchDocument(IReadOnlyList<PatchOperation> Operations)
    {
        public static PatchDocument Empty { get; } = new([]);
    }

    private abstract record PatchOperation;

    private sealed record AddFileOperation(string Path, IReadOnlyList<string> Lines) : PatchOperation;

    private sealed record DeleteFileOperation(string Path) : PatchOperation;

    private sealed record UpdateFileOperation(string Path, string? MoveTo, IReadOnlyList<PatchHunk> Hunks) : PatchOperation;

    private sealed record PatchHunk(IReadOnlyList<PatchLine> Lines);

    private sealed record PatchLine(PatchLineKind Kind, string Text);

    private enum PatchLineKind
    {
        Invalid,
        Context,
        Add,
        Remove,
    }

    private sealed class ParseState(string[] lines)
    {
        private int _index;

        public bool IsAtEnd => _index >= lines.Length;

        public string CurrentLine => lines[_index];

        public void Advance() => _index++;

        public bool TryReadExact(string value)
        {
            if (IsAtEnd || !string.Equals(CurrentLine, value, StringComparison.Ordinal))
            {
                return false;
            }

            Advance();
            return true;
        }
    }
}
