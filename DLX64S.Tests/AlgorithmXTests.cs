namespace DLX64S.Tests;

[LogStart]
public sealed class AlgorithmXTests
{
    // -----------------------------------------------------------------------
    // Knuth's 7-column, 6-row example (DLX paper, 2000)
    // Universe: {1,2,3,4,5,6,7}
    // Sets: A={1,4,7}  B={1,4}  C={4,5,7}  D={3,5,6}  E={2,3,6,7}  F={2,7}
    // Unique solution: B(1) + D(3) + F(5)
    // -----------------------------------------------------------------------
    private static NodePool BuildKnuth7x6()
    {
        var pool = new NodePool(capacity: 64);
        int c1 = pool.AllocHeader(), c2 = pool.AllocHeader(), c3 = pool.AllocHeader(),
            c4 = pool.AllocHeader(), c5 = pool.AllocHeader(), c6 = pool.AllocHeader(),
            c7 = pool.AllocHeader();

        pool.AddRow(rowId: 0, c1, c4, c7);             // A
        pool.AddRow(rowId: 1, c1, c4);                  // B
        pool.AddRow(rowId: 2, c4, c5, c7);              // C
        pool.AddRow(rowId: 3, c3, c5, c6);              // D
        pool.AddRow(rowId: 4, c2, c3, c6, c7);          // E
        pool.AddRow(rowId: 5, c2, c7);                  // F
        return pool;
    }

    [Fact]
    public void Knuth7x6_FindsUniqueSolution()
    {
        var pool = BuildKnuth7x6();
        var solutions = new List<int[]>();

        int count = AlgorithmX.Solve(pool, maxSolutions: 2, solutions.Add);

        Assert.Equal(1, count);
        Assert.Equal([1, 3, 5], solutions[0].OrderBy(x => x).ToArray()); // B, D, F
    }

    // -----------------------------------------------------------------------
    // 3-rooks on 3×3: one rook per row, one per column
    // Universe: {R0,R1,R2,C0,C1,C2}  —  6 columns
    // 9 candidates: rowId = r*3+c
    // Known total: 3! = 6 solutions
    // -----------------------------------------------------------------------
    private static NodePool BuildRooks3x3()
    {
        var pool = new NodePool(capacity: 64);
        int r0 = pool.AllocHeader(), r1 = pool.AllocHeader(), r2 = pool.AllocHeader();
        int c0 = pool.AllocHeader(), c1 = pool.AllocHeader(), c2 = pool.AllocHeader();

        int[] rows = [r0, r1, r2];
        int[] cols = [c0, c1, c2];
        for (int r = 0; r < 3; r++)
        for (int c = 0; c < 3; c++)
            pool.AddRow(rowId: r * 3 + c, rows[r], cols[c]);

        return pool;
    }

    [Fact]
    public void Rooks3x3_FindsAllSixSolutions()
    {
        var pool = BuildRooks3x3();
        var solutions = new List<int[]>();

        int count = AlgorithmX.Solve(pool, maxSolutions: 7, solutions.Add);

        Assert.Equal(6, count); // exhausted after exactly 6; asking for 7 finds no more
    }

    [Fact]
    public void Rooks3x3_AllSolutionsAreValidAndDistinct()
    {
        var pool = BuildRooks3x3();
        var solutions = new List<int[]>();
        AlgorithmX.Solve(pool, maxSolutions: 6, solutions.Add);

        foreach (var sol in solutions)
        {
            Assert.Equal(3, sol.Select(id => id / 3).Distinct().Count()); // one per row
            Assert.Equal(3, sol.Select(id => id % 3).Distinct().Count()); // one per col
        }

        // All six are distinct sets
        var sets = solutions.Select(s => new HashSet<int>(s)).ToList();
        for (int i = 0; i < sets.Count; i++)
        for (int j = i + 1; j < sets.Count; j++)
            Assert.False(sets[i].SetEquals(sets[j]));
    }

    // -----------------------------------------------------------------------
    // 8-rooks on 8×8: rowId = r*8+c  —  16 columns, 64 candidates
    // 8! = 40,320 solutions; only one is requested (deterministic order)
    // -----------------------------------------------------------------------
    private static NodePool BuildRooks8x8()
    {
        var pool = new NodePool(capacity: 256);
        var rowHdrs = Enumerable.Range(0, 8).Select(_ => pool.AllocHeader()).ToArray();
        var colHdrs = Enumerable.Range(0, 8).Select(_ => pool.AllocHeader()).ToArray();

        for (int r = 0; r < 8; r++)
        for (int c = 0; c < 8; c++)
            pool.AddRow(rowId: r * 8 + c, rowHdrs[r], colHdrs[c]);

        return pool;
    }

    [Fact]
    public void Rooks8x8_SingleSolutionIsValid()
    {
        var pool = BuildRooks8x8();
        var solutions = new List<int[]>();

        int count = AlgorithmX.Solve(pool, maxSolutions: 1, solutions.Add);

        Assert.Equal(1, count);
        var sol = solutions[0];
        Assert.Equal(8, sol.Select(id => id / 8).Distinct().Count()); // one per row
        Assert.Equal(8, sol.Select(id => id % 8).Distinct().Count()); // one per col
    }

