namespace SpScaffoldTester.Core.Scanning;

public sealed class StoredProcedureContract
{
    public string Name { get; init; } = string.Empty;
    public bool IsMetadataAmbiguous { get; init; }
    public IReadOnlyList<ParameterContract> Parameters { get; init; } = [];
    public IReadOnlyList<ResultColumnContract> ResultColumns { get; init; } = [];
}
