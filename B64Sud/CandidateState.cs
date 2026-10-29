namespace B64Sud;

using System.Numerics;

// 4,096 uint64 bitmasks — one per cell. Bit k is set when symbol k is still a candidate.
// All 64 bits set (ulong.MaxValue) = every symbol is a candidate (empty cell, not yet constrained).
// Exactly 1 bit set = cell is determined.
internal sealed class CandidateState
{
    private const int N = Grid.Size;      // 64
    private const int B = Grid.BlockSize; // 8

    private readonly ulong[] _masks = new ulong[N * N];
    private readonly bool[] _solved = new bool[N * N];

    private CandidateState() { }

    internal static CandidateState Initialize(Grid grid)
    {
        var state = new CandidateState();
        Array.Fill(state._masks, ulong.MaxValue);

        for (int r = 0; r < N; r++)
        for (int c = 0; c < N; c++)
        {
            char ch = grid[r, c];
            if (ch == Grid.Empty) continue;
            int idx = Symbols.ToIndex(ch);
            state._masks[r * N + c] = 1UL << idx;
            state._solved[r * N + c] = true;
        }

        return state;
    }

    // Propagate every given cell to its peers. Call once after Initialize.
    // Returns false if a contradiction is detected.
    internal bool PropagateGivens()
    {
        for (int i = 0; i < N * N; i++)
            if (_solved[i] && !Propagate(i / N, i % N))
                return false;
        return true;
    }

    // Clear this cell's fixed bit from all unsolved peers.
    // Returns false on contradiction (peer's mask reaches 0 or another solved peer has the same value).
    private bool Propagate(int row, int col)
    {
        ulong fixedBit = _masks[row * N + col];
        int boxR = (row / B) * B;
        int boxC = (col / B) * B;

        for (int c = 0; c < N; c++)
        {
            if (c != col && !ClearFromPeer(row, c, fixedBit)) return false;
        }
        for (int r = 0; r < N; r++)
        {
            if (r != row && !ClearFromPeer(r, col, fixedBit)) return false;
        }
        for (int r = boxR; r < boxR + B; r++)
        for (int c = boxC; c < boxC + B; c++)
        {
            if ((r != row || c != col) && !ClearFromPeer(r, c, fixedBit)) return false;
        }

        return true;
    }

    private bool ClearFromPeer(int row, int col, ulong fixedBit)
    {
        int i = row * N + col;
        if (_solved[i])
            return (_masks[i] & fixedBit) == 0; // same value in same unit = contradiction
        _masks[i] &= ~fixedBit;
        return _masks[i] != 0;
    }

    // Scan for unsolved cells with exactly 1 candidate; fix and propagate each.
    // Returns number of cells resolved this pass.
    internal int FindNakedSingles()
    {
        int count = 0;
        for (int r = 0; r < N; r++)
        for (int c = 0; c < N; c++)
        {
            int i = r * N + c;
            if (_solved[i]) continue;
            if (BitOperations.PopCount(_masks[i]) == 1)
            {
                FixCell(r, c, _masks[i]);
                count++;
            }
        }
        return count;
    }

