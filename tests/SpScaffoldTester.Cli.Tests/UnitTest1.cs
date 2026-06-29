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

    [Fact]
    public void Program_WithVerifyAndMatchingFiles_ShouldReturnZero()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sp-verify-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var baselinePath = Path.Combine(tempDir, "baseline.json");
        var currentPath = Path.Combine(tempDir, "current.json");
        var snapshotJson = "{\"schemaVersion\":\"1.0\",\"storedProcedures\":[]}";
        File.WriteAllText(baselinePath, snapshotJson);
        File.WriteAllText(currentPath, snapshotJson);

        using var output = new StringWriter();

        try
        {
            var exitCode = CliCommandRunner.Run(["verify", "--baseline", baselinePath, "--current", currentPath], output);

            Assert.Equal(0, exitCode);
            Assert.Contains("No breaking contract changes", output.ToString());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Program_WithVerifyAndDifferentFiles_ShouldReturnTwo()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sp-verify-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var baselinePath = Path.Combine(tempDir, "baseline.json");
        var currentPath = Path.Combine(tempDir, "current.json");
        File.WriteAllText(baselinePath, "{\"schemaVersion\":\"1.0\",\"storedProcedures\":[]}");
        File.WriteAllText(currentPath, "{\"schemaVersion\":\"1.0\",\"storedProcedures\":[{\"name\":\"usp_demo\"}]}");

        using var output = new StringWriter();

        try
        {
            var exitCode = CliCommandRunner.Run(["verify", "--baseline", baselinePath, "--current", currentPath], output);

            Assert.Equal(2, exitCode);
            Assert.Contains("Breaking contract changes detected", output.ToString());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Program_WithVerifyAndMissingFile_ShouldReturnFour()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sp-verify-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var baselinePath = Path.Combine(tempDir, "baseline.json");
        var currentPath = Path.Combine(tempDir, "current.json");
        File.WriteAllText(currentPath, "{\"schemaVersion\":\"1.0\",\"storedProcedures\":[]}");

        using var output = new StringWriter();

        try
        {
            var exitCode = CliCommandRunner.Run(["verify", "--baseline", baselinePath, "--current", currentPath], output);

            Assert.Equal(4, exitCode);
            Assert.Contains("Configuration error", output.ToString());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Program_WithVerifyAndOnlyOptionalParameterAdded_ShouldReturnZero()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sp-verify-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var baselinePath = Path.Combine(tempDir, "baseline.json");
        var currentPath = Path.Combine(tempDir, "current.json");

        File.WriteAllText(
            baselinePath,
            "{\"schemaVersion\":\"1.0\",\"storedProcedures\":[{\"name\":\"usp_demo\",\"parameters\":[],\"resultColumns\":[]}]}"
        );
        File.WriteAllText(
            currentPath,
            "{\"schemaVersion\":\"1.0\",\"storedProcedures\":[{\"name\":\"usp_demo\",\"parameters\":[{\"name\":\"traceId\",\"dbType\":\"nvarchar\",\"isOptional\":true}],\"resultColumns\":[]}]}"
        );

        using var output = new StringWriter();

        try
        {
            var exitCode = CliCommandRunner.Run(["verify", "--baseline", baselinePath, "--current", currentPath], output);

            Assert.Equal(0, exitCode);
            Assert.Contains("No breaking contract changes", output.ToString());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Program_WithVerifyAndReportPath_ShouldWriteJsonReport()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sp-verify-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var baselinePath = Path.Combine(tempDir, "baseline.json");
        var currentPath = Path.Combine(tempDir, "current.json");
        var reportPath = Path.Combine(tempDir, "report.json");

        File.WriteAllText(
            baselinePath,
            "{\"schemaVersion\":\"1.0\",\"storedProcedures\":[{\"name\":\"usp_demo\",\"parameters\":[{\"name\":\"id\",\"dbType\":\"int\",\"isOptional\":false}],\"resultColumns\":[]}]}"
        );
        File.WriteAllText(
            currentPath,
            "{\"schemaVersion\":\"1.0\",\"storedProcedures\":[{\"name\":\"usp_demo\",\"parameters\":[],\"resultColumns\":[]}]}"
        );

        using var output = new StringWriter();

        try
        {
            var exitCode = CliCommandRunner.Run(["verify", "--baseline", baselinePath, "--current", currentPath, "--report", reportPath], output);

            Assert.Equal(2, exitCode);
            Assert.True(File.Exists(reportPath));

            var reportJson = File.ReadAllText(reportPath);
            Assert.Contains("\"severity\":\"Breaking\"", reportJson);
            Assert.Contains("\"reasons\":[", reportJson);
            Assert.Contains("\"items\":[", reportJson);
            Assert.Contains("\"code\":\"ParameterRemoved\"", reportJson);
            Assert.Contains("\"storedProcedure\":\"usp_demo\"", reportJson);
            Assert.Contains("\"memberName\":\"id\"", reportJson);
            Assert.Contains("Parameter removed", reportJson);
            Assert.Contains("Verify report written to", output.ToString());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
