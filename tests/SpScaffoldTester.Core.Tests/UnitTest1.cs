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

        Assert.Equal(ContractDiffSeverity.None, result.Severity);
    }
}
