namespace SpScaffoldTester.Core.Scanning;

public sealed class ContractDiffEngine
{
    public ContractDiffResult Compare(ScanSnapshot baseline, ScanSnapshot current)
    {
        var baselineByName = baseline.StoredProcedures.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var currentByName = current.StoredProcedures.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var items = new List<ContractDiffItem>();

        foreach (var (spName, baselineSp) in baselineByName)
        {
            if (!currentByName.TryGetValue(spName, out var currentSp))
            {
                items.Add(new ContractDiffItem
                {
                    Type = "StoredProcedureRemoved",
                    StoredProcedure = spName,
                    Message = $"Stored procedure removed: {spName}"
                });
                continue;
            }

            items.AddRange(GetBreakingParameterDiffItems(baselineSp, currentSp));
            items.AddRange(GetBreakingResultColumnDiffItems(baselineSp, currentSp));
        }

        foreach (var spName in currentByName.Keys)
        {
            if (!baselineByName.ContainsKey(spName))
            {
                items.Add(new ContractDiffItem
                {
                    Type = "StoredProcedureAdded",
                    StoredProcedure = spName,
                    Message = $"Stored procedure added: {spName}"
                });
            }
        }

        return new ContractDiffResult
        {
            Severity = items.Count > 0 ? ContractDiffSeverity.Breaking : ContractDiffSeverity.None,
            Reasons = items.Select(x => x.Message).ToArray(),
            Items = items
        };
    }

    private static IReadOnlyList<ContractDiffItem> GetBreakingParameterDiffItems(StoredProcedureContract baselineSp, StoredProcedureContract currentSp)
    {
        var items = new List<ContractDiffItem>();
        var currentParameters = currentSp.Parameters.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var baselineParameter in baselineSp.Parameters)
        {
            if (!currentParameters.TryGetValue(baselineParameter.Name, out var currentParameter))
            {
                items.Add(new ContractDiffItem
                {
                    Type = "ParameterRemoved",
                    StoredProcedure = baselineSp.Name,
                    MemberName = baselineParameter.Name,
                    BaselineType = baselineParameter.DbType,
                    Message = $"Parameter removed: {baselineSp.Name}.{baselineParameter.Name}"
                });
                continue;
            }

            if (!string.Equals(baselineParameter.DbType, currentParameter.DbType, StringComparison.OrdinalIgnoreCase))
            {
                items.Add(new ContractDiffItem
                {
                    Type = "ParameterTypeChanged",
                    StoredProcedure = baselineSp.Name,
                    MemberName = baselineParameter.Name,
                    BaselineType = baselineParameter.DbType,
                    CurrentType = currentParameter.DbType,
                    Message = $"Parameter type changed: {baselineSp.Name}.{baselineParameter.Name} ({baselineParameter.DbType} -> {currentParameter.DbType})"
                });
            }
        }

        return items;
    }

    private static IReadOnlyList<ContractDiffItem> GetBreakingResultColumnDiffItems(StoredProcedureContract baselineSp, StoredProcedureContract currentSp)
    {
        var items = new List<ContractDiffItem>();
        var currentColumns = currentSp.ResultColumns.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var baselineColumn in baselineSp.ResultColumns)
        {
            if (!currentColumns.TryGetValue(baselineColumn.Name, out var currentColumn))
            {
                items.Add(new ContractDiffItem
                {
                    Type = "ResultColumnRemoved",
                    StoredProcedure = baselineSp.Name,
                    MemberName = baselineColumn.Name,
                    BaselineType = baselineColumn.DbType,
                    Message = $"Result column removed: {baselineSp.Name}.{baselineColumn.Name}"
                });
                continue;
            }

            if (!string.Equals(baselineColumn.DbType, currentColumn.DbType, StringComparison.OrdinalIgnoreCase))
            {
                items.Add(new ContractDiffItem
                {
                    Type = "ResultColumnTypeChanged",
                    StoredProcedure = baselineSp.Name,
                    MemberName = baselineColumn.Name,
                    BaselineType = baselineColumn.DbType,
                    CurrentType = currentColumn.DbType,
                    Message = $"Result column type changed: {baselineSp.Name}.{baselineColumn.Name} ({baselineColumn.DbType} -> {currentColumn.DbType})"
                });
            }
        }

        return items;
    }
}
