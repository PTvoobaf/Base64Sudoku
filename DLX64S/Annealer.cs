namespace DLX64S;

internal sealed class Annealer
{
    private const int N = Grid.Size;      // 64
    private const int B = Grid.BlockSize; // 8

    // Cooling schedule: T drops by factor Alpha each step.
    // At Alpha=0.9999995 over 50M steps: T ≈ T0 * e^(-250) ≈ 0.
    private const double T0       = 2.0;
    private const double Alpha    = 0.9999995;
    public  const int    MaxSteps = 50_000_000;

    private readonly Random _rng;
    private readonly int[,] _grid;     // [r, c] → symbol index 0..N-1
    private readonly int[,] _colCount; // [col, symbol] → count in that column
    private readonly int[,] _boxCount; // [box, symbol] → count in that box
    private int    _cost;
    private double _temp;

    public int  Cost => _cost;
    public double Temp => _temp;

    public Annealer(Random rng)
    {
        _rng      = rng;
        _grid     = new int[N, N];
        _colCount = new int[N, N];
        _boxCount = new int[N, N];

        // Each row is a random permutation of 0..N-1 — row uniqueness is
        // guaranteed by construction and never violated by a within-row swap.
        var row = new int[N];
        for (int r = 0; r < N; r++)
        {
            for (int i = 0; i < N; i++) row[i] = i;
            Shuffle(row);
            for (int c = 0; c < N; c++)
            {
                int v = row[c];
                _grid[r, c] = v;
                _colCount[c, v]++;
                _boxCount[Box(r, c), v]++;
            }
        }

        _cost = ComputeCost();
        _temp = T0;
    }

    // Run the SA loop. Returns a solved Grid (cost = 0), or null if MaxSteps
    // elapsed without converging.
    // onProgress(step, cost, temp) fires at most once per progressIntervalMs.
    public Grid? Run(Action<int, int, double>? onProgress = null,
                     int progressIntervalMs = 500)
    {
        long nextTick = 0;
        for (int step = 0; step < MaxSteps; step++)
        {
            if (_cost == 0) return Decode();

            Step();
            _temp *= Alpha;

            if (onProgress != null)
            {
                long now = Environment.TickCount64;
                if (now >= nextTick)
                {
                    onProgress(step, _cost, _temp);
                    nextTick = now + progressIntervalMs;
                }
            }
        }
        return _cost == 0 ? Decode() : null;
    }

    private void Step()
    {
        // Pick a random row and two distinct random column positions.
        int r  = _rng.Next(N);
        int c1 = _rng.Next(N);
        int c2 = _rng.Next(N - 1);
        if (c2 >= c1) c2++;

        int a   = _grid[r, c1]; // symbol leaving c1, entering c2
        int b   = _grid[r, c2]; // symbol leaving c2, entering c1
        int bx1 = Box(r, c1);
        int bx2 = Box(r, c2);

        // O(1) delta: only the two affected columns and (if different) two
        // affected boxes change occupancy; row counts are unchanged.
        int delta = ColDelta(c1, c2, a, b);
        if (bx1 != bx2) delta += BoxDelta(bx1, bx2, a, b);

        // Accept unconditionally if improving or neutral; accept worsening
        // moves with Boltzmann probability exp(-delta/T).
        if (delta <= 0 || _rng.NextDouble() < Math.Exp(-delta / _temp))
        {
            _grid[r, c1] = b; _grid[r, c2] = a;
            _colCount[c1, a]--; _colCount[c1, b]++;
            _colCount[c2, b]--; _colCount[c2, a]++;
            if (bx1 != bx2)
            {
                _boxCount[bx1, a]--; _boxCount[bx1, b]++;
                _boxCount[bx2, b]--; _boxCount[bx2, a]++;
            }
            _cost += delta;
        }
    }

    // ΔCost from swapping symbols a↔b between columns c1 and c2.
    // c1 loses a and gains b; c2 loses b and gains a.
    private int ColDelta(int c1, int c2, int a, int b) =>
        Inc(_colCount[c1, b]) + Dec(_colCount[c1, a]) +
        Inc(_colCount[c2, a]) + Dec(_colCount[c2, b]);

    // ΔCost from swapping symbols a↔b between boxes bx1 and bx2.
    private int BoxDelta(int bx1, int bx2, int a, int b) =>
        Inc(_boxCount[bx1, b]) + Dec(_boxCount[bx1, a]) +
        Inc(_boxCount[bx2, a]) + Dec(_boxCount[bx2, b]);

    // Change in max(0, count-1) when count increases by 1:
    //   count 0→1: no new conflict (+0);  count k→k+1 (k≥1): new conflict (+1)
    private static int Inc(int count) => count >= 1 ? 1 : 0;

    // Change in max(0, count-1) when count decreases by 1:
    //   count 1→0: no conflict removed (0);  count k→k-1 (k≥2): conflict removed (-1)
    private static int Dec(int count) => count >= 2 ? -1 : 0;

    private int ComputeCost()
    {
        int cost = 0;
        for (int i = 0; i < N; i++)
            for (int v = 0; v < N; v++)
            {
                cost += Math.Max(0, _colCount[i, v] - 1);
                cost += Math.Max(0, _boxCount[i, v] - 1);
            }
        return cost;
    }

    private Grid Decode()
    {
        var grid = new Grid();
        for (int r = 0; r < N; r++)
            for (int c = 0; c < N; c++)
                grid[r, c] = Symbols.ToChar(_grid[r, c]);
        return grid;
    }

    private static int Box(int r, int c) => (r / B) * B + (c / B);

    private void Shuffle(int[] arr)
    {
        for (int i = arr.Length - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
    }
}
