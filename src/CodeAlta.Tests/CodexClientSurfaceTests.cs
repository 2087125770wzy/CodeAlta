using System.Text.RegularExpressions;

namespace CodeAlta.Tests;

[TestClass]
public sealed class CodexClientSurfaceTests
{
    [TestMethod]
    public void CodexClient_ExposesAllGeneratedRequestMethodsExceptKnownGeneratorGaps()
    {
        var repoRoot = FindRepoRoot();
        var generatedRequestPath = Path.Combine(repoRoot, "src", "CodeAlta.CodexSdk", "generated", "ClientRequest.gen.cs");
        var clientPath = Path.Combine(repoRoot, "src", "CodeAlta.CodexSdk", "CodexClient.cs");

        var generatedMethods = File.ReadLines(generatedRequestPath)
            .Select(static line => Regex.Match(line, "typeDiscriminator: \"([^\"]+)\""))
            .Where(static match => match.Success)
            .Select(static match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        var surfacedMethods = File.ReadLines(clientPath)
            .Select(static line => line.TrimStart())
            .Where(static line => line.StartsWith("\"", StringComparison.Ordinal))
            .Select(static line => Regex.Match(line, "^\"([^\"]+)\""))
            .Where(static match => match.Success)
            .Select(static match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        var excludedMethods = new HashSet<string>(StringComparer.Ordinal)
        {
            "initialize",
            "fuzzyFileSearch"
        };

        var missingMethods = generatedMethods
            .Except(surfacedMethods)
            .Except(excludedMethods)
            .OrderBy(static method => method, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            missingMethods,
            $"Missing CodexClient wrappers for: {string.Join(", ", missingMethods)}");
    }

    private static string FindRepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "CodeAlta.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }
}
