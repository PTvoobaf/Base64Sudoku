# DLX64S — Overview

## What This Project Is

DLX64S uses Knuth's **Dancing Links** (DLX) implementation of **Algorithm X** to generate valid, fully-solved 64×64 Base64 Sudoku grids. Its primary output is a complete solution file in the same plaintext format used by B64Sud — making it a drop-in grid source for the B64Sud Digger.

## Motivation

B64Sud's Digger is currently limited to `solution.txt`, a single canonical solved grid. All dug puzzles share the same underlying solution, which means:

- The structural ceiling (~1880–1902 removable cells) is fixed to that one grid.
- All generated puzzles are fundamentally variations on a single answer.

DLX64S breaks this constraint by generating diverse, algorithmically valid complete grids. Each run (with a different seed) produces a distinct solution that can be fed to the Digger.

## Goals

1. **Generate** a complete, valid 64×64 Base64 Sudoku grid from scratch — no seed puzzle required.
2. **Randomize** output so each invocation yields a different grid (seeded for reproducibility when needed).
3. **Emit** the result in the B64Sud grid format so it can be piped directly to `--dig`.

Non-goals for this project:
- Solving partially-filled puzzles (that is B64Sud's role)
- Digging or difficulty classification
- Uniqueness verification (a complete grid is valid by construction; uniqueness is a property of *puzzles*, not solutions)

## Relationship to B64Sud

| Concern | B64Sud | DLX64S |
|---------|--------|--------|
| Input | Solved grid → puzzle | (none) |
| Output | Puzzle file | Solved grid |
| Algorithm | Tiered logic + backtrack | Algorithm X / DLX |
| Grid format | FORMAT.md | FORMAT.md (same) |

The two tools compose: `dlx64s | b64sud --dig - <profile>` (or via file) produces a complete puzzle-generation pipeline from empty board to playable puzzle.

## Grid Format

See `FORMAT.md`. Output is identical to `solution.txt` — a valid 71×71 ASCII file, no empty cells.
