namespace SpScaffoldTester.Core.Scanning;

public sealed class ParameterContract
{
    public string Name { get; init; } = string.Empty;
    public string DbType { get; init; } = string.Empty;
    public bool IsOptional { get; init; }
}