    // For each unit (row/col/box), find values that appear in exactly one unsolved cell; fix them.
    // Returns number of cells resolved this pass.
    internal int FindHiddenSingles()
    {
        int count = 0;

        // Rows
        for (int r = 0; r < N; r++)
        for (int v = 0; v < N; v++)
        {
            ulong bit = 1UL << v;
            int foundCol = -1;
            bool unique = true;
            for (int c = 0; c < N; c++)
            {
                int i = r * N + c;
                if (_solved[i] || (_masks[i] & bit) == 0) continue;
                if (foundCol >= 0) { unique = false; break; }
                foundCol = c;
            }
            if (unique && foundCol >= 0) { FixCell(r, foundCol, bit); count++; }
        }

        // Cols
        for (int c = 0; c < N; c++)
        for (int v = 0; v < N; v++)
        {
            ulong bit = 1UL << v;
            int foundRow = -1;
            bool unique = true;
            for (int r = 0; r < N; r++)
            {
                int i = r * N + c;
                if (_solved[i] || (_masks[i] & bit) == 0) continue;
                if (foundRow >= 0) { unique = false; break; }
                foundRow = r;
            }
            if (unique && foundRow >= 0) { FixCell(foundRow, c, bit); count++; }
        }

        // Boxes
        for (int boxR = 0; boxR < N; boxR += B)
        for (int boxC = 0; boxC < N; boxC += B)
        for (int v = 0; v < N; v++)
        {
            ulong bit = 1UL << v;
            int foundR = -1, foundC = -1;
            bool unique = true;
            for (int r = boxR; r < boxR + B && unique; r++)
            for (int c = boxC; c < boxC + B && unique; c++)
            {
                int i = r * N + c;
                if (_solved[i] || (_masks[i] & bit) == 0) continue;
                if (foundR >= 0) { unique = false; }
                else { foundR = r; foundC = c; }
            }
            if (unique && foundR >= 0) { FixCell(foundR, foundC, bit); count++; }
        }

        return count;
    }

    // Pointing: if all candidates for V in a box share a row, eliminate V from that row outside the box.
    //           if all candidates for V in a box share a col, eliminate V from that col outside the box.
    // Claiming: if all candidates for V in a row share a box, eliminate V from that box outside the row.
    //           if all candidates for V in a col share a box, eliminate V from that box outside the col.
    // Returns total candidate bits cleared.
    internal int FindIntersections()
    {
        int eliminations = 0;

        // Pointing (box → line)
        for (int boxR = 0; boxR < N; boxR += B)
        for (int boxC = 0; boxC < N; boxC += B)
        for (int v = 0; v < N; v++)
        {
            ulong bit = 1UL << v;
            int lockedRow = -1, lockedCol = -1;
            bool sameRow = true, sameCol = true;

            for (int r = boxR; r < boxR + B; r++)
            for (int c = boxC; c < boxC + B; c++)
            {
                int i = r * N + c;
                if (_solved[i] || (_masks[i] & bit) == 0) continue;
                if (lockedRow < 0) { lockedRow = r; lockedCol = c; }
                else
                {
                    if (r != lockedRow) sameRow = false;
                    if (c != lockedCol) sameCol = false;
                }
            }

            if (lockedRow < 0) continue;

            if (sameRow)
                for (int c = 0; c < N; c++)
                {
                    if (c >= boxC && c < boxC + B) continue;
                    int i = lockedRow * N + c;
                    if (!_solved[i] && (_masks[i] & bit) != 0) { _masks[i] &= ~bit; eliminations++; }
                }

            if (sameCol)
                for (int r = 0; r < N; r++)
                {
                    if (r >= boxR && r < boxR + B) continue;
                    int i = r * N + lockedCol;
                    if (!_solved[i] && (_masks[i] & bit) != 0) { _masks[i] &= ~bit; eliminations++; }
                }
        }

        // Claiming rows (row → box)
        for (int r = 0; r < N; r++)
        for (int v = 0; v < N; v++)
        {
            ulong bit = 1UL << v;
            int lockedBoxC = -1;
            bool sameBox = true;

            for (int c = 0; c < N && sameBox; c++)
            {
                int i = r * N + c;
                if (_solved[i] || (_masks[i] & bit) == 0) continue;
                int bc = (c / B) * B;
                if (lockedBoxC < 0) lockedBoxC = bc;
                else if (bc != lockedBoxC) sameBox = false;
            }

            if (lockedBoxC < 0 || !sameBox) continue;
            int boxR = (r / B) * B;
            for (int r2 = boxR; r2 < boxR + B; r2++)
            {
                if (r2 == r) continue;
                for (int c = lockedBoxC; c < lockedBoxC + B; c++)
                {
                    int i = r2 * N + c;
                    if (!_solved[i] && (_masks[i] & bit) != 0) { _masks[i] &= ~bit; eliminations++; }
                }
            }
        }

        // Claiming cols (col → box)
        for (int c = 0; c < N; c++)
        for (int v = 0; v < N; v++)
        {
            ulong bit = 1UL << v;
            int lockedBoxR = -1;
            bool sameBox = true;

            for (int r = 0; r < N && sameBox; r++)
            {
                int i = r * N + c;
                if (_solved[i] || (_masks[i] & bit) == 0) continue;
                int br = (r / B) * B;
                if (lockedBoxR < 0) lockedBoxR = br;
                else if (br != lockedBoxR) sameBox = false;
            }

            if (lockedBoxR < 0 || !sameBox) continue;
            int boxC = (c / B) * B;
            for (int r = lockedBoxR; r < lockedBoxR + B; r++)
            for (int c2 = boxC; c2 < boxC + B; c2++)
            {
                if (c2 == c) continue;
                int i = r * N + c2;
                if (!_solved[i] && (_masks[i] & bit) != 0) { _masks[i] &= ~bit; eliminations++; }
            }
        }

        return eliminations;
    }

