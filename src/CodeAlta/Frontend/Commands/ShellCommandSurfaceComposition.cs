using CodeAlta.App;
using CodeAlta.ViewModels;

namespace CodeAlta.Frontend.Commands;

internal static class ShellCommandSurfaceComposition
{
    public static ShellCommandSurfaceCoordinator Create(
        PromptComposerViewModel promptComposerViewModel,
        ThreadWorkspaceViewModel threadWorkspaceViewModel,
        ThreadCommandCoordinator threadCommandCoordinator,
        IShellPromptInputService promptInputService,
        IShellThreadCommandService threadCommandService,
        IShellDialogCommandService dialogCommandService,
        IShellNavigationCommandService navigationCommandService,
        IShellTabCommandService tabCommandService,
        IShellStatusService statusService,
        IPluginCommandService pluginCommandService,
        Action toggleCommandBarMultiLine)
    {
        ArgumentNullException.ThrowIfNull(promptComposerViewModel);
        ArgumentNullException.ThrowIfNull(threadWorkspaceViewModel);
        ArgumentNullException.ThrowIfNull(threadCommandCoordinator);
        ArgumentNullException.ThrowIfNull(promptInputService);
        ArgumentNullException.ThrowIfNull(threadCommandService);
        ArgumentNullException.ThrowIfNull(dialogCommandService);
        ArgumentNullException.ThrowIfNull(navigationCommandService);
        ArgumentNullException.ThrowIfNull(tabCommandService);
        ArgumentNullException.ThrowIfNull(statusService);
        ArgumentNullException.ThrowIfNull(pluginCommandService);
        ArgumentNullException.ThrowIfNull(toggleCommandBarMultiLine);

        var commandPalettePresenter = new ShellCommandPalettePresenter(dialogCommandService);
        var shellCommandRegistry = new ShellCommandRegistryFactory(
            threadCommandCoordinator,
            dialogCommandService,
            navigationCommandService,
            tabCommandService,
            statusService,
            pluginCommandService).Create(commandPalettePresenter);
        var shellCommandDispatcher = new ShellCommandDispatcher(shellCommandRegistry);
        var shellCommandBindingProjector = new ShellCommandBindingProjector(
            promptComposerViewModel,
            threadWorkspaceViewModel,
            threadCommandService,
            statusService,
            shellCommandRegistry,
            shellCommandDispatcher,
            pluginCommandService);
        return new ShellCommandSurfaceCoordinator(
            promptInputService,
            shellCommandDispatcher,
            shellCommandBindingProjector,
            commandPalettePresenter,
            toggleCommandBarMultiLine);
    }
}
