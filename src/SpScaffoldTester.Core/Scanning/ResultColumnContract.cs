namespace SpScaffoldTester.Core.Scanning;

public sealed class ResultColumnContract
{
    public string Name { get; init; } = string.Empty;
    public string DbType { get; init; } = string.Empty;
    public bool IsNullable { get; init; }
}