    // If two unsolved cells in the same unit share exactly the same two candidates,
    // those two values can be eliminated from all other unsolved cells in that unit.
    // Returns total candidate bits cleared.
    internal int FindNakedPairs()
    {
        int eliminations = 0;
        Span<int> pidx = stackalloc int[N];
        Span<ulong> pmask = stackalloc ulong[N];

        // Rows
        for (int r = 0; r < N; r++)
        {
            int pc = 0;
            for (int c = 0; c < N; c++)
            {
                int i = r * N + c;
                if (!_solved[i] && BitOperations.PopCount(_masks[i]) == 2)
                    { pidx[pc] = i; pmask[pc] = _masks[i]; pc++; }
            }
            for (int a = 0; a < pc; a++)
            for (int b = a + 1; b < pc; b++)
            {
                if (pmask[a] != pmask[b]) continue;
                ulong elim = pmask[a];
                for (int c = 0; c < N; c++)
                {
                    int i = r * N + c;
                    if (i == pidx[a] || i == pidx[b] || _solved[i]) continue;
                    if ((_masks[i] & elim) != 0) { _masks[i] &= ~elim; eliminations++; }
                }
            }
        }

        // Cols
        for (int c = 0; c < N; c++)
        {
            int pc = 0;
            for (int r = 0; r < N; r++)
            {
                int i = r * N + c;
                if (!_solved[i] && BitOperations.PopCount(_masks[i]) == 2)
                    { pidx[pc] = i; pmask[pc] = _masks[i]; pc++; }
            }
            for (int a = 0; a < pc; a++)
            for (int b = a + 1; b < pc; b++)
            {
                if (pmask[a] != pmask[b]) continue;
                ulong elim = pmask[a];
                for (int r = 0; r < N; r++)
                {
                    int i = r * N + c;
                    if (i == pidx[a] || i == pidx[b] || _solved[i]) continue;
                    if ((_masks[i] & elim) != 0) { _masks[i] &= ~elim; eliminations++; }
                }
            }
        }

        // Boxes
        for (int boxR = 0; boxR < N; boxR += B)
        for (int boxC = 0; boxC < N; boxC += B)
        {
            int pc = 0;
            for (int r = boxR; r < boxR + B; r++)
            for (int c = boxC; c < boxC + B; c++)
            {
                int i = r * N + c;
                if (!_solved[i] && BitOperations.PopCount(_masks[i]) == 2)
                    { pidx[pc] = i; pmask[pc] = _masks[i]; pc++; }
            }
            for (int a = 0; a < pc; a++)
            for (int b = a + 1; b < pc; b++)
            {
                if (pmask[a] != pmask[b]) continue;
                ulong elim = pmask[a];
                for (int r = boxR; r < boxR + B; r++)
                for (int c = boxC; c < boxC + B; c++)
                {
                    int i = r * N + c;
                    if (i == pidx[a] || i == pidx[b] || _solved[i]) continue;
                    if ((_masks[i] & elim) != 0) { _masks[i] &= ~elim; eliminations++; }
                }
            }
        }

        return eliminations;
    }

