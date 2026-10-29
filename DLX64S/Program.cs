namespace DLX64S;

static class Program
{
    static int Main(string[] args)
    {
        long seed    = Environment.TickCount64;
        int  count   = 1;
        string? outPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--seed":  seed    = ParseSeed(args[++i]);  break;
                case "--count": count   = int.Parse(args[++i]);  break;
                case "--out":   outPath = args[++i];             break;
                default:
                    Console.Error.WriteLine($"Unknown argument: {args[i]}");
                    return 1;
            }
        }

        TextWriter output = outPath is null
            ? Console.Out
            : new StreamWriter(outPath);

        try   { Generator.Generate(seed, count, output); }
        finally { if (outPath is not null) output.Dispose(); }

        return 0;
    }

    // Accepts decimal or 0x/0X-prefixed hex; full uint64 range reinterpreted as int64.
    static long ParseSeed(string s) =>
        s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? unchecked((long)Convert.ToUInt64(s[2..], 16))
            : long.Parse(s);
}