    // -----------------------------------------------------------------------
    // 64-rooks on 64×64: rowId = r*64+c  —  128 columns, 4096 candidates
    // Exercises NodePool and AlgorithmX at the same header-list scale as the
    // 64×64 Sudoku row/column constraint blocks (each has 64 headers per symbol).
    // Solutions: 64! — only one or two are requested.
    // -----------------------------------------------------------------------
    private static NodePool BuildRooks64x64()
    {
        // 128 column headers + 4096 candidates × 2 nodes each = 8320 non-root nodes
        var pool = new NodePool(capacity: 8320);
        var rowHdrs = Enumerable.Range(0, 64).Select(_ => pool.AllocHeader()).ToArray();
        var colHdrs = Enumerable.Range(0, 64).Select(_ => pool.AllocHeader()).ToArray();

        for (int r = 0; r < 64; r++)
        for (int c = 0; c < 64; c++)
            pool.AddRow(rowId: r * 64 + c, rowHdrs[r], colHdrs[c]);

        return pool;
    }

    [Fact]
    public void Rooks64x64_SingleSolutionIsValid()
    {
        var pool = BuildRooks64x64();
        var solutions = new List<int[]>();

        int count = AlgorithmX.Solve(pool, maxSolutions: 1, solutions.Add);

        Assert.Equal(1, count);
        var sol = solutions[0];
        Assert.Equal(64, sol.Select(id => id / 64).Distinct().Count()); // one per row
        Assert.Equal(64, sol.Select(id => id % 64).Distinct().Count()); // one per col
    }

    [Fact]
    public void Rooks64x64_TwoSolutionsAreValidAndDistinct()
    {
        var pool = BuildRooks64x64();
        var solutions = new List<int[]>();

        int count = AlgorithmX.Solve(pool, maxSolutions: 2, solutions.Add);

        Assert.Equal(2, count);
        foreach (var sol in solutions)
        {
            Assert.Equal(64, sol.Select(id => id / 64).Distinct().Count());
            Assert.Equal(64, sol.Select(id => id % 64).Distinct().Count());
        }
        Assert.False(new HashSet<int>(solutions[0]).SetEquals(solutions[1]));
    }

    // -----------------------------------------------------------------------
    // 64-rooks with 9 rooks pre-placed at prime-numbered coordinates:
    //   (row 2, col 3), (row 5, col 7), (row 11, col 13), (row 17, col 19),
    //   (row 23, col 29), (row 31, col 37), (row 41, col 43),
    //   (row 47, col 53), (row 59, col 61).
    // Pre-covering those row+col headers constrains the search to 55 rooks;
    // combined with the 9 fixed, every row and column must be covered once.
    // -----------------------------------------------------------------------
    [Fact]
    public void Rooks64x64_NinePrimePreplacedRooksCompleteToFullSolution()
    {
        (int Row, int Col)[] prePlaced =
        [
            ( 2,  3), ( 5,  7), (11, 13), (17, 19), (23, 29),
            (31, 37), (41, 43), (47, 53), (59, 61)
        ];

        var pool = new NodePool(capacity: 8320);
        var rowHdrs = Enumerable.Range(0, 64).Select(_ => pool.AllocHeader()).ToArray();
        var colHdrs = Enumerable.Range(0, 64).Select(_ => pool.AllocHeader()).ToArray();

        for (int r = 0; r < 64; r++)
        for (int c = 0; c < 64; c++)
            pool.AddRow(rowId: r * 64 + c, rowHdrs[r], colHdrs[c]);

        foreach (var (r, c) in prePlaced)
        {
            pool.Cover(rowHdrs[r]);
            pool.Cover(colHdrs[c]);
        }

        var solutions = new List<int[]>();
        int count = AlgorithmX.Solve(pool, maxSolutions: 1, solutions.Add);

        Assert.Equal(1, count);

        var allPlacements = prePlaced
            .Select(p => p.Row * 64 + p.Col)
            .Concat(solutions[0])
            .ToArray();

        Assert.Equal(64, allPlacements.Length);
        Assert.Equal(64, allPlacements.Select(id => id / 64).Distinct().Count()); // one per row
        Assert.Equal(64, allPlacements.Select(id => id % 64).Distinct().Count()); // one per col
    }

    // -----------------------------------------------------------------------
    // Infeasibility: universe {1,2,3}, rows [{1,2},{2,3}]
    // No subset covers all three without double-covering 2.
    // -----------------------------------------------------------------------
    [Fact]
    public void Infeasible_FindsNoSolutions()
    {
        var pool = new NodePool(capacity: 16);
        int x1 = pool.AllocHeader(), x2 = pool.AllocHeader(), x3 = pool.AllocHeader();
        pool.AddRow(rowId: 0, x1, x2); // covers 1 and 2
        pool.AddRow(rowId: 1, x2, x3); // covers 2 and 3

        int count = AlgorithmX.Solve(pool, maxSolutions: 1, _ => { });

        Assert.Equal(0, count);
    }
}
