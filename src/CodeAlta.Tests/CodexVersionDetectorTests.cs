extern alias CodexSdkGenerator;

using CodexVersionDetector = CodexSdkGenerator::CodeAlta.CodexSdk.Generator.CodexVersionDetector;

namespace CodeAlta.Tests;

[TestClass]
public sealed class CodexVersionDetectorTests
{
    [TestMethod]
    public void TryParseVersion_ParsesSimpleSemanticVersion()
    {
        var success = CodexVersionDetector.TryParseVersion("0.44.1", out var version);

        Assert.IsTrue(success);
        Assert.AreEqual(new Version(0, 44, 1), version);
    }

    [TestMethod]
    public void TryParseVersion_ParsesVersionWithinText()
    {
        var success = CodexVersionDetector.TryParseVersion(
            "codex-cli 1.2.3-beta.4",
            out var version);

        Assert.IsTrue(success);
        Assert.AreEqual(new Version(1, 2, 3), version);
    }

    [TestMethod]
    public void TryParseVersion_ParsesFourSegmentVersion()
    {
        var success = CodexVersionDetector.TryParseVersion(
            "codex version 2.10.5.17",
            out var version);

        Assert.IsTrue(success);
        Assert.AreEqual(new Version(2, 10, 5, 17), version);
    }

    [TestMethod]
    public void TryParseVersion_ReturnsFalse_WhenNoVersionFound()
    {
        var success = CodexVersionDetector.TryParseVersion(
            "codex version unknown",
            out var version);

        Assert.IsFalse(success);
        Assert.AreEqual(new Version(0, 0, 0), version);
    }
}
