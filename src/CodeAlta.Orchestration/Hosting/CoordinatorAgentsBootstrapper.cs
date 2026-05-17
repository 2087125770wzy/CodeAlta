using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XenoAtom.Logging;

namespace CodeAlta.Orchestration.Hosting;

/// <summary>
/// Bootstraps and refreshes the global coordinator <c>AGENTS.md</c> file under the CodeAlta global root.
/// </summary>
public static class CoordinatorAgentsBootstrapper
{
    private const string TemplateVersion = "2026-05-10";
    private const string ManagedBeginPrefix = "<!-- CodeAlta:coordinator-managed:begin";
    private const string ManagedEndMarker = "<!-- CodeAlta:coordinator-managed:end -->";
    private const string LocalBeginMarker = "<!-- CodeAlta:local-instructions:begin -->";
    private const string LocalEndMarker = "<!-- CodeAlta:local-instructions:end -->";
    private const string AltaHelpPlaceholder = "{{ALTA_HELP}}";
    private const string LocalInstructionsPlaceholder = "### Local instructions\n\nAdd local coordinator preferences here. CodeAlta preserves this section.";
    private const string FallbackAltaHelp = "Run `alta --help` in a CodeAlta session to inspect the current live-tool command surface.";
    private static readonly Logger Logger = LogManager.GetLogger("CodeAlta.CoordinatorAgents");

    /// <summary>
    /// Ensures the global coordinator <c>AGENTS.md</c> exists and has the current managed block.
    /// </summary>
    /// <param name="globalRoot">The CodeAlta global root, normally <c>~/.alta</c>.</param>
    /// <param name="altaHelpText">Optional generated <c>alta --help</c> text to embed in the managed coordinator guidance.</param>
    /// <returns>A bootstrap result describing what changed.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="globalRoot"/> is empty.</exception>
    public static CoordinatorAgentsBootstrapResult Ensure(string globalRoot, string? altaHelpText = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(globalRoot);
        Directory.CreateDirectory(globalRoot);

        var targetPath = Path.Combine(globalRoot, "AGENTS.md");
        if (!File.Exists(targetPath))
        {
            var initialManagedBlock = BuildManagedBlock(LoadManagedBody(altaHelpText));
            File.WriteAllText(targetPath, BuildFile(initialManagedBlock, BuildLocalBlock(LocalInstructionsPlaceholder)), Encoding.UTF8);
            return new CoordinatorAgentsBootstrapResult(targetPath, CoordinatorAgentsBootstrapAction.Created);
        }

        var current = File.ReadAllText(targetPath, Encoding.UTF8);
        var markers = FindMarkers(current);
        if (markers.HasNoMarkers)
        {
            var migratedManagedBlock = BuildManagedBlock(LoadManagedBody(altaHelpText));
            var backupPath = CreateBackup(targetPath);
            var migratedLocalBlock = BuildLocalBlock(current.TrimEnd('\r', '\n'));
            File.WriteAllText(targetPath, BuildFile(migratedManagedBlock, migratedLocalBlock), Encoding.UTF8);
            return new CoordinatorAgentsBootstrapResult(targetPath, CoordinatorAgentsBootstrapAction.Migrated, BackupPath: backupPath);
        }

        if (!markers.IsValid)
        {
            var message = "The global coordinator AGENTS.md markers are damaged or duplicated; leaving the file unchanged.";
            LogWarning(message);
            return new CoordinatorAgentsBootstrapResult(
                targetPath,
                CoordinatorAgentsBootstrapAction.Unchanged,
                Diagnostics: [message]);
        }

        var currentManagedBlock = current[markers.ManagedBeginIndex..(markers.ManagedEndIndex + ManagedEndMarker.Length)];
        var effectiveAltaHelpText = string.IsNullOrWhiteSpace(altaHelpText)
            ? ExtractManagedAltaHelp(currentManagedBlock)
            : altaHelpText;
        var managedBlock = BuildManagedBlock(LoadManagedBody(effectiveAltaHelpText));
        if (string.Equals(currentManagedBlock, managedBlock, StringComparison.Ordinal))
        {
            return new CoordinatorAgentsBootstrapResult(targetPath, CoordinatorAgentsBootstrapAction.Unchanged);
        }

        var localBlock = current[markers.LocalBeginIndex..(markers.LocalEndIndex + LocalEndMarker.Length)];
        File.WriteAllText(targetPath, BuildFile(managedBlock, localBlock), Encoding.UTF8);
        return new CoordinatorAgentsBootstrapResult(targetPath, CoordinatorAgentsBootstrapAction.Updated);
    }

    private static string LoadManagedBody(string? altaHelpText)
    {
        var contentPath = Path.Combine(AppContext.BaseDirectory, "content", "coordinator", "AGENTS.md");
        string body;
        if (File.Exists(contentPath))
        {
            body = File.ReadAllText(contentPath, Encoding.UTF8).Trim();
        }
        else
        {
            body = "# CodeAlta Global Coordinator\n\nUse the `alta` live tool for finite CodeAlta host/session/catalog operations.\n\n## `alta --help`\n\n```text\n{{ALTA_HELP}}\n```";
        }

        return body.Replace(AltaHelpPlaceholder, NormalizeAltaHelp(altaHelpText), StringComparison.Ordinal);
    }

