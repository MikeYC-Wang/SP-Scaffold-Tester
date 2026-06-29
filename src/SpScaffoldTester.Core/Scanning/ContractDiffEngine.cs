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
                    Code = ContractDiffCode.StoredProcedureRemoved,
                    StoredProcedure = spName,
                    Message = $"Stored procedure removed: {spName}"
                });
                continue;
            }

            if (baselineSp.IsMetadataAmbiguous || currentSp.IsMetadataAmbiguous)
            {
                items.Add(new ContractDiffItem
                {
                    Code = ContractDiffCode.MetadataAmbiguous,
                    StoredProcedure = spName,
                    Message = $"Metadata ambiguous: {spName}"
                });
                continue;
            }

            items.AddRange(GetBreakingParameterDiffItems(baselineSp, currentSp));
            items.AddRange(GetWarningParameterDiffItems(baselineSp, currentSp));
            items.AddRange(GetBreakingResultColumnDiffItems(baselineSp, currentSp));
            items.AddRange(GetWarningResultColumnDiffItems(baselineSp, currentSp));
        }

        foreach (var spName in currentByName.Keys)
        {
            if (!baselineByName.ContainsKey(spName))
            {
                items.Add(new ContractDiffItem
                {
                    Code = ContractDiffCode.StoredProcedureAdded,
                    StoredProcedure = spName,
                    Message = $"Stored procedure added: {spName}"
                });
            }
        }

        return new ContractDiffResult
        {
            Severity = GetSeverity(items),
            Reasons = items.Select(x => x.Message).ToArray(),
            Items = items
        };
    }

    private static ContractDiffSeverity GetSeverity(IReadOnlyList<ContractDiffItem> items)
    {
        if (items.Any(x => x.Code is ContractDiffCode.StoredProcedureRemoved
            or ContractDiffCode.StoredProcedureAdded
            or ContractDiffCode.ParameterRemoved
            or ContractDiffCode.ParameterTypeChanged
            or ContractDiffCode.ResultColumnRemoved
            or ContractDiffCode.ResultColumnTypeChanged))
        {
            return ContractDiffSeverity.Breaking;
        }

        if (items.Any(x => x.Code == ContractDiffCode.MetadataAmbiguous))
        {
            return ContractDiffSeverity.Unknown;
        }

        if (items.Any(x => x.Code is ContractDiffCode.OptionalParameterAdded or ContractDiffCode.NullableResultColumnAdded))
        {
            return ContractDiffSeverity.Warning;
        }

        return ContractDiffSeverity.None;
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
                    Code = ContractDiffCode.ParameterRemoved,
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
                    Code = ContractDiffCode.ParameterTypeChanged,
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

    private static IReadOnlyList<ContractDiffItem> GetWarningParameterDiffItems(StoredProcedureContract baselineSp, StoredProcedureContract currentSp)
    {
        var items = new List<ContractDiffItem>();
        var baselineParameters = baselineSp.Parameters.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var currentParameter in currentSp.Parameters)
        {
            if (!baselineParameters.ContainsKey(currentParameter.Name) && currentParameter.IsOptional)
            {
                items.Add(new ContractDiffItem
                {
                    Code = ContractDiffCode.OptionalParameterAdded,
                    StoredProcedure = currentSp.Name,
                    MemberName = currentParameter.Name,
                    CurrentType = currentParameter.DbType,
                    Message = $"Optional parameter added: {currentSp.Name}.{currentParameter.Name}"
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
                    Code = ContractDiffCode.ResultColumnRemoved,
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
                    Code = ContractDiffCode.ResultColumnTypeChanged,
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

    private static IReadOnlyList<ContractDiffItem> GetWarningResultColumnDiffItems(StoredProcedureContract baselineSp, StoredProcedureContract currentSp)
    {
        var items = new List<ContractDiffItem>();
        var baselineColumns = baselineSp.ResultColumns.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var currentColumn in currentSp.ResultColumns)
        {
            if (!baselineColumns.ContainsKey(currentColumn.Name) && currentColumn.IsNullable)
            {
                items.Add(new ContractDiffItem
                {
                    Code = ContractDiffCode.NullableResultColumnAdded,
                    StoredProcedure = currentSp.Name,
                    MemberName = currentColumn.Name,
                    CurrentType = currentColumn.DbType,
                    Message = $"Nullable result column added: {currentSp.Name}.{currentColumn.Name}"
                });
            }
        }

        return items;
    }
}
