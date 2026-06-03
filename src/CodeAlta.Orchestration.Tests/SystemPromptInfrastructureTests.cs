using CodeAlta.Catalog;
using CodeAlta.Orchestration.Runtime.SystemPrompts;

namespace CodeAlta.Orchestration.Tests;

[TestClass]
public sealed class SystemPromptInfrastructureTests
{
    [TestMethod]
    public void AgentPromptCatalog_ListsBuiltInGlobalProjectAndMarksOverrides()
    {
        using var temp = TempDirectory.Create();
        var appBase = Path.Combine(temp.Path, "app");
        var globalRoot = Path.Combine(temp.Path, "global");
        var projectRoot = Path.Combine(temp.Path, "project");
        WritePrompt(appBase, "default", "Default Built-in", "default", "Built-in body.");
        WritePrompt(globalRoot, "default", "Default Global", "default", "Global body.");
        WritePrompt(globalRoot, "global-extra", "Global Extra", "default", "Global extra body.");
        WritePrompt(projectRoot, "default", "Default Project", "project-system", "Project body.");

        var catalog = new AgentPromptCatalog(new FileSystemPromptContentLocator(appBase));
        var allPrompts = catalog.ListPrompts(new AgentPromptCatalogQuery
        {
            UserCodeAltaRoot = globalRoot,
            ProjectRoot = projectRoot,
            ProjectPromptResourcesTrusted = true,
        });

        CollectionAssert.AreEqual(
            new[]
            {
                AgentPromptSourceKind.BuiltIn,
                AgentPromptSourceKind.UserGlobal,
                AgentPromptSourceKind.UserGlobal,
                AgentPromptSourceKind.Project,
            },
            allPrompts.Select(static prompt => prompt.SourceKind).ToArray());
        Assert.IsTrue(allPrompts.Single(prompt => prompt.SourceKind == AgentPromptSourceKind.BuiltIn).IsShadowed);
        Assert.IsTrue(allPrompts.Single(prompt => prompt.SourceKind == AgentPromptSourceKind.UserGlobal && prompt.PromptName == "default").IsShadowed);

        var effectiveDefault = catalog.ResolvePrompt(
            new AgentPromptCatalogQuery
            {
                UserCodeAltaRoot = globalRoot,
                ProjectRoot = projectRoot,
                ProjectPromptResourcesTrusted = true,
            },
            "default");

        Assert.IsNotNull(effectiveDefault);
        Assert.AreEqual(AgentPromptSourceKind.Project, effectiveDefault.SourceKind);
        Assert.AreEqual("Default Project", effectiveDefault.DisplayName);
        Assert.AreEqual("project-system", effectiveDefault.SystemPromptName);
    }

    [TestMethod]
    public void SystemPromptBuilder_UsesSelectedAgentPromptBodyAndSystemProperty()
    {
        using var temp = TempDirectory.Create();
        var appBase = Path.Combine(temp.Path, "app");
        var globalRoot = Path.Combine(temp.Path, "global");
        var projectRoot = Path.Combine(temp.Path, "project");
        Directory.CreateDirectory(projectRoot);
        WriteSystem(appBase, "default", "Built-in default system.");
        WritePrompt(appBase, "default", "Default", "default", "Built-in default prompt.");
        WriteSystem(projectRoot, "custom-system", "Project custom system.");
        WritePrompt(projectRoot, "review", "Review", "custom-system", "Review the current change set.");
        WriteTemplate(projectRoot, "default", "review");

        var builder = new SystemPromptBuilder(new FileSystemPromptContentLocator(appBase));
        var bundle = builder.Build(new SystemPromptBuildRequest
        {
            ProviderKey = "codex",
            ProviderType = "codex",
            ProtocolFamily = "codex",
            Session = new SessionViewDescriptor
            {
                SessionId = "session-1",
                ProviderId = "codex",
                ProviderKey = "codex",
                WorkingDirectory = projectRoot,
                Kind = SessionViewKind.ProjectSession,
            },
            Project = new ProjectDescriptor
            {
                Id = "project-1",
                Slug = "project-1",
                DisplayName = "Project 1",
                ProjectPath = projectRoot,
            },
            UserCodeAltaRoot = globalRoot,
            PartOptionsOverride = new PartialSystemPromptPartOptions(
                Skills: false,
                ProjectContext: false,
                RuntimeContext: false,
                ToolGuidance: false),
        });

        Assert.AreEqual("Project custom system.", bundle.SystemMessage);
        StringAssert.Contains(bundle.DeveloperInstructions!, "# Agent Prompt");
        StringAssert.Contains(bundle.DeveloperInstructions!, "Review the current change set.");
        Assert.AreEqual("custom-system", bundle.Manifest.Template.BaseName);
        Assert.AreEqual("review", bundle.Manifest.Template.InstructionName);
        Assert.IsTrue(bundle.Manifest.Parts.Any(static part => part.Key == "agents/review" && part.Status == "selected"));
    }

