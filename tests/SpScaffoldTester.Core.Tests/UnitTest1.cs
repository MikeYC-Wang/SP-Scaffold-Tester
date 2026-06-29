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
    }
}
