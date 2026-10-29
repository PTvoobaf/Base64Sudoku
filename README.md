# SAINTCON26 Hackers Challenge

## Base64 Sudoku

Here's a copy of the CTF instructions for the first of the four "Base64 Sudoku" challenges:

> 64×64 Sudoku, using the Base64 symbol set: ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/

> Sudoku rules: Each row contains each symbol exactly once. Each column contains each symbol exactly once. Each 8×8 box contains each symbol exactly once.

> Completely solve at least the first row of the attached puzzle. Submit the 64 symbols of that row in left-to-right order, using our Hacker's Challenge flag format:

> HC{ZYXWVUTSRQPONMLKJIHGFEDCBA+zyxwvutsrqponmlkjihgfedcba/987654321}

> (That is not the correct flag, but the correct flag follows the same pattern.)

The puzzle generators are C# .NET console apps, developed incrementally using a SPEC-TODO-CODE methodology with milestones. Claude Haiku/Sonnet and ChatGPT 5.x helped write and test code, and so did a local AI tool with Mistral and gemma4 LLMs.

The `DLX64S*` folders contain code to generate a completely solved puzzle.

- The DLX64S name is misleading -- an LLM originally suggested that a "Dancing Links" DLX algorithm would be the best puzzle creation tool. It wasn't. A "Simulated Annealing" algorithm worked much better.

The `B64SUD*` subfolders contain code for a puzzle-solver and a puzzle-creator.

- The algorithm starts with a completely solved puzzle, then "digs" blank spaces into it, verifying along the way that blanked spaces do not admit an alternate solution.
- The algorithm also ensures that a "digged" puzzle satisfies one of four difficulty tiers:

  1. (Singles) The entire puzzle can be solved using "Naked Singles" and "Hidden Singles" logical deduction tactics.
  2. (Elimination) The puzzle might also require Intersections (point/claim) and Naked Pairs/Triples tactics.
  3. (Advanced) The puzzle might also require X-Wings, XY-Wings, and/or Swordfish tactics.
  4. (Recursion) The puzzle might also require guessing and backtracking.
