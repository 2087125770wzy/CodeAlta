using XenoAtom.Logging;

namespace CodeAlta.Tests;

[TestClass]
public sealed class CodeAltaTestLoggingAssemblyHooks
{
    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        CodeAltaTestLogging.InitializeFallback();
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
        LogManager.Shutdown();
    }
}

internal static class CodeAltaTestLogging
{
    public static void InitializeFallback()
    {
        if (LogManager.IsInitialized)
        {
            return;
        }

        try
        {
            LogManager.InitializeForAsync(new LogManagerConfig());
        }
        catch (InvalidOperationException) when (LogManager.IsInitialized)
        {
        }
    }
}
