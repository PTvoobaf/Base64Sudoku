namespace DLX64S;

internal static class SudokuMatrix
{
    private const int N  = Grid.Size;      // 64
    private const int B  = Grid.BlockSize; // 8
    private const int N2 = N * N;          // 4096

    // 16,384 column headers + 262,144 candidates × 4 nodes each
    public const int PoolCapacity = N2 * 4 + N * N2 * 4;

    // Build the constraint matrix with candidates added in a shuffled order,
    // then pre-fill the first two bands (rows 0..2B-1 = rows 0..15) by
    // covering the 1024 constraint sets they satisfy.  AlgorithmX fills only
    // rows 2B..N-1 (48 rows × 64 columns = 3072 remaining cells).
    //
    // Returns the pool (ready for AlgorithmX) and PreFilled[r, c] = symbol
    // index for r < 2*B, so the caller can reconstruct both pre-filled bands.
    public static (NodePool Pool, int[,] PreFilled) Build(Random rng)
    {
        var pool = new NodePool(PoolCapacity);

        // Allocate all 16,384 column headers.
        // Layout: [0..N2-1] cell(r,c)=r*N+c  [N2..2N2-1] row-val(r,v)=N2+r*N+v
        //         [2N2..3N2-1] col-val(c,v)=2N2+c*N+v  [3N2..4N2-1] box-val(bx,v)=3N2+bx*N+v
        var h = new int[N2 * 4];
        for (int i = 0; i < h.Length; i++)
            h[i] = pool.AllocHeader();

        // Shuffle and add all 262,144 candidates so AlgorithmX explores in a
        // random order that varies with the seed.
        var cands = new int[N * N2];
        for (int i = 0; i < cands.Length; i++) cands[i] = i;
        Shuffle(cands, rng);

        foreach (int cand in cands)
        {
            int r  = cand / N2;
            int c  = (cand / N) % N;
            int v  = cand % N;
            int bx = (r / B) * B + (c / B);
            pool.AddRow(
                rowId: cand,
                h[r * N + c],
                h[N2  + r * N + v],
                h[2*N2 + c * N + v],
                h[3*N2 + bx * N + v]
            );
        }

        var preFilled = new int[2 * B, N];

        // --- Band 0 (rows 0..B-1) ---
        // Shuffled cyclic Latin square: group(r, b) = (rowPerm0[r] + colPerm0[b]) % B
        // assigns one symbol-group to each (row, box-stack) slot.  Independent
        // B-permutations pi0[group, box, offset] distribute the 8 symbols of each
        // group within each box-stack column.
        var rowPerm0 = new int[B]; for (int i = 0; i < B; i++) rowPerm0[i] = i;
        var colPerm0 = new int[B]; for (int i = 0; i < B; i++) colPerm0[i] = i;
        Shuffle(rowPerm0, rng);
        Shuffle(colPerm0, rng);

        var pi0 = new int[B, B, B];
        for (int p = 0; p < B; p++)
            for (int b = 0; b < B; b++)
            {
                var perm = new int[B]; for (int i = 0; i < B; i++) perm[i] = i;
                Shuffle(perm, rng);
                for (int k = 0; k < B; k++) pi0[p, b, k] = perm[k];
            }

        for (int r = 0; r < B; r++)
            for (int b = 0; b < B; b++)
            {
                int group = (rowPerm0[r] + colPerm0[b]) % B;
                for (int k = 0; k < B; k++)
                    preFilled[r, b * B + k] = group * B + pi0[group, b, k];
            }

        // --- Band 1 (rows B..2B-1) ---
        // Same cyclic structure with fresh (rowPerm1, colPerm1) permutations.
        // pi1[g, b, k] = pi0[g, b, (k + shift[g,b]) % B] where shift ∈ [1, B-1].
        // A non-zero cyclic rotation of a permutation is always a derangement:
        // pi1[g, b, k] ≠ pi0[g, b, k] for every k, so band 1 never places the
        // same symbol as band 0 in any column (column-uniqueness guaranteed).
        var rowPerm1 = new int[B]; for (int i = 0; i < B; i++) rowPerm1[i] = i;
        var colPerm1 = new int[B]; for (int i = 0; i < B; i++) colPerm1[i] = i;
        Shuffle(rowPerm1, rng);
        Shuffle(colPerm1, rng);

        var pi1 = new int[B, B, B];
        for (int p = 0; p < B; p++)
            for (int b = 0; b < B; b++)
            {
                int shift = 1 + rng.Next(B - 1); // [1, B-1]
                for (int k = 0; k < B; k++)
                    pi1[p, b, k] = pi0[p, b, (k + shift) % B];
            }

        for (int r = 0; r < B; r++)
            for (int b = 0; b < B; b++)
            {
                int group = (rowPerm1[r] + colPerm1[b]) % B;
                for (int k = 0; k < B; k++)
                    preFilled[B + r, b * B + k] = group * B + pi1[group, b, k];
            }

        // Cover constraints for both pre-filled bands.
        for (int band = 0; band < 2; band++)
            for (int r = 0; r < B; r++)
                for (int c = 0; c < N; c++)
                {
                    int row = band * B + r;
                    int v   = preFilled[row, c];
                    int bx  = band * B + c / B; // (row / B) * B + (c / B)
                    pool.Cover(h[row * N + c]);
                    pool.Cover(h[N2  + row * N + v]);
                    pool.Cover(h[2*N2 + c * N + v]);
                    pool.Cover(h[3*N2 + bx * N + v]);
                }

        return (pool, preFilled);
    }

    private static void Shuffle(int[] arr, Random rng)
    {
        for (int i = arr.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
    }
}