    // For value V, if it appears as a candidate in exactly 2 cells in each of 2 rows and those
    // cells share the same 2 columns, eliminate V from all other cells in those columns (and vice
    // versa for the column variant). Returns total candidate bits cleared.
    internal int FindXWings()
    {
        int eliminations = 0;
        Span<ulong> rowProfile = stackalloc ulong[N];
        Span<ulong> colProfile = stackalloc ulong[N];

        for (int v = 0; v < N; v++)
        {
            ulong bit = 1UL << v;

            // Row version: rowProfile[r] = bitmask of columns where V is a candidate in row r
            rowProfile.Clear();
            for (int r = 0; r < N; r++)
                for (int c = 0; c < N; c++)
                {
                    int i = r * N + c;
                    if (!_solved[i] && (_masks[i] & bit) != 0)
                        rowProfile[r] |= 1UL << c;
                }

            for (int r1 = 0; r1 < N; r1++)
            {
                if (BitOperations.PopCount(rowProfile[r1]) != 2) continue;
                for (int r2 = r1 + 1; r2 < N; r2++)
                {
                    if (rowProfile[r1] != rowProfile[r2]) continue;
                    ulong cols = rowProfile[r1];
                    while (cols != 0)
                    {
                        int c = BitOperations.TrailingZeroCount(cols);
                        cols &= cols - 1;
                        for (int r = 0; r < N; r++)
                        {
                            if (r == r1 || r == r2) continue;
                            int i = r * N + c;
                            if (!_solved[i] && (_masks[i] & bit) != 0)
                            { _masks[i] &= ~bit; eliminations++; }
                        }
                    }
                }
            }

            // Column version: colProfile[c] = bitmask of rows where V is a candidate in col c
            colProfile.Clear();
            for (int c = 0; c < N; c++)
                for (int r = 0; r < N; r++)
                {
                    int i = r * N + c;
                    if (!_solved[i] && (_masks[i] & bit) != 0)
                        colProfile[c] |= 1UL << r;
                }

            for (int c1 = 0; c1 < N; c1++)
            {
                if (BitOperations.PopCount(colProfile[c1]) != 2) continue;
                for (int c2 = c1 + 1; c2 < N; c2++)
                {
                    if (colProfile[c1] != colProfile[c2]) continue;
                    ulong rows = colProfile[c1];
                    while (rows != 0)
                    {
                        int r = BitOperations.TrailingZeroCount(rows);
                        rows &= rows - 1;
                        for (int c = 0; c < N; c++)
                        {
                            if (c == c1 || c == c2) continue;
                            int i = r * N + c;
                            if (!_solved[i] && (_masks[i] & bit) != 0)
                            { _masks[i] &= ~bit; eliminations++; }
                        }
                    }
                }
            }
        }

        return eliminations;
    }

