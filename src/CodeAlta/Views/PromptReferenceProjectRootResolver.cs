using CodeAlta.Catalog;

namespace CodeAlta.Views;

internal static class PromptReferenceProjectRootResolver
{
    public static string? Resolve(
        WorkThreadDescriptor? selectedThread,
        Func<string?, ProjectDescriptor?> getProjectById,
        Func<ProjectDescriptor?> getSelectedProject,
        string? globalRoot = null)
    {
        ArgumentNullException.ThrowIfNull(getProjectById);
        ArgumentNullException.ThrowIfNull(getSelectedProject);

        if (selectedThread is null)
        {
            return getSelectedProject()?.ProjectPath ?? NormalizeOptionalRoot(globalRoot);
        }

        if (selectedThread.Kind == WorkThreadKind.GlobalThread)
        {
            return NormalizeOptionalRoot(selectedThread.WorkingDirectory) ?? NormalizeOptionalRoot(globalRoot);
        }

        return getProjectById(selectedThread.ProjectRef)?.ProjectPath ?? selectedThread.WorkingDirectory;
    }

    private static string? NormalizeOptionalRoot(string? path)
        => string.IsNullOrWhiteSpace(path) ? null : path.Trim();
}
