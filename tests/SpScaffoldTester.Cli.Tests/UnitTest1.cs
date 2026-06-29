using System.IO;

namespace SpScaffoldTester.Cli.Tests;

public class ScanCommandRunnerTests
{
    [Fact]
    public void Run_WithScanArgument_ShouldReturnZeroAndWriteJson()
    {
        using var output = new StringWriter();

        var exitCode = ScanCommandRunner.Run(["scan"], output);
        var text = output.ToString();

        Assert.Equal(0, exitCode);
        Assert.Contains("\"command\":\"scan\"", text);
        Assert.Contains("\"status\":\"stub\"", text);
    }

    [Fact]
    public void Run_WithoutSupportedArgument_ShouldReturnOne()
    {
        using var output = new StringWriter();

        var exitCode = ScanCommandRunner.Run([], output);

        Assert.Equal(1, exitCode);
        Assert.Contains("Usage: sp-scaffold-tester scan", output.ToString());
    }

    [Fact]
    public void Run_WithScanAndOutputPath_ShouldWriteSnapshotFile()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sp-scan-{Guid.NewGuid():N}.json");
        using var output = new StringWriter();

        try
        {
            var exitCode = ScanCommandRunner.Run(["scan", "--out", tempFile], output);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(tempFile));

            var text = File.ReadAllText(tempFile);
            Assert.Contains("\"schemaVersion\":\"1.0\"", text);
            Assert.Contains("\"storedProcedures\":[]", text.Replace("\n", string.Empty).Replace("\r", string.Empty).Replace(" ", string.Empty));
            Assert.Contains("Snapshot written to", output.ToString());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void Run_WithOutOptionMissingPath_ShouldReturnOne()
    {
        using var output = new StringWriter();

        var exitCode = ScanCommandRunner.Run(["scan", "--out"], output);

        Assert.Equal(1, exitCode);
        Assert.Contains("Usage: sp-scaffold-tester scan", output.ToString());
    }
}