    // Generalization of X-Wing to 3 base lines: for value V, if it appears in 2–3 cells in each
    // of 3 rows and the union of those columns has exactly 3 members, eliminate V from those
    // columns outside the 3 rows (and vice versa for the column variant).
    // Returns total candidate bits cleared.
    internal int FindSwordfish()
    {
        int eliminations = 0;
        Span<ulong> rowProfile = stackalloc ulong[N];
        Span<ulong> colProfile = stackalloc ulong[N];

        for (int v = 0; v < N; v++)
        {
            ulong bit = 1UL << v;

            // Row version
            rowProfile.Clear();
            for (int r = 0; r < N; r++)
                for (int c = 0; c < N; c++)
                {
                    int i = r * N + c;
                    if (!_solved[i] && (_masks[i] & bit) != 0)
                        rowProfile[r] |= 1UL << c;
                }

            for (int r1 = 0; r1 < N; r1++)
            {
                int cnt1 = BitOperations.PopCount(rowProfile[r1]);
                if (cnt1 < 2 || cnt1 > 3) continue;
                for (int r2 = r1 + 1; r2 < N; r2++)
                {
                    int cnt2 = BitOperations.PopCount(rowProfile[r2]);
                    if (cnt2 < 2 || cnt2 > 3) continue;
                    ulong union12 = rowProfile[r1] | rowProfile[r2];
                    if (BitOperations.PopCount(union12) > 3) continue;
                    for (int r3 = r2 + 1; r3 < N; r3++)
                    {
                        int cnt3 = BitOperations.PopCount(rowProfile[r3]);
                        if (cnt3 < 2 || cnt3 > 3) continue;
                        ulong union = union12 | rowProfile[r3];
                        if (BitOperations.PopCount(union) != 3) continue;
                        ulong cols = union;
                        while (cols != 0)
                        {
                            int c = BitOperations.TrailingZeroCount(cols);
                            cols &= cols - 1;
                            for (int r = 0; r < N; r++)
                            {
                                if (r == r1 || r == r2 || r == r3) continue;
                                int i = r * N + c;
                                if (!_solved[i] && (_masks[i] & bit) != 0)
                                { _masks[i] &= ~bit; eliminations++; }
                            }
                        }
                    }
                }
            }

            // Column version
            colProfile.Clear();
            for (int c = 0; c < N; c++)
                for (int r = 0; r < N; r++)
                {
                    int i = r * N + c;
                    if (!_solved[i] && (_masks[i] & bit) != 0)
                        colProfile[c] |= 1UL << r;
                }

            for (int c1 = 0; c1 < N; c1++)
            {
                int cnt1 = BitOperations.PopCount(colProfile[c1]);
                if (cnt1 < 2 || cnt1 > 3) continue;
                for (int c2 = c1 + 1; c2 < N; c2++)
                {
                    int cnt2 = BitOperations.PopCount(colProfile[c2]);
                    if (cnt2 < 2 || cnt2 > 3) continue;
                    ulong union12 = colProfile[c1] | colProfile[c2];
                    if (BitOperations.PopCount(union12) > 3) continue;
                    for (int c3 = c2 + 1; c3 < N; c3++)
                    {
                        int cnt3 = BitOperations.PopCount(colProfile[c3]);
                        if (cnt3 < 2 || cnt3 > 3) continue;
                        ulong union = union12 | colProfile[c3];
                        if (BitOperations.PopCount(union) != 3) continue;
                        ulong rows = union;
                        while (rows != 0)
                        {
                            int r = BitOperations.TrailingZeroCount(rows);
                            rows &= rows - 1;
                            for (int c = 0; c < N; c++)
                            {
                                if (c == c1 || c == c2 || c == c3) continue;
                                int i = r * N + c;
                                if (!_solved[i] && (_masks[i] & bit) != 0)
                                { _masks[i] &= ~bit; eliminations++; }
                            }
                        }
                    }
                }
            }
        }

        return eliminations;
    }

