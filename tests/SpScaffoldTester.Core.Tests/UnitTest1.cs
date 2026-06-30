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

            Assert.Equal("userId", sp.ResultColumns[0].Name);
            Assert.Equal("int", sp.ResultColumns[0].DbType);
            Assert.False(sp.ResultColumns[0].IsNullable);

            Assert.Equal("nickName", sp.ResultColumns[1].Name);
            Assert.Equal("nvarchar", sp.ResultColumns[1].DbType);
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
