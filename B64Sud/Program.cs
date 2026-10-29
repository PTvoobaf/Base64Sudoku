using B64Sud;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  B64Sud --solve <puzzle-file>");
    Console.Error.WriteLine("  B64Sud --dig   <solution-file> [target-empty-cells] [--tier2cap N] [--tier3cap N] [--tier4cap N]");
    return 1;
}

string command = args[0];
string filePath = args[1];

if (command == "--solve")
{
    var puzzle = GridParser.Parse(File.ReadAllText(filePath));
    var result = Solver.Solve(puzzle);

    switch (result.Outcome)
    {
        case SolveOutcome.Solved:
            Console.Write(GridParser.Serialize(result.Solution!));
            Console.Error.WriteLine(
                $"Solved. Tier calls: T1={result.TierCalls[0]} T2={result.TierCalls[1]} T3={result.TierCalls[2]} T4={result.TierCalls[3]}");
            return 0;
        case SolveOutcome.Invalid:
            Console.Error.WriteLine("Invalid puzzle.");
            return 2;
        case SolveOutcome.RequiresHigherTier:
            if (result.Solution != null)
                Console.Write(GridParser.Serialize(result.Solution));
            Console.Error.WriteLine(
                $"{(result.Solution != null ? "Non-unique puzzle (first solution shown)" : "Requires higher tier")}." +
                $" Tier calls: T1={result.TierCalls[0]} T2={result.TierCalls[1]} T3={result.TierCalls[2]} T4={result.TierCalls[3]}");
            return 3;
    }
}
else if (command == "--dig")
{
    int target = 2048;
    int? tier2cap = null;
    int? tier3cap = null;
    int? tier4cap = null;

    for (int i = 2; i < args.Length; i++)
    {
        if (args[i] == "--tier2cap" && i + 1 < args.Length && int.TryParse(args[i + 1], out int t2))
            { tier2cap = t2; i++; }
        else if (args[i] == "--tier3cap" && i + 1 < args.Length && int.TryParse(args[i + 1], out int t3))
            { tier3cap = t3; i++; }
        else if (args[i] == "--tier4cap" && i + 1 < args.Length && int.TryParse(args[i + 1], out int t4))
            { tier4cap = t4; i++; }
        else if (i == 2 && int.TryParse(args[i], out int t))
            target = t;
    }

    int[]? tierCaps = null;
    if (tier2cap.HasValue || tier3cap.HasValue || tier4cap.HasValue)
        tierCaps = [int.MaxValue, tier2cap ?? int.MaxValue, tier3cap ?? int.MaxValue, tier4cap ?? int.MaxValue];

    var solution = GridParser.Parse(File.ReadAllText(filePath));
    var profile = new DiggerProfile { TargetEmptyCells = target, TierCaps = tierCaps };
    var digResult = Digger.Dig(solution, profile);
    Console.Write(GridParser.Serialize(digResult.Puzzle));
    Console.Error.WriteLine(
        $"Dug {digResult.RemovedCells} cells. Cells requiring higher tiers:" +
        $" Tier-2: {digResult.TierCellCounts[1]}" +
        $" Tier-3: {digResult.TierCellCounts[2]}" +
        $" Tier-4: {digResult.TierCellCounts[3]}");
    return 0;
}
else
{
    Console.Error.WriteLine($"Unknown command: {command}");
    return 1;
}

return 0;
