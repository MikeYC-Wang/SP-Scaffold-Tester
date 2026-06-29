namespace SpScaffoldTester.Core.Scanning;

public interface ISnapshotComparer
{
    bool AreEquivalent(string baselinePath, string currentPath);
}
