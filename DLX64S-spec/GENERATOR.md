# Generator Design

## Responsibility

The generator builds the DLX constraint matrix, runs Algorithm X with randomized row order, and serializes the resulting complete grid. It takes no puzzle input — the output is always a fresh, valid solution.

## Command-Line Interface

```
dlx64s [--seed <N>] [--count <N>] [--out <file>]
```

| Flag | Default | Meaning |
|------|---------|---------|
| `--seed <N>` | system clock (nanoseconds) | 64-bit integer seed for the PRNG; emit seed to stderr so runs are reproducible |
| `--count <N>` | 1 | number of distinct solutions to generate (each uses an independent shuffled search) |
| `--out <file>` | stdout | write output to file instead of stdout |

Seed is always printed to stderr regardless of whether it was supplied, so a lucky run can be replayed.

## Output Format

One complete grid per solution, in the B64Sud 71×71 ASCII layout (see `FORMAT.md`). Multiple solutions (with `--count > 1`) are separated by a blank line.

The grid is valid by construction — no post-generation verification is needed.

## Internal Pipeline

```
1. Build DLX matrix
   a. Allocate node pool (flat array, ~1.07 M nodes for 64×64)
   b. Link column headers into circular header list rooted at sentinel
   c. For each candidate (r, c, v): allocate 4 nodes, link into
      cell / row / col / box column lists; append to row's horizontal list

2. Seed PRNG (--seed value or clock)

3. Run Algorithm X
   a. MCV: choose column c with minimum Size
   b. Shuffle column c's row list using PRNG
   c. For each row in shuffled order: cover, recurse, uncover
   d. When partial solution length == 4096: emit and decrement --count;
      if count == 0, terminate search early

4. Translate solution
   Each chosen candidate row carries a RowId encoding (r, c, v).
   Decode: r = rowId / (N*N), c = (rowId / N) % N, v = rowId % N
   Fill a [64][64] value array.

5. Serialize
   Map value 0–63 → Base64 char. Lay out 71×71 ASCII with space gutters.
   Emit to stdout or --out file.
```

## Node Layout

A compact struct minimizes cache pressure during search:

```
struct Node {
    int Left, Right;   // horizontal list (indices into node pool)
    int Up, Down;      // vertical list (indices into node pool)
    int Column;        // index of column header node
    int RowId;         // candidate index: r*N*N + c*N + v  (−1 for headers)
}
```

Using indices rather than pointers reduces memory to ~24 bytes/node × 1.07 M nodes ≈ 26 MB, avoids GC pressure in managed languages, and makes serialization and cloning straightforward.

Column header nodes are stored in the same pool at a reserved prefix (indices 0–16,384); the root sentinel is index 0. Interior nodes start at index 16,385.

## Randomization Strategy

Row shuffling is performed once per column selection, using a **Fisher-Yates shuffle** over a local copy of the column's row list. The shuffled list is iterated without further allocation.

Only the row iteration order is randomized; the MCV column selection is deterministic (ties broken by column index, lowest first). This is sufficient to produce statistically diverse grids.

## Early Termination

When `--count` solutions have been found, the search unwinds immediately. For `--count 1`, this means the very first complete assignment found is returned — no further tree exploration occurs.

For `--count > 1`, the search continues from where it left off after each solution, naturally exploring the next branch. Solutions are independent (different cells are resolved in different orders at different branch points), so diversity is not guaranteed but is expected in practice.

## Performance Notes

- **Matrix build:** O(N³) = O(262,144) operations — negligible.
- **Search:** empirical timing baseline to be established at Milestone 1. Expected order of magnitude: seconds, not minutes, for a single solution. No prior measurements exist for 64×64 DLX generation.
- **Memory:** ~26 MB for the node pool (fixed at startup). No dynamic allocation during search.
- The PRNG is the sole source of non-determinism; seed logging ensures any result is reproducible.

## Integration with B64Sud

```powershell
# Generate a new solved grid and dig a puzzle from it
dlx64s --seed 12345 > new_solution.txt
dotnet run --project B64Sud -- --dig new_solution.txt 4095 --tier2cap 4094 --tier3cap 4093 --tier4cap 2 > new_puzzle.txt
dotnet run --project B64Sud -- --solve new_puzzle.txt
```

The only requirement is that `dlx64s` output conforms to `FORMAT.md` — the Digger and Solver are format-agnostic with respect to which values fill the grid.
