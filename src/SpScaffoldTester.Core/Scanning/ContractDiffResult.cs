namespace SpScaffoldTester.Core.Scanning;

public sealed class ContractDiffResult
{
    public ContractDiffSeverity Severity { get; init; } = ContractDiffSeverity.None;
}

public enum ContractDiffSeverity
{
    None = 0,
    Breaking = 1
}
