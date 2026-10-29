# Digger Spec

- Inputs:
  1. A fully solved 64x64 Sudoku grid (ref: FORMAT.md; use `solution.txt` as the reference fixture)
  2. A Target_Technique_Profile (e.g., "2048 empty cells, Max 5 Tier-2 calls, 0 Tier-3 calls")
- Repeated Process (until desired empty cell count threshold reached):
  1. Select a cell at random (or in a symmetrical pattern)
  2. Remove the value
  3. Pass the resulting grid and the Target_Technique_Profile caps to the Solver
  4. If Solver reports a unique solution AND the returned per-tier call counts fit within Target_Technique_Profile, commit the removal
  5. If not, backtrack (re-insert value) and try another cell
- Output: A Base64 Sudoku puzzle file (ref: FORMAT.md)

## Computation Time Constraint

**This is the most important Digger/Solver constraint.** The purpose of the Target_Technique_Profile is to guarantee that every produced puzzle can be solved within bounded computation time. Puzzles that require excessive higher-tier technique applications — or any uncapped backtracking — are rejected by design. The Digger relies on the Solver's per-tier call counts (returned on every invocation) to enforce these caps at commit time. A puzzle that slips past these bounds could require impractical or effectively infinite solver time.
