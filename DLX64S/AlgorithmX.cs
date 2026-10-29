namespace DLX64S;

internal static class AlgorithmX
{
    // Run Algorithm X on pool. Calls onSolution with a RowId snapshot for each exact
    // cover found. Stops after maxSolutions calls; pass int.MaxValue to find all.
    // Returns the number of solutions found.
    //
    // onProgress, if provided, is called at most once per progressIntervalMs with
    // (currentDepth, totalNodes) so callers can display live search telemetry.
    public static int Solve(NodePool pool, int maxSolutions, Action<int[]> onSolution,
                            Action<int, long>? onProgress = null,
                            int progressIntervalMs = 1000)
    {
        int found = 0;
        long nodes = 0;
        long nextTick = 0;
        var partial = new List<int>();
        Search();
        onProgress?.Invoke(partial.Count, nodes); // final tick — always fires once per Solve call
        return found;

        // Returns true to signal early termination (maxSolutions reached).
        bool Search()
        {
            nodes++;
            if (onProgress != null)
            {
                long now = Environment.TickCount64;
                if (now >= nextTick)
                {
                    onProgress(partial.Count, nodes);
                    nextTick = now + progressIntervalMs;
                }
            }

            if (pool[NodePool.Root].Right == NodePool.Root)
            {
                onSolution(partial.ToArray());
                return ++found >= maxSolutions;
            }

            int col = ChooseColumn();
            pool.Cover(col);

            for (int row = pool[col].Down; row != col; row = pool[row].Down)
            {
                partial.Add(pool[row].RowId);
                for (int node = pool[row].Right; node != row; node = pool[node].Right)
                    pool.Cover(pool[node].Column);

                if (Search()) return true; // early exit; pool intentionally left modified

                for (int node = pool[row].Left; node != row; node = pool[node].Left)
                    pool.Uncover(pool[node].Column);
                partial.RemoveAt(partial.Count - 1);
            }

            pool.Uncover(col);
            return false;
        }

        // MCV: choose the column with the fewest uncovered rows; ties go to lowest index.
        int ChooseColumn()
        {
            int best = pool[NodePool.Root].Right;
            for (int col = pool[best].Right; col != NodePool.Root; col = pool[col].Right)
                if (pool[col].Size < pool[best].Size)
                    best = col;
            return best;
        }
    }
}