    [TestMethod]
    public void SystemPromptBuilder_ListsEffectiveAgentPromptsWithoutSensitiveMetadata()
    {
        using var temp = TempDirectory.Create();
        var appBase = Path.Combine(temp.Path, "app");
        var globalRoot = Path.Combine(temp.Path, "global");
        var projectRoot = Path.Combine(temp.Path, "project");
        Directory.CreateDirectory(projectRoot);
        WriteSystem(appBase, "default", "Built-in default system.");
        WritePrompt(appBase, "default", "Default Built-in", "default", "Built-in default body.", "Built-in default description.");
        WritePrompt(globalRoot, "default", "Default Global", "default", "Global default body.", "Global default description.");
        WritePrompt(globalRoot, "global-extra", "Global Extra", "default", "Global extra body.", "Global extra description.");
        WriteSystem(projectRoot, "project-system", "Project system.");
        WritePrompt(projectRoot, "default", "Project Default", "project-system", "Project default body.", "Project default description.");

        var builder = new SystemPromptBuilder(new FileSystemPromptContentLocator(appBase));
        var bundle = builder.Build(new SystemPromptBuildRequest
        {
            ProviderKey = "codex",
            ProviderType = "codex",
            ProtocolFamily = "codex",
            Session = new SessionViewDescriptor
            {
                SessionId = "session-1",
                ProviderId = "codex",
                ProviderKey = "codex",
                WorkingDirectory = projectRoot,
                Kind = SessionViewKind.ProjectSession,
            },
            Project = new ProjectDescriptor
            {
                Id = "project-1",
                Slug = "project-1",
                DisplayName = "Project 1",
                ProjectPath = projectRoot,
            },
            UserCodeAltaRoot = globalRoot,
            SelectedPromptName = "global-extra",
            PartOptionsOverride = new PartialSystemPromptPartOptions(
                Skills: false,
                ProjectContext: false,
                RuntimeContext: false,
                ToolGuidance: true),
        });

        var developerInstructions = bundle.DeveloperInstructions!;
        var newline = Environment.NewLine;
        StringAssert.Contains(developerInstructions, "# Agent Prompts");
        StringAssert.Contains(developerInstructions, "Agent prompt profiles available for this session:");
        StringAssert.Contains(developerInstructions, $"- current: `global-extra` — Global Extra{newline}  - Source: user-global; system: `default`{newline}  - Description: Global extra description.");
        StringAssert.Contains(developerInstructions, $"- `default` — Project Default{newline}  - Source: project; system: `project-system`{newline}  - Description: Project default description.");
        StringAssert.Contains(developerInstructions, "alta session set_agent --prompt-id <id>");
        StringAssert.Contains(developerInstructions, "alta session send <session-id> --prompt-id <id> --stdin");
        Assert.IsFalse(developerInstructions.Contains("Default Built-in", StringComparison.Ordinal));
        Assert.IsFalse(developerInstructions.Contains("Default Global", StringComparison.Ordinal));
        Assert.IsFalse(developerInstructions.Contains("Project default body.", StringComparison.Ordinal));
        Assert.IsFalse(developerInstructions.Contains(projectRoot, StringComparison.Ordinal));
        Assert.IsFalse(developerInstructions.Contains("sha256:", StringComparison.Ordinal));
        Assert.IsTrue(bundle.Manifest.Parts.Any(static part => part.Key == "prompt.discovery" && part.Kind == "agent_prompts" && part.Status == "selected"));
    }

    private static void WriteSystem(string root, string id, string body)
    {
        var directory = root.EndsWith("project", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(root, ".alta", "prompts", "system")
            : Path.Combine(root, "content", "prompts", "system");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, id + ".system-prompt.md"), $"""
            ---
            description: Test system prompt.
            ---
            {body}
            """);
    }

    private static void WritePrompt(string root, string id, string name, string system, string body, string description = "Test agent prompt.")
    {
        var directory = root.EndsWith("app", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(root, "content", "prompts", "agents")
            : root.EndsWith("project", StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(root, ".alta", "prompts", "agents")
                : Path.Combine(root, "prompts", "agents");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, id + ".prompt.md"), $"""
            ---
            name: {name}
            system: {system}
            description: {description}
            ---
            {body}
            """);
    }

    private static void WriteTemplate(string root, string system, string agent)
    {
        var directory = Path.Combine(root, ".alta", "prompts");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "template.yml"), $"""
            version: 1
            system: {system}
            agent: {agent}
            """);
    }

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "codealta-orchestration-tests-" + Guid.NewGuid().ToString("N"));
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
