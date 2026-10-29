# Algorithm

## Exact Cover

DLX64S reduces 64×64 Sudoku generation to the **exact cover** problem:

> Given a universe *U* of elements and a collection *S* of subsets, find a sub-collection *S\** ⊆ *S* such that every element of *U* appears in exactly one member of *S\**.

A complete Sudoku solution is an exact cover over four constraint types — each must be satisfied exactly once.

## Encoding 64×64 Sudoku as Exact Cover

Let N = 64 (grid dimension) and B = 8 (box dimension, √N). Symbols are indexed 0–63, mapped to the Base64 alphabet on output.

### Candidates (rows of the constraint matrix)

Each *candidate* is a triple (r, c, v) — "place value v in cell (r, c)." There are N³ = 262,144 candidates.

### Constraints (columns of the constraint matrix)

| Constraint type | Meaning | Count | Column index range |
|-----------------|---------|-------|--------------------|
| Cell | Cell (r, c) contains exactly one value | N² = 4,096 | 0 – 4,095 |
| Row | Row r contains value v exactly once | N² = 4,096 | 4,096 – 8,191 |
| Column | Column c contains value v exactly once | N² = 4,096 | 8,192 – 12,287 |
| Box | Box b contains value v exactly once | N² = 4,096 | 12,288 – 16,383 |

**Total constraint columns: 16,384.**

Box index: b = (r / B) × B + (c / B), i.e., the box in row-major order among the 64 8×8 sub-grids.

### Matrix structure

Each candidate (r, c, v) sets exactly **4** ones in the constraint matrix:

| Column | Value |
|--------|-------|
| r × N + c | Cell (r, c) |
| 4096 + r × N + v | Row r, value v |
| 8192 + c × N + v | Column c, value v |
| 12288 + b × N + v | Box b, value v |

The matrix has 262,144 rows, 16,384 columns, and 1,048,576 ones — sparse at one part in 256.

## Algorithm X

Algorithm X (Knuth, 2000) solves exact cover by recursive search with backtracking:

```
function AlgorithmX(matrix):
    if matrix has no columns: record solution; return
    choose column c  // selection heuristic: minimum-count column (MCV)
    cover(c)
    for each row r in column c (in chosen order):
        add r to partial solution
        for each column j in row r (except c): cover(j)
        AlgorithmX(reduced matrix)
        for each column j in row r (in reverse): uncover(j)
        remove r from partial solution
    uncover(c)
```

**Cover(c):** remove column c from the header list; for each row in c, remove that row from every other column it appears in.  
**Uncover(c):** restore in the exact reverse order of cover — the linked structure makes this O(1) per node.

## Dancing Links

DLX is Knuth's node-level implementation of Algorithm X using **doubly-linked circular lists**.

Each "1" in the constraint matrix is a node with six fields:

| Field | Type | Meaning |
|-------|------|---------|
| `Left`, `Right` | Node | horizontal list within the row |
| `Up`, `Down` | Node | vertical list within the column |
| `Column` | ColumnHeader | points to the column header node |
| `RowId` | int | identifies the candidate (r, c, v) this node belongs to |

Column headers extend each vertical list and additionally store `Size` (number of uncovered ones in the column) and `Left`/`Right` links forming the active-column list rooted at a sentinel **root node**.

**Cover operation (O(ones in covered rows)):**

```
cover(c):
    unlink c from header list
    for each row r downward in c:
        for each node n rightward in r (except the c-column node):
            unlink n from its column list
            n.Column.Size--
```

**Uncover is the exact reverse**, restoring nodes in reverse order — no explicit undo stack needed.

### Memory

For the full 64×64 unconstrained problem:
- **1,048,576** interior nodes (one per matrix "1")
- **16,384** column header nodes
- **1** root node
- **≈ 8–10 bytes per node** (six pointer/int fields in a compact struct) → **~10 MB total**

All nodes are allocated once at startup from a flat array; no heap churn during search.

## Randomization

Pure Algorithm X with MCV always returns the same solution for a given matrix. To generate diverse grids:

1. When iterating over rows in a chosen column (the inner `for` loop), **shuffle the row list** using a seeded PRNG before iterating.
2. The MCV column selection is deterministic (it selects any minimum-size column — ties may also be broken randomly if desired).

Shuffling only the row order is sufficient; the solution space is fully explored in randomized order. A 64-bit seed (user-supplied or drawn from the system clock) makes runs reproducible.

## Complexity

The worst-case backtrack tree is exponential, but:

- **For generation from an empty grid**, the MCV heuristic keeps branching factors low: early in the search, any uncovered column still has 64 candidates; after ~64 placements in one row, row-value constraints are nearly exhausted, and the remaining candidates collapse quickly.
- **In practice**, DLX on 9×9 Sudoku (729 candidates, 324 constraints) runs in microseconds; 16×16 (4,096 candidates, 1,024 constraints) in milliseconds. The 64×64 case (262,144 candidates, 16,384 constraints) is expected to complete within seconds for generation runs, though no empirical data exists yet for this grid size.

The first milestone should include a `Measure-Command` timing baseline.
