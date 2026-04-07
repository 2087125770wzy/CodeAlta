using System.Diagnostics;

namespace CodeAlta.CodexSdk;

internal static class CodexProcessHelper
{
    internal static ProcessStartInfo CreateCommandProcessStartInfo(
        string executablePath,
        string arguments,
        bool redirectStandardInput = false,
        bool redirectStandardOutput = true,
        bool redirectStandardError = true,
        bool createNoWindow = false)
    {
        ArgumentNullException.ThrowIfNull(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);

        return new ProcessStartInfo(executablePath, arguments)
        {
            RedirectStandardInput = redirectStandardInput,
            RedirectStandardOutput = redirectStandardOutput,
            RedirectStandardError = redirectStandardError,
            UseShellExecute = false,
            CreateNoWindow = createNoWindow
        };
    }
}
