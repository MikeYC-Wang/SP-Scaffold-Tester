namespace SpScaffoldTester.Core.Scanning;

public enum ContractDiffCode
{
    StoredProcedureRemoved = 0,
    StoredProcedureAdded = 1,
    ParameterRemoved = 2,
    ParameterTypeChanged = 3,
    ResultColumnRemoved = 4,
    ResultColumnTypeChanged = 5
}
