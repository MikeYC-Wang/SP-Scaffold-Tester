namespace SpScaffoldTester.Core.Scanning;

public sealed class ContractDiffEngine
{
    public ContractDiffResult Compare(ScanSnapshot baseline, ScanSnapshot current)
    {
        var baselineByName = baseline.StoredProcedures.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var currentByName = current.StoredProcedures.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var (spName, baselineSp) in baselineByName)
        {
            if (!currentByName.TryGetValue(spName, out var currentSp))
            {
                return new ContractDiffResult { Severity = ContractDiffSeverity.Breaking };
            }

            if (HasBreakingParameterDiff(baselineSp, currentSp))
            {
                return new ContractDiffResult { Severity = ContractDiffSeverity.Breaking };
            }

            if (HasBreakingResultColumnDiff(baselineSp, currentSp))
            {
                return new ContractDiffResult { Severity = ContractDiffSeverity.Breaking };
            }
        }

        foreach (var spName in currentByName.Keys)
        {
            if (!baselineByName.ContainsKey(spName))
            {
                return new ContractDiffResult { Severity = ContractDiffSeverity.Breaking };
            }
        }

        return new ContractDiffResult { Severity = ContractDiffSeverity.None };
    }

    private static bool HasBreakingParameterDiff(StoredProcedureContract baselineSp, StoredProcedureContract currentSp)
    {
        var currentParameters = currentSp.Parameters.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var baselineParameter in baselineSp.Parameters)
        {
            if (!currentParameters.TryGetValue(baselineParameter.Name, out var currentParameter))
            {
                return true;
            }

            if (!string.Equals(baselineParameter.DbType, currentParameter.DbType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasBreakingResultColumnDiff(StoredProcedureContract baselineSp, StoredProcedureContract currentSp)
    {
        var currentColumns = currentSp.ResultColumns.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var baselineColumn in baselineSp.ResultColumns)
        {
            if (!currentColumns.TryGetValue(baselineColumn.Name, out var currentColumn))
            {
                return true;
            }

            if (!string.Equals(baselineColumn.DbType, currentColumn.DbType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
