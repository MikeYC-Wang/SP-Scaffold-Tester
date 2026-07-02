using SpScaffoldTester.Core.Scanning;

namespace SpScaffoldTester.Core.Tests;

public class StubScanServiceTests
{
    [Fact]
    public void RunScan_ShouldReturnScanStubResult()
    {
        var service = new StubScanService();

        var result = service.RunScan();

        Assert.Equal("scan", result.Command);
        Assert.Equal("stub", result.Status);
        Assert.Equal("Scan pipeline is not implemented yet.", result.Message);
        Assert.Equal("1.0", result.Snapshot.SchemaVersion);
        Assert.Empty(result.Snapshot.StoredProcedures);
    }
}

public class SqlFileScanServiceTests
{
    [Fact]
    public void RunScan_WithSqlFile_ShouldParseProcedureAndParameters()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sp-scan-sql-{Guid.NewGuid():N}.sql");
        File.WriteAllText(
            tempFile,
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

        try
        {
            var service = new SqlFileScanService(tempFile);

            var result = service.RunScan();

            Assert.Equal("scanned", result.Status);
            Assert.Single(result.Snapshot.StoredProcedures);

            var sp = result.Snapshot.StoredProcedures[0];
            Assert.Equal("dbo.usp_GetUser", sp.Name);
            Assert.Equal(2, sp.Parameters.Count);

            Assert.Equal("id", sp.Parameters[0].Name);
            Assert.Equal("int", sp.Parameters[0].DbType);
            Assert.False(sp.Parameters[0].IsOptional);

            Assert.Equal("traceId", sp.Parameters[1].Name);
            Assert.Equal("nvarchar", sp.Parameters[1].DbType);
            Assert.True(sp.Parameters[1].IsOptional);
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
    public void RunScan_WithSelectCastColumns_ShouldParseResultColumns()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sp-scan-sql-{Guid.NewGuid():N}.sql");
        File.WriteAllText(
            tempFile,
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

        try
        {
            var service = new SqlFileScanService(tempFile);

            var result = service.RunScan();

            var sp = Assert.Single(result.Snapshot.StoredProcedures);
            Assert.Equal(2, sp.ResultColumns.Count);

            Assert.Equal("nickName", sp.ResultColumns[0].Name);
            Assert.Equal("nvarchar", sp.ResultColumns[0].DbType);
            Assert.True(sp.ResultColumns[0].IsNullable);

            Assert.Equal("userId", sp.ResultColumns[1].Name);
            Assert.Equal("int", sp.ResultColumns[1].DbType);
            Assert.False(sp.ResultColumns[1].IsNullable);
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
    public void RunScan_WithUnsupportedSelectProjection_ShouldMarkMetadataAmbiguous()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sp-scan-sql-{Guid.NewGuid():N}.sql");
        File.WriteAllText(
            tempFile,
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

        try
        {
            var service = new SqlFileScanService(tempFile);

            var result = service.RunScan();

            var sp = Assert.Single(result.Snapshot.StoredProcedures);
            Assert.True(sp.IsMetadataAmbiguous);
            Assert.Empty(sp.ResultColumns);
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
    public void RunScan_WithMultipleProcedures_ShouldReturnDeterministicOrderByName()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sp-scan-sql-{Guid.NewGuid():N}.sql");
        File.WriteAllText(
            tempFile,
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

        try
        {
            var service = new SqlFileScanService(tempFile);

            var result = service.RunScan();

            Assert.Equal(2, result.Snapshot.StoredProcedures.Count);
            Assert.Equal("dbo.usp_Alpha", result.Snapshot.StoredProcedures[0].Name);
            Assert.Equal("dbo.usp_Zeta", result.Snapshot.StoredProcedures[1].Name);
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
    public void RunScan_WithUnorderedMembers_ShouldReturnParametersAndColumnsInNameOrder()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sp-scan-sql-{Guid.NewGuid():N}.sql");
        File.WriteAllText(
            tempFile,
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

        try
        {
            var service = new SqlFileScanService(tempFile);

            var result = service.RunScan();

            var sp = Assert.Single(result.Snapshot.StoredProcedures);
            Assert.Equal(2, sp.Parameters.Count);
            Assert.Equal("alpha", sp.Parameters[0].Name);
            Assert.Equal("zeta", sp.Parameters[1].Name);

            Assert.Equal(2, sp.ResultColumns.Count);
            Assert.Equal("id", sp.ResultColumns[0].Name);
            Assert.Equal("nickName", sp.ResultColumns[1].Name);
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
    public void RunScan_WithCommentedSpExecuteSql_ShouldIgnoreCommentForAmbiguityDetection()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sp-scan-sql-{Guid.NewGuid():N}.sql");
        File.WriteAllText(
            tempFile,
            """
            CREATE PROCEDURE dbo.usp_GetUser
            AS
            BEGIN
                -- sp_executesql N'SELECT 1'
                SELECT CAST(1 AS INT) AS id;
            END
            """
        );

        try
        {
            var service = new SqlFileScanService(tempFile);

            var result = service.RunScan();

            var sp = Assert.Single(result.Snapshot.StoredProcedures);
            Assert.False(sp.IsMetadataAmbiguous);
            Assert.Single(sp.ResultColumns);
            Assert.Equal("id", sp.ResultColumns[0].Name);
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
    public void RunScan_WithSchemaQualifiedParameterType_ShouldParseTableValuedParameter()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sp-scan-sql-{Guid.NewGuid():N}.sql");
        File.WriteAllText(
            tempFile,
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

        try
        {
            var service = new SqlFileScanService(tempFile);

            var result = service.RunScan();

            var sp = Assert.Single(result.Snapshot.StoredProcedures);
            Assert.Equal(2, sp.Parameters.Count);
            Assert.Equal("items", sp.Parameters[0].Name);
            Assert.Equal("dbo.itemtype", sp.Parameters[0].DbType);
            Assert.False(sp.Parameters[0].IsOptional);
            Assert.Equal("tenantId", sp.Parameters[1].Name);
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
    public void RunScan_WithDecimalPrecisionParameter_ShouldParseAllParameters()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sp-scan-sql-{Guid.NewGuid():N}.sql");
        File.WriteAllText(
            tempFile,
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

        try
        {
            var service = new SqlFileScanService(tempFile);

            var result = service.RunScan();

            var sp = Assert.Single(result.Snapshot.StoredProcedures);
            Assert.Equal(2, sp.Parameters.Count);
            Assert.Equal("amount", sp.Parameters[0].Name);
            Assert.Equal("decimal", sp.Parameters[0].DbType);
            Assert.Equal("tenantId", sp.Parameters[1].Name);
            Assert.Equal("int", sp.Parameters[1].DbType);
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
    public void RunScan_WithSimpleAliasedSelectColumns_ShouldParseResultColumns()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sp-scan-sql-{Guid.NewGuid():N}.sql");
        File.WriteAllText(
            tempFile,
            """
            CREATE PROCEDURE dbo.usp_GetOrder
            AS
            BEGIN
                SELECT 1 AS id, NULL AS nickName;
            END
            """
        );

        try
        {
            var service = new SqlFileScanService(tempFile);

            var result = service.RunScan();

            var sp = Assert.Single(result.Snapshot.StoredProcedures);
            Assert.False(sp.IsMetadataAmbiguous);
            Assert.Equal(2, sp.ResultColumns.Count);
            Assert.Equal("id", sp.ResultColumns[0].Name);
            Assert.Equal("int", sp.ResultColumns[0].DbType);
            Assert.False(sp.ResultColumns[0].IsNullable);
            Assert.Equal("nickName", sp.ResultColumns[1].Name);
            Assert.Equal("unknown", sp.ResultColumns[1].DbType);
            Assert.True(sp.ResultColumns[1].IsNullable);
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
    public void RunScan_WithSimpleAliasedSelectWithoutSemicolon_ShouldParseResultColumns()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sp-scan-sql-{Guid.NewGuid():N}.sql");
        File.WriteAllText(
            tempFile,
            """
            CREATE PROCEDURE dbo.usp_GetOrder
            AS
            BEGIN
                SELECT 1 AS id
            END
            """
        );

        try
        {
            var service = new SqlFileScanService(tempFile);

            var result = service.RunScan();

            var sp = Assert.Single(result.Snapshot.StoredProcedures);
            Assert.False(sp.IsMetadataAmbiguous);
            Assert.Single(sp.ResultColumns);
            Assert.Equal("id", sp.ResultColumns[0].Name);
            Assert.Equal("int", sp.ResultColumns[0].DbType);
            Assert.False(sp.ResultColumns[0].IsNullable);
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
    public void RunScan_WithSchemaQualifiedCastType_ShouldParseResultColumns()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sp-scan-sql-{Guid.NewGuid():N}.sql");
        File.WriteAllText(
            tempFile,
            """
            CREATE PROCEDURE dbo.usp_GetOrder
            AS
            BEGIN
                SELECT CAST(NULL AS [dbo].[NameType]) AS name;
            END
            """
        );

        try
        {
            var service = new SqlFileScanService(tempFile);

            var result = service.RunScan();

            var sp = Assert.Single(result.Snapshot.StoredProcedures);
            Assert.False(sp.IsMetadataAmbiguous);
            Assert.Single(sp.ResultColumns);
            Assert.Equal("name", sp.ResultColumns[0].Name);
            Assert.Equal("dbo.nametype", sp.ResultColumns[0].DbType);
            Assert.True(sp.ResultColumns[0].IsNullable);
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
    public void RunScan_WithTopClauseAndSimpleAliasedColumns_ShouldParseResultColumns()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sp-scan-sql-{Guid.NewGuid():N}.sql");
        File.WriteAllText(
            tempFile,
            """
            CREATE PROCEDURE dbo.usp_GetOrder
            AS
            BEGIN
                SELECT TOP 1 1 AS id, NULL AS nickName;
            END
            """
        );

        try
        {
            var service = new SqlFileScanService(tempFile);

            var result = service.RunScan();

            var sp = Assert.Single(result.Snapshot.StoredProcedures);
            Assert.False(sp.IsMetadataAmbiguous);
            Assert.Equal(2, sp.ResultColumns.Count);
            Assert.Equal("id", sp.ResultColumns[0].Name);
            Assert.Equal("int", sp.ResultColumns[0].DbType);
            Assert.False(sp.ResultColumns[0].IsNullable);
            Assert.Equal("nickName", sp.ResultColumns[1].Name);
            Assert.Equal("unknown", sp.ResultColumns[1].DbType);
            Assert.True(sp.ResultColumns[1].IsNullable);
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
    public void RunScan_WithDistinctAndSimpleAliasedColumns_ShouldParseResultColumns()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sp-scan-sql-{Guid.NewGuid():N}.sql");
        File.WriteAllText(
            tempFile,
            """
            CREATE PROCEDURE dbo.usp_GetOrder
            AS
            BEGIN
                SELECT DISTINCT 1 AS id, NULL AS nickName;
            END
            """
        );

        try
        {
            var service = new SqlFileScanService(tempFile);

            var result = service.RunScan();

            var sp = Assert.Single(result.Snapshot.StoredProcedures);
            Assert.False(sp.IsMetadataAmbiguous);
            Assert.Equal(2, sp.ResultColumns.Count);
            Assert.Equal("id", sp.ResultColumns[0].Name);
            Assert.Equal("int", sp.ResultColumns[0].DbType);
            Assert.False(sp.ResultColumns[0].IsNullable);
            Assert.Equal("nickName", sp.ResultColumns[1].Name);
            Assert.Equal("unknown", sp.ResultColumns[1].DbType);
            Assert.True(sp.ResultColumns[1].IsNullable);
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
    public void RunScan_WithCreateProcAbbreviation_ShouldParseProcedureAndParameters()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sp-scan-sql-{Guid.NewGuid():N}.sql");
        File.WriteAllText(
            tempFile,
            """
            CREATE PROC dbo.usp_GetUser
                @id INT
            AS
            BEGIN
                SELECT CAST(1 AS INT) AS id;
            END
            """
        );

        try
        {
            var service = new SqlFileScanService(tempFile);

            var result = service.RunScan();

            var sp = Assert.Single(result.Snapshot.StoredProcedures);
            Assert.Equal("dbo.usp_GetUser", sp.Name);
            Assert.Single(sp.Parameters);
            Assert.Equal("id", sp.Parameters[0].Name);
            Assert.Equal("int", sp.Parameters[0].DbType);
            Assert.False(sp.IsMetadataAmbiguous);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}

public class ContractDiffEngineTests
{
    [Fact]
    public void Compare_WhenParameterRemoved_ShouldBeBreaking()
    {
        var engine = new ContractDiffEngine();
        var baseline = new ScanSnapshot
        {
            StoredProcedures =
            [
                new StoredProcedureContract
                {
                    Name = "usp_demo",
                    Parameters = [new ParameterContract { Name = "id", DbType = "int", IsOptional = false }]
                }
            ]
        };

        var current = new ScanSnapshot
        {
            StoredProcedures =
            [
                new StoredProcedureContract
                {
                    Name = "usp_demo",
                    Parameters = []
                }
            ]
        };

        var result = engine.Compare(baseline, current);

        Assert.Equal(ContractDiffSeverity.Breaking, result.Severity);
        Assert.Single(result.Reasons);
        Assert.Contains("Parameter removed", result.Reasons[0]);
        Assert.Single(result.Items);
        Assert.Equal(ContractDiffCode.ParameterRemoved, result.Items[0].Code);
        Assert.Equal("usp_demo", result.Items[0].StoredProcedure);
        Assert.Equal("id", result.Items[0].MemberName);
        Assert.Equal("int", result.Items[0].BaselineType);
        Assert.Null(result.Items[0].CurrentType);
    }

    [Fact]
    public void Compare_WhenOnlyOptionalParameterAdded_ShouldBeNone()
    {
        var engine = new ContractDiffEngine();
        var baseline = new ScanSnapshot
        {
            StoredProcedures =
            [
                new StoredProcedureContract
                {
                    Name = "usp_demo",
                    Parameters = []
                }
            ]
        };

        var current = new ScanSnapshot
        {
            StoredProcedures =
            [
                new StoredProcedureContract
                {
                    Name = "usp_demo",
                    Parameters = [new ParameterContract { Name = "traceId", DbType = "nvarchar", IsOptional = true }]
                }
            ]
        };

        var result = engine.Compare(baseline, current);

        Assert.Equal(ContractDiffSeverity.Warning, result.Severity);
        Assert.Single(result.Reasons);
        Assert.Single(result.Items);
        Assert.Equal(ContractDiffCode.OptionalParameterAdded, result.Items[0].Code);
    }

    [Fact]
    public void Compare_WhenMetadataIsAmbiguous_ShouldBeUnknown()
    {
        var engine = new ContractDiffEngine();
        var baseline = new ScanSnapshot
        {
            StoredProcedures =
            [
                new StoredProcedureContract
                {
                    Name = "usp_demo",
                    Parameters = [],
                    IsMetadataAmbiguous = true
                }
            ]
        };

        var current = new ScanSnapshot
        {
            StoredProcedures =
            [
                new StoredProcedureContract
                {
                    Name = "usp_demo",
                    Parameters = [],
                    IsMetadataAmbiguous = true
                }
            ]
        };

        var result = engine.Compare(baseline, current);

        Assert.Equal(ContractDiffSeverity.Unknown, result.Severity);
        Assert.Single(result.Items);
        Assert.Equal(ContractDiffCode.MetadataAmbiguous, result.Items[0].Code);
    }
}