    private static string NormalizeAltaHelp(string? altaHelpText)
    {
        var help = string.IsNullOrWhiteSpace(altaHelpText) ? FallbackAltaHelp : altaHelpText.Trim();
        return help.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').TrimEnd('\n');
    }

    private static string? ExtractManagedAltaHelp(string managedBlock)
    {
        const string header = "## Generated `alta --help`";
        const string fence = "```text";
        var headerIndex = managedBlock.IndexOf(header, StringComparison.Ordinal);
        if (headerIndex < 0)
        {
            return null;
        }

        var fenceIndex = managedBlock.IndexOf(fence, headerIndex, StringComparison.Ordinal);
        if (fenceIndex < 0)
        {
            return null;
        }

        var contentStart = managedBlock.IndexOf('\n', fenceIndex + fence.Length);
        if (contentStart < 0)
        {
            return null;
        }

        contentStart++;
        var fenceEnd = managedBlock.IndexOf("\n```", contentStart, StringComparison.Ordinal);
        if (fenceEnd < 0)
        {
            return null;
        }

        var help = managedBlock[contentStart..fenceEnd].Trim();
        return string.IsNullOrWhiteSpace(help) ? null : help;
    }

    private static string BuildManagedBlock(string managedBody)
    {
        var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(managedBody))).ToLowerInvariant();
        return $"{ManagedBeginPrefix} version=\"{TemplateVersion}\" checksum=\"{checksum}\" -->\n{managedBody}\n{ManagedEndMarker}";
    }

    private static string BuildLocalBlock(string localContent)
    {
        var content = string.IsNullOrEmpty(localContent) ? string.Empty : localContent.TrimEnd('\r', '\n');
        return $"{LocalBeginMarker}\n{content}\n{LocalEndMarker}";
    }

    private static string BuildFile(string managedBlock, string localBlock)
        => managedBlock + "\n\n" + localBlock + "\n";

    private static MarkerInfo FindMarkers(string content)
    {
        var managedBegin = FindAll(content, ManagedBeginPrefix);
        var managedEnd = FindAll(content, ManagedEndMarker);
        var localBegin = FindAll(content, LocalBeginMarker);
        var localEnd = FindAll(content, LocalEndMarker);
        return new MarkerInfo(managedBegin, managedEnd, localBegin, localEnd);
    }

    private static IReadOnlyList<int> FindAll(string content, string marker)
    {
        var indexes = new List<int>();
        var start = 0;
        while (start < content.Length)
        {
            var index = content.IndexOf(marker, start, StringComparison.Ordinal);
            if (index < 0)
            {
                break;
            }

            indexes.Add(index);
            start = index + marker.Length;
        }

        return indexes;
    }

    private static string CreateBackup(string targetPath)
    {
        var backupPath = targetPath + "." + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + ".bak";
        File.Copy(targetPath, backupPath, overwrite: false);
        return backupPath;
    }

    private static void LogWarning(string message)
    {
        Logger.Warn(message);
    }

    private sealed record MarkerInfo(
        IReadOnlyList<int> ManagedBeginIndexes,
        IReadOnlyList<int> ManagedEndIndexes,
        IReadOnlyList<int> LocalBeginIndexes,
        IReadOnlyList<int> LocalEndIndexes)
    {
        public bool HasNoMarkers => ManagedBeginIndexes.Count == 0 && ManagedEndIndexes.Count == 0 && LocalBeginIndexes.Count == 0 && LocalEndIndexes.Count == 0;

        public int ManagedBeginIndex => ManagedBeginIndexes[0];

        public int ManagedEndIndex => ManagedEndIndexes[0];

        public int LocalBeginIndex => LocalBeginIndexes[0];

        public int LocalEndIndex => LocalEndIndexes[0];

        public bool IsValid =>
            ManagedBeginIndexes.Count == 1 &&
            ManagedEndIndexes.Count == 1 &&
            LocalBeginIndexes.Count == 1 &&
            LocalEndIndexes.Count == 1 &&
            ManagedBeginIndex < ManagedEndIndex &&
            ManagedEndIndex < LocalBeginIndex &&
            LocalBeginIndex < LocalEndIndex;
    }
}

/// <summary>
/// Describes the result of bootstrapping global coordinator instructions.
/// </summary>
/// <param name="Path">The coordinator instruction file path.</param>
/// <param name="Action">The action performed.</param>
/// <param name="BackupPath">The backup path when an unmarked file was migrated.</param>
/// <param name="Diagnostics">Diagnostics emitted during bootstrap.</param>
public sealed record CoordinatorAgentsBootstrapResult(
    string Path,
    CoordinatorAgentsBootstrapAction Action,
    string? BackupPath = null,
    IReadOnlyList<string>? Diagnostics = null);

/// <summary>
/// Classifies coordinator instruction bootstrap actions.
/// </summary>
public enum CoordinatorAgentsBootstrapAction
{
    /// <summary>No file changes were required or possible.</summary>
    Unchanged,

    /// <summary>A new coordinator instruction file was created.</summary>
    Created,

    /// <summary>The managed block was refreshed while preserving local instructions.</summary>
    Updated,

    /// <summary>An unmarked existing file was backed up and migrated into the local section.</summary>
    Migrated,
}
