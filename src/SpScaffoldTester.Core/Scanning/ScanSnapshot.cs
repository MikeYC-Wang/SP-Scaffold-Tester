namespace SpScaffoldTester.Core.Scanning;

public sealed class ScanSnapshot
{
    public string SchemaVersion { get; init; } = "1.0";
    public IReadOnlyList<StoredProcedureContract> StoredProcedures { get; init; } = [];
}
