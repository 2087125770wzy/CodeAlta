using CodeAlta.App;
using CodeAlta.Catalog;
using CodeAlta.Catalog.Skills;

namespace CodeAlta.Tests;

[TestClass]
public sealed class SkillsManagementServiceTests
{
    [TestMethod]
    public void ListRelatedFiles_ReturnsAuthoringFilesUnderKnownFolders()
    {
        using var temp = TempDirectory.Create();
        var skillRoot = Path.Combine(temp.Path, "sample-skill");
        Directory.CreateDirectory(skillRoot);
        Directory.CreateDirectory(Path.Combine(skillRoot, "scripts"));
        Directory.CreateDirectory(Path.Combine(skillRoot, "references", "deep"));
        Directory.CreateDirectory(Path.Combine(skillRoot, "assets"));
        File.WriteAllText(Path.Combine(skillRoot, "SKILL.md"), "---\nname: sample-skill\ndescription: sample\n---\n");
        File.WriteAllText(Path.Combine(skillRoot, "README.md"), "ignored");
        File.WriteAllText(Path.Combine(skillRoot, "scripts", "run.ps1"), "Write-Host test");
        File.WriteAllText(Path.Combine(skillRoot, "references", "deep", "guide.md"), "# Guide");
        File.WriteAllText(Path.Combine(skillRoot, "assets", "icon.svg"), "<svg />");

        var files = SkillsManagementService.ListRelatedFiles(CreateDescriptor(skillRoot));

        CollectionAssert.AreEqual(
            new[]
            {
                "scripts/run.ps1",
                "references/deep/guide.md",
                "assets/icon.svg",
            },
            files.Select(static file => file.RelativePath).ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                "scripts",
                "references",
                "assets",
            },
            files.Select(static file => file.Category).ToArray());
        Assert.IsTrue(files.All(static file => File.Exists(file.FullPath)));
    }

    [TestMethod]
    public void ListRelatedFiles_ReturnsEmptyForMissingSkillRoot()
    {
        var descriptor = CreateDescriptor(Path.Combine(Path.GetTempPath(), $"missing-skill-{Guid.NewGuid():N}"));

        var files = SkillsManagementService.ListRelatedFiles(descriptor);

        Assert.AreEqual(0, files.Count);
    }

    [TestMethod]
    public async Task CreateSkillAsync_CreatesProjectAltaSkillForCombinedScope()
    {
        using var temp = TempDirectory.Create();
        var projectRoot = Path.Combine(temp.Path, "project");
        var globalRoot = Path.Combine(temp.Path, ".alta");
        Directory.CreateDirectory(projectRoot);
        var service = new SkillsManagementService(
            new SkillCatalog(),
            new CatalogOptions { GlobalRoot = globalRoot },
            () => new ProjectDescriptor { ProjectPath = projectRoot });

        var result = await service.CreateSkillAsync(
            SkillsManagementScope.Combined,
            "sample-skill",
            "Use this skill for tests.");

        Assert.AreEqual(SkillCreationTargetKind.ProjectCodeAlta, result.TargetKind);
        Assert.AreEqual(Path.Combine(projectRoot, ".alta", "skills", "sample-skill"), result.SkillRootPath);
        Assert.IsTrue(File.Exists(result.SkillFilePath));
        Assert.IsTrue(Directory.Exists(Path.Combine(result.SkillRootPath, "scripts")));
        Assert.IsTrue(Directory.Exists(Path.Combine(result.SkillRootPath, "references")));
        Assert.IsTrue(Directory.Exists(Path.Combine(result.SkillRootPath, "assets")));
        var contents = await File.ReadAllTextAsync(result.SkillFilePath);
        StringAssert.Contains(contents, "name: sample-skill");
        StringAssert.Contains(contents, "description: 'Use this skill for tests.'");
    }

    [TestMethod]
    public async Task CreateSkillAsync_CreatesUserAltaSkillWhenNoProjectIsSelected()
    {
        using var temp = TempDirectory.Create();
        var service = new SkillsManagementService(
            new SkillCatalog(),
            new CatalogOptions { GlobalRoot = temp.Path },
            () => null);

        var result = await service.CreateSkillAsync(
            SkillsManagementScope.Combined,
            "user-skill",
            "Use this skill globally.");

        Assert.AreEqual(SkillCreationTargetKind.UserCodeAlta, result.TargetKind);
        Assert.AreEqual(Path.Combine(temp.Path, "skills", "user-skill"), result.SkillRootPath);
        Assert.IsTrue(File.Exists(result.SkillFilePath));
    }

    [TestMethod]
    [DataRow("BadName")]
    [DataRow("-bad")]
    [DataRow("bad-")]
    [DataRow("bad--name")]
    [DataRow("bad_name")]
    public async Task CreateSkillAsync_RejectsInvalidSkillNames(string name)
    {
        using var temp = TempDirectory.Create();
        var service = new SkillsManagementService(
            new SkillCatalog(),
            new CatalogOptions { GlobalRoot = temp.Path },
            () => null);

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => service.CreateSkillAsync(SkillsManagementScope.User, name, "Description.")).ConfigureAwait(false);
    }

    private static SkillDescriptor CreateDescriptor(string skillRoot)
        => new()
        {
            Name = "sample-skill",
            NormalizedName = "sample-skill",
            Title = "sample-skill",
            Description = "Sample skill.",
            SkillRootPath = skillRoot,
            SkillFilePath = Path.Combine(skillRoot, "SKILL.md"),
            SourceKind = SkillSourceKind.ProjectAlta,
            SourceId = "test",
            Scope = SkillScopeKind.Project,
            Precedence = 0,
            Frontmatter = new SkillFrontmatter(),
            IsTrusted = true,
            IsValid = true,
            IsModelVisible = true,
        };

    private sealed class TempDirectory(string path) : IDisposable
    {
        public string Path { get; } = path;

        public static TempDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"codealta-skills-ui-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TempDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
