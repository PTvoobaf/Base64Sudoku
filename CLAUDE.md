# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Status

**B64Sud: complete.** All four milestones done. See `B64Sud-spec/MILESTONES.md` and `B64Sud-spec/CLOSEOUT.md`.

**DLX64S: in progress.** Milestones 0, 1, and 2 are complete. Milestone 3 (generator) is blocked pending a design decision — see the DLX64S section below.

## What This Project Is

B64Sud is a **64×64 Sudoku solver and puzzle generator** using Base64 character encoding for cell values.

Two programs:

- **Solver** — takes a puzzle (with empty cells), returns the solved grid or a failure reason, always with per-tier technique call counts on stderr
- **Digger** — takes a fully solved grid and a difficulty profile, returns a puzzle by iteratively removing cells while verifying uniqueness and technique-count bounds via the Solver

## Data Format (`B64Sud-spec/FORMAT.md`)

- 64×64 cells, 8×8 sub-grid blocks
- Cell values: standard Base64 alphabet `A-Za-z0-9+/` (64 symbols)
- Empty cell: `.`
- Physical ASCII layout: 71×71 characters — 64 data rows/cols + 7 single-space gutter rows/cols separating the 8×8 blocks
- Persistence: plaintext ASCII

Reference files in the repo root:
- `solution.txt` — canonical fully-solved grid; use for tests and as Digger input
- `poor_example1.txt` — a puzzle requiring effectively infinite solver time; retained as a negative fixture

## Architecture

### Solver (`B64Sud-spec/SOLVER.md`)

State is 4,096 `uint64` bitmasks — one per cell — representing candidate sets. The `uint64` type is a deliberate fit: 64 bits for 64 Base64 symbols. Solving proceeds through tiered techniques:

| Tier | Techniques |
|------|-----------|
| 1 | Naked Singles, Hidden Singles |
| 2 | Intersections (Pointing/Claiming), Naked Pairs |
| 3 | X-Wings, Swordfish, XY-Wings |
| 4 | Depth-limited backtracker (MRV cell selection) |

The Solver accepts per-tier call count caps from the caller and aborts with `RequiresHigherTier` when any cap is exceeded. It always returns per-tier call counts alongside its result.

### Digger (`B64Sud-spec/DIGGER.md`)

Takes a solved grid + `Target_Technique_Profile` (cell target + per-tier caps). Greedy single-pass loop: pick a random cell, remove it, run a capped solve for difficulty classification — if it passes, run a second uncapped solve to verify uniqueness (required when `tierCaps[3] > 0`). Commit or backtrack accordingly.

**Bounded computation time is the primary correctness requirement.**

## Build & Test Commands (B64Sud)

```powershell
dotnet build
dotnet test
dotnet run --project B64Sud -- --solve solution.txt
dotnet run --project B64Sud -- --dig solution.txt 4095 --tier2cap 4094 --tier3cap 4093 --tier4cap 2
```

---

## DLX64S

A companion project using Knuth's Dancing Links (Algorithm X) to generate fresh, fully-solved 64×64 Base64 Sudoku grids — primarily as diverse input for the B64Sud Digger. Spec documents in `DLX64S-spec/`.

### Milestone Status

| Milestone | Status | Notes |
|-----------|--------|-------|
| 0: Scaffolding | **Done** | Grid/GridParser/Symbols copied from B64Sud; 5 tests; `DLX64S.Tests/exhibits/` |
| 1: Node pool | **Done** | `Node` struct + `NodePool` with flat array, `Cover`/`Uncover`; 7 tests |
| 2: Algorithm X | **Done** | `AlgorithmX.Solve` with MCV, local-function closures, bool early-exit; 7 tests incl. 64-rooks |
| 3a: Full-matrix DLX | **Blocked** | All DLX variants stall in last band; see MILESTONES.md |
| 3b: Band pre-fill DLX | **Blocked** | Band-0 and band-0+1 pre-fill both plateau at ~325 remaining cells; structural, not seed-specific |
| 3c: Simulated Annealing | **In progress** | See MILESTONES.md §3c for plan |
| 4: Polish | Pending | |

### Key Files

- `DLX64S/Node.cs` — 7-field fat struct (Left, Right, Up, Down, Column, RowId, Size)
- `DLX64S/NodePool.cs` — flat `Node[]`, root at index 0, headers at 1–N, `Cover`/`Uncover`
- `DLX64S/AlgorithmX.cs` — generic exact-cover solver, no Sudoku knowledge
- `DLX64S/SudokuMatrix.cs` — builds the 262,144-candidate × 16,384-constraint pool with bands 0–1 pre-filled; retained for reference but not used by Generator
- `DLX64S/Generator.cs` — orchestrates SA → emit; `--seed`, `--count`, `--out` (M3c in progress)
- `DLX64S/Program.cs` — CLI entry point

### Build & Test Commands (DLX64S)

```powershell
dotnet build DLX64S/DLX64S.csproj
dotnet test DLX64S.Tests/DLX64S.Tests.csproj --filter "FullyQualifiedName!~Generator"
```

The `GeneratorTests` are excluded above because M3c is incomplete. The 20 passing tests cover M0–M2.
