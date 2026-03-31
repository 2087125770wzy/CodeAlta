using CodeAlta.Frontend.Commands;
using CodeAlta.Frontend.Help;

namespace CodeAlta.Tests;

[TestClass]
public sealed class ShellCommandHelpTests
{
    [TestMethod]
    public void BuildSections_UsesShellCommandCatalogMetadata()
    {
        var helpCommand = ShellCommandCatalog.Get("CodeAlta.Shell.Help");
        var paletteCommand = ShellCommandCatalog.Get("CodeAlta.Shell.CommandPalette");
        var fullPromptCommand = ShellCommandCatalog.Get("CodeAlta.Thread.ExpandPrompt");

        var sections = ShellHelpContentBuilder.BuildSections();
        var entry = sections
            .SelectMany(static section => section.Entries)
            .Single(candidate => string.Equals(candidate.Label, helpCommand.Label, StringComparison.Ordinal));
        var paletteEntry = sections
            .SelectMany(static section => section.Entries)
            .Single(candidate => string.Equals(candidate.Label, paletteCommand.Label, StringComparison.Ordinal));
        var fullPromptEntry = sections
            .SelectMany(static section => section.Entries)
            .Single(candidate => string.Equals(candidate.Label, fullPromptCommand.Label, StringComparison.Ordinal));

        CollectionAssert.Contains(entry.Bindings.ToArray(), "/help");
        CollectionAssert.Contains(entry.Bindings.ToArray(), "?");
        CollectionAssert.Contains(paletteEntry.Bindings.ToArray(), "/");
        CollectionAssert.Contains(fullPromptEntry.Bindings.ToArray(), "/full_prompt");
    }

    [TestMethod]
    public void BuildSections_FilterMatchesAliases()
    {
        var sections = ShellHelpContentBuilder.BuildSections("compact");
        var entries = sections.SelectMany(static section => section.Entries).ToArray();

        Assert.AreEqual(1, entries.Length);
        Assert.AreEqual("Compact", entries[0].Label);
    }

    [TestMethod]
    public void ShellCommandMetadata_UsesCommandStyleIdentifiers()
    {
        var fullPromptCommand = ShellCommandCatalog.Get("CodeAlta.Thread.ExpandPrompt");
        var closeTabCommand = ShellCommandCatalog.Get("CodeAlta.Thread.CloseTab");

        Assert.AreEqual("full_prompt", fullPromptCommand.CommandName);
        CollectionAssert.Contains(fullPromptCommand.Aliases.ToArray(), "full_prompt");
        Assert.AreEqual("close_tab", closeTabCommand.CommandName);
        CollectionAssert.Contains(closeTabCommand.Aliases.ToArray(), "close_tab");
        CollectionAssert.Contains(closeTabCommand.Aliases.ToArray(), "close");
    }
}
