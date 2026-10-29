namespace DLX64S;

internal static class Generator
{
    public static void Generate(long seed, int count, TextWriter output)
    {
        Console.Error.WriteLine($"Seed: {seed}");
        var rng   = new Random((int)(seed ^ (seed >> 32)));
        var watch = System.Diagnostics.Stopwatch.StartNew();

        int emitted  = 0;
        int attempts = 0;
        while (emitted < count)
        {
            attempts++;
            var grid = new Annealer(rng).Run(
                onProgress: (step, cost, temp) =>
                    Console.Error.Write(
                        $"\r[{watch.Elapsed:mm\\:ss}]  attempt {attempts}" +
                        $"  step {step / 1_000_000.0:F1}M  cost {cost,5}  T {temp:F5}   "),
                progressIntervalMs: 500);

            Console.Error.WriteLine();

            if (grid == null)
            {
                Console.Error.WriteLine($"  attempt {attempts} did not converge — retrying");
                continue;
            }

            if (emitted > 0) output.WriteLine();
            output.Write(GridParser.Serialize(grid));
            output.WriteLine();
            emitted++;
        }

        Console.Error.WriteLine(
            $"Done: {emitted} grid(s), {attempts} attempt(s), {watch.Elapsed:mm\\:ss} elapsed.");
    }
}
