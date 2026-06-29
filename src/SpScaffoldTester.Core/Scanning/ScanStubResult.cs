namespace SpScaffoldTester.Core.Scanning;

public sealed class ScanStubResult
{
    public string Command { get; init; } = "scan";
    public string Status { get; init; } = "stub";
    public string Message { get; init; } = "Scan pipeline is not implemented yet.";
}
