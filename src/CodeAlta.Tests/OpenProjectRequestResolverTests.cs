using CodeAlta.App;
using CodeAlta.Catalog;

namespace CodeAlta.Tests;

[TestClass]
public sealed class OpenProjectRequestResolverTests
{
    [TestMethod]
    public void LooksLikePath_TreatsHomeAndRootedInputsAsPaths()
    {
        Assert.IsTrue(OpenProjectRequestResolver.LooksLikePath("~"));
        Assert.IsTrue(OpenProjectRequestResolver.LooksLikePath("~/repo"));
        Assert.IsTrue(OpenProjectRequestResolver.LooksLikePath(Path.GetPathRoot(Environment.CurrentDirectory)!));
        Assert.IsFalse(OpenProjectRequestResolver.LooksLikePath("CodeAlta"));
    }

    [TestMethod]
    public void ResolveProjectReference_UsesSlugBeforeDisplayName()
    {
        var slugProject = CreateProject("project-1", "codealta", "Project One");
        var displayNameProject = CreateProject("project-2", "project-two", "codealta");

        var result = OpenProjectRequestResolver.ResolveProjectReference(
            [slugProject, displayNameProject],
            "codealta");

        Assert.AreSame(slugProject, result);
    }

    [TestMethod]
    public void ResolveProjectReference_ThrowsForAmbiguousDisplayName()
    {
        var projects = new[]
        {
            CreateProject("project-1", "codealta-a", "CodeAlta"),
            CreateProject("project-2", "codealta-b", "CodeAlta"),
        };

        var ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => OpenProjectRequestResolver.ResolveProjectReference(projects, "CodeAlta"));

        StringAssert.Contains(ex.Message, "matched multiple entries");
    }

    private static ProjectDescriptor CreateProject(string id, string slug, string displayName)
    {
        return new ProjectDescriptor
        {
            Id = id,
            Slug = slug,
            Name = displayName,
            DisplayName = displayName,
            ProjectPath = Path.Combine(Path.GetTempPath(), slug),
            DefaultBranch = "main",
        };
    }
}