    // Pivot cell P has candidates {A,B}. Wing W1 (peer of P) has {A,C}. Wing W2 (peer of P) has
    // {B,C}. Any cell visible to both W1 and W2 can have C eliminated.
    // Returns total candidate bits cleared.
    internal int FindXYWings()
    {
        int eliminations = 0;

        Span<int> twoIdx = stackalloc int[N * N];
        int twoCount = 0;
        for (int i = 0; i < N * N; i++)
            if (!_solved[i] && BitOperations.PopCount(_masks[i]) == 2)
                twoIdx[twoCount++] = i;

        for (int pi = 0; pi < twoCount; pi++)
        {
            int pidx = twoIdx[pi];
            int pr = pidx / N, pc = pidx % N;
            ulong pMask = _masks[pidx];

            for (int w1i = 0; w1i < twoCount; w1i++)
            {
                if (w1i == pi) continue;
                int w1idx = twoIdx[w1i];
                int w1r = w1idx / N, w1c = w1idx % N;
                if (!SharesUnit(pr, pc, w1r, w1c)) continue;
                ulong shared = _masks[w1idx] & pMask;
                if (BitOperations.PopCount(shared) != 1) continue;
                ulong cBit = _masks[w1idx] ^ shared;
                ulong w2Needed = (pMask ^ shared) | cBit;

                for (int w2i = 0; w2i < twoCount; w2i++)
                {
                    if (w2i == pi || w2i == w1i) continue;
                    int w2idx = twoIdx[w2i];
                    int w2r = w2idx / N, w2c = w2idx % N;
                    if (!SharesUnit(pr, pc, w2r, w2c)) continue;
                    if (_masks[w2idx] != w2Needed) continue;

                    // Eliminate cBit from cells visible to both W1 and W2.
                    // Iterate units of W1; skip cells also in W1's row/col to avoid double-visits.

                    // W1's row
                    for (int c = 0; c < N; c++)
                    {
                        int i = w1r * N + c;
                        if (i == w1idx || i == w2idx || _solved[i]) continue;
                        if ((_masks[i] & cBit) == 0) continue;
                        if (SharesUnit(w2r, w2c, w1r, c))
                        { _masks[i] &= ~cBit; eliminations++; }
                    }
                    // W1's col, excluding W1's own row
                    for (int r = 0; r < N; r++)
                    {
                        if (r == w1r) continue;
                        int i = r * N + w1c;
                        if (i == w2idx || _solved[i]) continue;
                        if ((_masks[i] & cBit) == 0) continue;
                        if (SharesUnit(w2r, w2c, r, w1c))
                        { _masks[i] &= ~cBit; eliminations++; }
                    }
                    // W1's box, excluding cells already covered by W1's row or col
                    int boxR = (w1r / B) * B, boxC = (w1c / B) * B;
                    for (int r = boxR; r < boxR + B; r++)
                    {
                        if (r == w1r) continue;
                        for (int c = boxC; c < boxC + B; c++)
                        {
                            if (c == w1c) continue;
                            int i = r * N + c;
                            if (i == w2idx || _solved[i]) continue;
                            if ((_masks[i] & cBit) == 0) continue;
                            if (SharesUnit(w2r, w2c, r, c))
                            { _masks[i] &= ~cBit; eliminations++; }
                        }
                    }
                }
            }
        }

        return eliminations;
    }

    private static bool SharesUnit(int r1, int c1, int r2, int c2) =>
        r1 == r2 || c1 == c2 || (r1 / B == r2 / B && c1 / B == c2 / B);

    internal CandidateState Clone()
    {
        var c = new CandidateState();
        Array.Copy(_masks, c._masks, N * N);
        Array.Copy(_solved, c._solved, N * N);
        return c;
    }

    internal ulong GetCandidates(int idx) => _masks[idx];

    // Assume a specific candidate value for a cell and propagate. Contradiction is detectable
    // afterward via IsValid(). Caller must ensure bit is currently set in the cell's mask.
    internal void AssumeCell(int row, int col, ulong bit) => FixCell(row, col, bit);

    // Returns the flat index of the unsolved cell with fewest candidates (MRV heuristic),
    // or -1 if all cells are solved.
    internal int FindMRVCell()
    {
        int best = -1, bestCount = int.MaxValue;
        for (int i = 0; i < N * N; i++)
        {
            if (_solved[i]) continue;
            int cnt = BitOperations.PopCount(_masks[i]);
            if (cnt >= bestCount) continue;
            bestCount = cnt;
            best = i;
            if (bestCount == 2) break; // 1 would be a naked single; 2 is the practical minimum
        }
        return best;
    }

    private void FixCell(int row, int col, ulong bit)
    {
        _masks[row * N + col] = bit;
        _solved[row * N + col] = true;
        Propagate(row, col);
    }

    internal bool IsComplete()
    {
        for (int i = 0; i < N * N; i++)
            if (!_solved[i]) return false;
        return true;
    }

    internal bool IsValid()
    {
        for (int i = 0; i < N * N; i++)
            if (_masks[i] == 0) return false;
        return true;
    }

    internal Grid ToGrid()
    {
        var grid = new Grid();
        for (int r = 0; r < N; r++)
        for (int c = 0; c < N; c++)
            grid[r, c] = Symbols.ToChar(BitOperations.TrailingZeroCount(_masks[r * N + c]));
        return grid;
    }
}
