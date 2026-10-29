# Solver spec

- Input: A 64x64 Sudoku puzzle.
- Logic Engine: State: 4,096 uint64 bitmasks (Candidate Masks).
  - Constraint Propagation: Immediately clear bits in row/column/box when a cell is fixed.
- Tiered Tactics (The Milestones):
  - Tier 1 (Singles): Naked Singles, Hidden Singles.
  - Tier 2 (Basic Elimination): Intersections (Pointing/Claiming), Naked Pairs/Triples.
  - Tier 3 (Advanced Patterns): X-Wings, Swordfish, XY-Wings.
  - Tier 4 (Chains/Search): Alternating Inference Chains (AIC), or a Human-like limited-depth backtracker.
- Output: Solved grid OR "Invalid" OR "Requires higher tier than allowed."
  - **Always include per-tier technique call counts in the output**, regardless of success or failure. The Digger depends on these counts to enforce the Target_Technique_Profile.

## Computation Time Constraint

Bounded computation time is the primary correctness requirement. Higher-tier techniques — especially AIC and backtracking — have worst-case exponential behavior. The Solver must respect a caller-supplied technique cap per tier and abort with "Requires higher tier than allowed" the moment any cap is exceeded. This makes the Solver a hard gate, not just a reporter.
