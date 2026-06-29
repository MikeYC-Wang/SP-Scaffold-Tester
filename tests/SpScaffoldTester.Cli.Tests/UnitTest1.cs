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
}
