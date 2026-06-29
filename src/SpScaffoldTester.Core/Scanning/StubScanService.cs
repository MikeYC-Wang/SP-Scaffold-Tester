namespace SpScaffoldTester.Core.Scanning;

public sealed class StubScanService : IScanService
{
    public ScanStubResult RunScan()
    {
        return new ScanStubResult();
    }
}
