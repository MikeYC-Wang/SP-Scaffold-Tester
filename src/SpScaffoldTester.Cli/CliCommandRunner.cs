namespace SpScaffoldTester.Cli;

public static class CliCommandRunner
{
    public static int Run(string[] args, TextWriter output)
    {
        if (args.Length == 0)
        {
            output.WriteLine("Usage: sp-scaffold-tester <scan|verify>");
            return 1;
        }

        if (args[0].Equals("scan", StringComparison.OrdinalIgnoreCase))
        {
            return ScanCommandRunner.Run(args, output);
        }

        if (args[0].Equals("verify", StringComparison.OrdinalIgnoreCase))
        {
            return VerifyCommandRunner.Run(args, output);
        }

        output.WriteLine("Usage: sp-scaffold-tester <scan|verify>");
        return 1;
    }
}
