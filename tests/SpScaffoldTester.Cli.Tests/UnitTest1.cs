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
    public void Run_WithScanAndSqlPath_ShouldParseAndWriteSnapshotFile()
    {
        var tempSqlFile = Path.Combine(Path.GetTempPath(), $"sp-scan-sql-{Guid.NewGuid():N}.sql");
        var tempSnapshotFile = Path.Combine(Path.GetTempPath(), $"sp-scan-snapshot-{Guid.NewGuid():N}.json");

        File.WriteAllText(
            tempSqlFile,
            """
            CREATE PROCEDURE dbo.usp_GetUser
                @id INT,
                @traceId NVARCHAR(36) = NULL
            AS
            BEGIN
                SELECT 1;
            END
            """
        );

        using var output = new StringWriter();

        try
        {
            var exitCode = ScanCommandRunner.Run(["scan", "--sql", tempSqlFile, "--out", tempSnapshotFile], output);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(tempSnapshotFile));

            var snapshotText = File.ReadAllText(tempSnapshotFile);
            Assert.Contains("\"name\":\"dbo.usp_GetUser\"", snapshotText);
            Assert.Contains("\"name\":\"id\"", snapshotText);
            Assert.Contains("\"name\":\"traceId\"", snapshotText);

            var outputText = output.ToString();
            Assert.Contains("\"status\":\"scanned\"", outputText);
            Assert.Contains("Snapshot written to", outputText);
        }
        finally
        {
            if (File.Exists(tempSqlFile))
            {
                File.Delete(tempSqlFile);
            }

            if (File.Exists(tempSnapshotFile))
            {
                File.Delete(tempSnapshotFile);
            }
        }
    }

    [Fact]
    public void Run_WithScanAndSqlPath_ShouldIncludeResultColumnsInSnapshot()
    {
        var tempSqlFile = Path.Combine(Path.GetTempPath(), $"sp-scan-sql-{Guid.NewGuid():N}.sql");
        var tempSnapshotFile = Path.Combine(Path.GetTempPath(), $"sp-scan-snapshot-{Guid.NewGuid():N}.json");

        File.WriteAllText(
            tempSqlFile,
            """
            CREATE PROCEDURE dbo.usp_GetUser
                @id INT
            AS
            BEGIN
                SELECT
                    CAST(1 AS INT) AS userId,
                    CAST(NULL AS NVARCHAR(100)) AS nickName;
            END
            """
        );

        using var output = new StringWriter();

        try
        {
            var exitCode = ScanCommandRunner.Run(["scan", "--sql", tempSqlFile, "--out", tempSnapshotFile], output);

            Assert.Equal(0, exitCode);

            var snapshotText = File.ReadAllText(tempSnapshotFile);
            Assert.Contains("\"resultColumns\":[", snapshotText);
            Assert.Contains("\"name\":\"userId\"", snapshotText);
            Assert.Contains("\"dbType\":\"int\"", snapshotText);
            Assert.Contains("\"name\":\"nickName\"", snapshotText);
            Assert.Contains("\"dbType\":\"nvarchar\"", snapshotText);
        }
        finally
        {
            if (File.Exists(tempSqlFile))
            {
                File.Delete(tempSqlFile);
            }

            if (File.Exists(tempSnapshotFile))
            {
                File.Delete(tempSnapshotFile);
            }
        }
    }

    [Fact]
    public void Run_WithScanAndUnsupportedProjection_ShouldSetMetadataAmbiguousFlag()
    {
        var tempSqlFile = Path.Combine(Path.GetTempPath(), $"sp-scan-sql-{Guid.NewGuid():N}.sql");
        var tempSnapshotFile = Path.Combine(Path.GetTempPath(), $"sp-scan-snapshot-{Guid.NewGuid():N}.json");

        File.WriteAllText(
            tempSqlFile,
            """
            CREATE PROCEDURE dbo.usp_GetUser
                @id INT
            AS
            BEGIN
                SELECT u.Id AS userId
                FROM dbo.Users u;
            END
            """
        );

        using var output = new StringWriter();

        try
        {
            var exitCode = ScanCommandRunner.Run(["scan", "--sql", tempSqlFile, "--out", tempSnapshotFile], output);

            Assert.Equal(0, exitCode);

            var snapshotText = File.ReadAllText(tempSnapshotFile);
            Assert.Contains("\"isMetadataAmbiguous\":true", snapshotText);
        }
        finally
        {
            if (File.Exists(tempSqlFile))
            {
                File.Delete(tempSqlFile);
            }

            if (File.Exists(tempSnapshotFile))
            {
                File.Delete(tempSnapshotFile);
            }
        }
    }

    [Fact]
    public void Run_WithMultipleProcedures_ShouldWriteSnapshotInDeterministicOrder()
    {
        var tempSqlFile = Path.Combine(Path.GetTempPath(), $"sp-scan-sql-{Guid.NewGuid():N}.sql");
        var tempSnapshotFile = Path.Combine(Path.GetTempPath(), $"sp-scan-snapshot-{Guid.NewGuid():N}.json");

        File.WriteAllText(
            tempSqlFile,
            """
            CREATE PROCEDURE dbo.usp_Zeta
            AS
            BEGIN
                SELECT 1;
            END

            CREATE PROCEDURE dbo.usp_Alpha
            AS
            BEGIN
                SELECT 1;
            END
            """
        );

        using var output = new StringWriter();

        try
        {
            var exitCode = ScanCommandRunner.Run(["scan", "--sql", tempSqlFile, "--out", tempSnapshotFile], output);

            Assert.Equal(0, exitCode);
            var snapshotText = File.ReadAllText(tempSnapshotFile);

            var alphaIndex = snapshotText.IndexOf("dbo.usp_Alpha", StringComparison.Ordinal);
            var zetaIndex = snapshotText.IndexOf("dbo.usp_Zeta", StringComparison.Ordinal);

            Assert.True(alphaIndex >= 0);
            Assert.True(zetaIndex >= 0);
            Assert.True(alphaIndex < zetaIndex);
        }
        finally
        {
            if (File.Exists(tempSqlFile))
            {
                File.Delete(tempSqlFile);
            }

            if (File.Exists(tempSnapshotFile))
            {
                File.Delete(tempSnapshotFile);
            }
        }
    }

    [Fact]
    public void Run_WithUnorderedMembers_ShouldWriteSnapshotWithDeterministicMemberOrder()
    {
        var tempSqlFile = Path.Combine(Path.GetTempPath(), $"sp-scan-sql-{Guid.NewGuid():N}.sql");
        var tempSnapshotFile = Path.Combine(Path.GetTempPath(), $"sp-scan-snapshot-{Guid.NewGuid():N}.json");

        File.WriteAllText(
            tempSqlFile,
            """
            CREATE PROCEDURE dbo.usp_GetUser
                @zeta INT,
                @alpha NVARCHAR(50) = NULL
            AS
            BEGIN
                SELECT
                    CAST(NULL AS NVARCHAR(100)) AS nickName,
                    CAST(1 AS INT) AS id;
            END
            """
        );

        using var output = new StringWriter();

        try
        {
            var exitCode = ScanCommandRunner.Run(["scan", "--sql", tempSqlFile, "--out", tempSnapshotFile], output);

            Assert.Equal(0, exitCode);
            var snapshotText = File.ReadAllText(tempSnapshotFile);

            var alphaParamIndex = snapshotText.IndexOf("\"name\":\"alpha\"", StringComparison.Ordinal);
            var zetaParamIndex = snapshotText.IndexOf("\"name\":\"zeta\"", StringComparison.Ordinal);
            var idColumnIndex = snapshotText.IndexOf("\"name\":\"id\"", StringComparison.Ordinal);
            var nickNameColumnIndex = snapshotText.IndexOf("\"name\":\"nickName\"", StringComparison.Ordinal);

            Assert.True(alphaParamIndex >= 0);
            Assert.True(zetaParamIndex >= 0);
            Assert.True(idColumnIndex >= 0);
            Assert.True(nickNameColumnIndex >= 0);

            Assert.True(alphaParamIndex < zetaParamIndex);
            Assert.True(idColumnIndex < nickNameColumnIndex);
        }
        finally
        {
            if (File.Exists(tempSqlFile))
            {
                File.Delete(tempSqlFile);
            }

            if (File.Exists(tempSnapshotFile))
            {
                File.Delete(tempSnapshotFile);
            }
        }
    }

    [Fact]
    public void Run_WithCommentedSpExecuteSql_ShouldNotSetMetadataAmbiguousFlag()
    {
        var tempSqlFile = Path.Combine(Path.GetTempPath(), $"sp-scan-sql-{Guid.NewGuid():N}.sql");
        var tempSnapshotFile = Path.Combine(Path.GetTempPath(), $"sp-scan-snapshot-{Guid.NewGuid():N}.json");

        File.WriteAllText(
            tempSqlFile,
            """
            CREATE PROCEDURE dbo.usp_GetUser
            AS
            BEGIN
                -- sp_executesql N'SELECT 1'
                SELECT CAST(1 AS INT) AS id;
            END
            """
        );

        using var output = new StringWriter();

        try
        {
            var exitCode = ScanCommandRunner.Run(["scan", "--sql", tempSqlFile, "--out", tempSnapshotFile], output);

            Assert.Equal(0, exitCode);
            var snapshotText = File.ReadAllText(tempSnapshotFile);
            Assert.Contains("\"isMetadataAmbiguous\":false", snapshotText);
            Assert.Contains("\"name\":\"id\"", snapshotText);
        }
        finally
        {
            if (File.Exists(tempSqlFile))
            {
                File.Delete(tempSqlFile);
            }

            if (File.Exists(tempSnapshotFile))
            {
                File.Delete(tempSnapshotFile);
            }
        }
    }

    [Fact]
    public void Run_WithSchemaQualifiedParameterType_ShouldWriteNormalizedParameterType()
    {
        var tempSqlFile = Path.Combine(Path.GetTempPath(), $"sp-scan-sql-{Guid.NewGuid():N}.sql");
        var tempSnapshotFile = Path.Combine(Path.GetTempPath(), $"sp-scan-snapshot-{Guid.NewGuid():N}.json");

        File.WriteAllText(
            tempSqlFile,
            """
            CREATE PROCEDURE dbo.usp_GetUser
                @items [dbo].[ItemType] READONLY,
                @tenantId INT
            AS
            BEGIN
                SELECT CAST(1 AS INT) AS id;
            END
            """
        );

        using var output = new StringWriter();

        try
        {
            var exitCode = ScanCommandRunner.Run(["scan", "--sql", tempSqlFile, "--out", tempSnapshotFile], output);

            Assert.Equal(0, exitCode);
            var snapshotText = File.ReadAllText(tempSnapshotFile);
            Assert.Contains("\"name\":\"items\"", snapshotText);
            Assert.Contains("\"dbType\":\"dbo.itemtype\"", snapshotText);
            Assert.Contains("\"name\":\"tenantId\"", snapshotText);
        }
        finally
        {
            if (File.Exists(tempSqlFile))
            {
                File.Delete(tempSqlFile);
            }

            if (File.Exists(tempSnapshotFile))
            {
                File.Delete(tempSnapshotFile);
            }
        }
    }

    [Fact]
    public void Run_WithDecimalPrecisionParameter_ShouldWriteBothParameters()
    {
        var tempSqlFile = Path.Combine(Path.GetTempPath(), $"sp-scan-sql-{Guid.NewGuid():N}.sql");
        var tempSnapshotFile = Path.Combine(Path.GetTempPath(), $"sp-scan-snapshot-{Guid.NewGuid():N}.json");

        File.WriteAllText(
            tempSqlFile,
            """
            CREATE PROCEDURE dbo.usp_GetOrder
                @amount DECIMAL(18,2),
                @tenantId INT
            AS
            BEGIN
                SELECT CAST(1 AS INT) AS id;
            END
            """
        );

        using var output = new StringWriter();

        try
        {
            var exitCode = ScanCommandRunner.Run(["scan", "--sql", tempSqlFile, "--out", tempSnapshotFile], output);

            Assert.Equal(0, exitCode);
            var snapshotText = File.ReadAllText(tempSnapshotFile);
            Assert.Contains("\"name\":\"amount\"", snapshotText);
            Assert.Contains("\"dbType\":\"decimal\"", snapshotText);
            Assert.Contains("\"name\":\"tenantId\"", snapshotText);
            Assert.Contains("\"dbType\":\"int\"", snapshotText);
        }
        finally
        {
            if (File.Exists(tempSqlFile))
            {
                File.Delete(tempSqlFile);
            }

            if (File.Exists(tempSnapshotFile))
            {
                File.Delete(tempSnapshotFile);
            }
        }
    }

    [Fact]
    public void Run_WithSimpleAliasedSelectColumns_ShouldWriteResultColumns()
    {
        var tempSqlFile = Path.Combine(Path.GetTempPath(), $"sp-scan-sql-{Guid.NewGuid():N}.sql");
        var tempSnapshotFile = Path.Combine(Path.GetTempPath(), $"sp-scan-snapshot-{Guid.NewGuid():N}.json");

        File.WriteAllText(
            tempSqlFile,
            """
            CREATE PROCEDURE dbo.usp_GetOrder
            AS
            BEGIN
                SELECT 1 AS id, NULL AS nickName;
            END
            """
        );

        using var output = new StringWriter();

        try
        {
            var exitCode = ScanCommandRunner.Run(["scan", "--sql", tempSqlFile, "--out", tempSnapshotFile], output);

            Assert.Equal(0, exitCode);
            var snapshotText = File.ReadAllText(tempSnapshotFile);
            Assert.Contains("\"isMetadataAmbiguous\":false", snapshotText);
            Assert.Contains("\"name\":\"id\"", snapshotText);
            Assert.Contains("\"dbType\":\"int\"", snapshotText);
            Assert.Contains("\"name\":\"nickName\"", snapshotText);
            Assert.Contains("\"dbType\":\"unknown\"", snapshotText);
        }
        finally
        {
            if (File.Exists(tempSqlFile))
            {
                File.Delete(tempSqlFile);
            }

            if (File.Exists(tempSnapshotFile))
            {
                File.Delete(tempSnapshotFile);
            }
        }
    }

    [Fact]
    public void Run_WithSimpleAliasedSelectWithoutSemicolon_ShouldWriteResultColumns()
    {
        var tempSqlFile = Path.Combine(Path.GetTempPath(), $"sp-scan-sql-{Guid.NewGuid():N}.sql");
        var tempSnapshotFile = Path.Combine(Path.GetTempPath(), $"sp-scan-snapshot-{Guid.NewGuid():N}.json");

        File.WriteAllText(
            tempSqlFile,
            """
            CREATE PROCEDURE dbo.usp_GetOrder
            AS
            BEGIN
                SELECT 1 AS id
            END
            """
        );

        using var output = new StringWriter();

        try
        {
            var exitCode = ScanCommandRunner.Run(["scan", "--sql", tempSqlFile, "--out", tempSnapshotFile], output);

            Assert.Equal(0, exitCode);
            var snapshotText = File.ReadAllText(tempSnapshotFile);
            Assert.Contains("\"isMetadataAmbiguous\":false", snapshotText);
            Assert.Contains("\"name\":\"id\"", snapshotText);
            Assert.Contains("\"dbType\":\"int\"", snapshotText);
        }
        finally
        {
            if (File.Exists(tempSqlFile))
            {
                File.Delete(tempSqlFile);
            }

            if (File.Exists(tempSnapshotFile))
            {
                File.Delete(tempSnapshotFile);
            }
        }
    }

    [Fact]
    public void Run_WithSchemaQualifiedCastType_ShouldWriteNormalizedResultColumnType()
    {
        var tempSqlFile = Path.Combine(Path.GetTempPath(), $"sp-scan-sql-{Guid.NewGuid():N}.sql");
        var tempSnapshotFile = Path.Combine(Path.GetTempPath(), $"sp-scan-snapshot-{Guid.NewGuid():N}.json");

        File.WriteAllText(
            tempSqlFile,
            """
            CREATE PROCEDURE dbo.usp_GetOrder
            AS
            BEGIN
                SELECT CAST(NULL AS [dbo].[NameType]) AS name;
            END
            """
        );

        using var output = new StringWriter();

        try
        {
            var exitCode = ScanCommandRunner.Run(["scan", "--sql", tempSqlFile, "--out", tempSnapshotFile], output);

            Assert.Equal(0, exitCode);
            var snapshotText = File.ReadAllText(tempSnapshotFile);
            Assert.Contains("\"isMetadataAmbiguous\":false", snapshotText);
            Assert.Contains("\"name\":\"name\"", snapshotText);
            Assert.Contains("\"dbType\":\"dbo.nametype\"", snapshotText);
        }
        finally
        {
            if (File.Exists(tempSqlFile))
            {
                File.Delete(tempSqlFile);
            }

            if (File.Exists(tempSnapshotFile))
            {
                File.Delete(tempSnapshotFile);
            }
        }
    }

    [Fact]
    public void Run_WithTopClauseAndSimpleAliasedColumns_ShouldWriteResultColumns()
    {
        var tempSqlFile = Path.Combine(Path.GetTempPath(), $"sp-scan-sql-{Guid.NewGuid():N}.sql");
        var tempSnapshotFile = Path.Combine(Path.GetTempPath(), $"sp-scan-snapshot-{Guid.NewGuid():N}.json");

        File.WriteAllText(
            tempSqlFile,
            """
            CREATE PROCEDURE dbo.usp_GetOrder
            AS
            BEGIN
                SELECT TOP 1 1 AS id, NULL AS nickName;
            END
            """
        );

        using var output = new StringWriter();

        try
        {
            var exitCode = ScanCommandRunner.Run(["scan", "--sql", tempSqlFile, "--out", tempSnapshotFile], output);

            Assert.Equal(0, exitCode);
            var snapshotText = File.ReadAllText(tempSnapshotFile);
            Assert.Contains("\"isMetadataAmbiguous\":false", snapshotText);
            Assert.Contains("\"name\":\"id\"", snapshotText);
            Assert.Contains("\"dbType\":\"int\"", snapshotText);
            Assert.Contains("\"name\":\"nickName\"", snapshotText);
            Assert.Contains("\"dbType\":\"unknown\"", snapshotText);
        }
        finally
        {
            if (File.Exists(tempSqlFile))
            {
                File.Delete(tempSqlFile);
            }

            if (File.Exists(tempSnapshotFile))
            {
                File.Delete(tempSnapshotFile);
            }
        }
    }

    [Fact]
    public void Run_WithScanAndMissingSqlPath_ShouldReturnFour()
    {
        using var output = new StringWriter();

        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-sql-{Guid.NewGuid():N}.sql");
        var exitCode = ScanCommandRunner.Run(["scan", "--sql", missingPath], output);

        Assert.Equal(4, exitCode);
        Assert.Contains("Configuration error", output.ToString());
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
            Assert.Contains("Contract warnings detected", output.ToString());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Program_WithVerifyAndOnlyOptionalParameterAddedAndStrict_ShouldReturnTwo()
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
            var exitCode = CliCommandRunner.Run(["verify", "--baseline", baselinePath, "--current", currentPath, "--strict"], output);

            Assert.Equal(2, exitCode);
            Assert.Contains("Strict mode escalated", output.ToString());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Program_WithVerifyAndUnknownMetadata_ShouldReturnZero()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sp-verify-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var baselinePath = Path.Combine(tempDir, "baseline.json");
        var currentPath = Path.Combine(tempDir, "current.json");

        File.WriteAllText(
            baselinePath,
            "{\"schemaVersion\":\"1.0\",\"storedProcedures\":[{\"name\":\"usp_demo\",\"isMetadataAmbiguous\":true,\"parameters\":[],\"resultColumns\":[]}]}"
        );
        File.WriteAllText(
            currentPath,
            "{\"schemaVersion\":\"1.0\",\"storedProcedures\":[{\"name\":\"usp_demo\",\"isMetadataAmbiguous\":true,\"parameters\":[],\"resultColumns\":[]}]}"
        );

        using var output = new StringWriter();

        try
        {
            var exitCode = CliCommandRunner.Run(["verify", "--baseline", baselinePath, "--current", currentPath], output);

            Assert.Equal(0, exitCode);
            Assert.Contains("Contract analysis contains unknown changes", output.ToString());
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
