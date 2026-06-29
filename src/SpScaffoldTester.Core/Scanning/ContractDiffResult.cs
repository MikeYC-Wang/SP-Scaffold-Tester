namespace SpScaffoldTester.Core.Scanning;

public sealed class ContractDiffResult
{
    public ContractDiffSeverity Severity { get; init; } = ContractDiffSeverity.None;
    public IReadOnlyList<string> Reasons { get; init; } = [];
    public IReadOnlyList<ContractDiffItem> Items { get; init; } = [];
}

public enum ContractDiffSeverity
{
    None = 0,
    Breaking = 1
}
