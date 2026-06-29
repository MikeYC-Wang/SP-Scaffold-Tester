namespace SpScaffoldTester.Core.Scanning;

public sealed class ContractDiffItem
{
    public ContractDiffCode Code { get; init; }
    public string StoredProcedure { get; init; } = string.Empty;
    public string? MemberName { get; init; }
    public string? BaselineType { get; init; }
    public string? CurrentType { get; init; }
    public string Message { get; init; } = string.Empty;
}
