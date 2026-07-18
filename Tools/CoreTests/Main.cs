// Entry point of the Core test harness. Two modes:
//   dotnet run --project Tools/CoreTests                 assertion suite (exit code 1 on failure)
//   dotnet run --project Tools/CoreTests -- baseline     scripted playthrough trace on stdout
// The baseline trace is the regression net: capture it before a Core refactor, diff after.

using System;

public static class CoreTestMain
{
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "baseline")
        {
            Console.Out.Write(Baseline.RunAll());
            return 0;
        }
        return JokerTests.RunAll();
    }
}
