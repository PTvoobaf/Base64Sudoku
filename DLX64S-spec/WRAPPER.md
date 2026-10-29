# PEM-Style Sudoku and CTF Artifact Plan

## Intent

Make PEM-style encapsulation the canonical interchange format for both B64Sud and DLX64S. This is a format revision, not a separate wrapper utility: normal parsing, serialization, CLI input/output, pipelines, tests, fixtures, and saved artifacts must all understand the document format.

The public Sudoku document deliberately contains little metadata. Private event provisioning data belongs in a JSON sidecar that is ingested by the CTFd plugin and never distributed with the competitor artifact.

The existing 71-line grid remains the document body. The solver, digger, and simulated-annealing generator continue to operate on `Grid` values internally; the application boundary changes from a bare `Grid` to a versioned document containing a grid and validated metadata.

This plan follows the later DLX64S closeout where it supersedes the original B64Sud plan. In particular, DLX64S is a simulated-annealing generator despite its historical name and generated solutions are diverse.

## Canonical Version 1 Sudoku Document

Puzzle and solution documents use the same boundary:

```text
-----BEGIN BASE64 SUDOKU-----
Format-Version: 1
Alphabet: RFC4648
Puzzle-Id: 0xbadc0ffee0ddf00d
Empty-Cells: 1884

RQAL.... .H.Grh.. ...
...
-----END BASE64 SUDOKU-----
```

`Empty-Cells` determines how the document is used: zero denotes a complete candidate solution and a positive value denotes a puzzle to solve. There is no separate puzzle/solution marker.

### Syntax and canonicalization

- The only version-1 boundary label is `BASE64 SUDOKU`.
- Headers are ASCII `Name: Value` lines followed by one empty separator line. Duplicate headers, malformed lines, mismatched boundaries, and trailing non-document content are errors.
- The header block contains exactly four fields, in this order: `Format-Version`, `Alphabet`, `Puzzle-Id`, and `Empty-Cells`. All are required; no other or extension headers are permitted in version 1.
- Grid dimensions are fixed at 64x64 by version 1 and are not repeated in a header. A different size requires a different format version/specification.
- `Alphabet` is `RFC4648` in version 1.
- `Puzzle-Id` is the generator's signed `Int64` seed rendered as its exact unsigned 64-bit bit pattern: `0x` plus 16 lowercase hex digits. A puzzle and its corresponding complete solution carry the same value so they can be matched without inspecting their bodies.
- `Empty-Cells` is decimal in the range 0-4096 and must equal the number of `.` cells in the body.
- The body is exactly the current physical grid: 64 data rows, seven empty gutter rows, 71 characters on each data row, spaces only in the seven gutter columns, RFC 4648 symbols in filled cells, and `.` only in empty cells. There is no second Base64 encoding layer.
- Writers emit headers in the order shown above, use LF line endings, and end the closing boundary with one newline. Readers accept CRLF or LF.
- Multiple documents form a stream by simple concatenation, optionally with blank lines between closing and opening boundaries. Blank lines no longer carry record-separation semantics. Single-document commands reject zero or multiple input documents with a clear error.

### `Puzzle-Id` and seed policy

`Puzzle-Id` intentionally exposes the exact generator seed. No hash, HMAC, event secret, or separate public identifier is used.

- C# `long` is `System.Int64`. Preserve its exact 64-bit two's-complement pattern when formatting and parsing hexadecimal values, including seeds whose high bit is set.
- The JSON sidecar stores the same hexadecimal value under `seed`; equality is a direct string/bit-pattern comparison.
- The command-line seed is rendered directly as `Puzzle-Id`; the format layer does not allocate, derive, reserve, or validate seeds across invocations.
- DLX64S already supports `--count`. Preserve that existing behavior without adding per-member seed derivation: documents from one counted run carry the command's seed and remain reproducible as the same ordered run.
- The event artifact workflow is expected to invoke generation separately for each competitor/team with an administrator-selected seed. Avoiding seed reuse is an event-administration responsibility outside this codebase.
- The event accepts that a competitor who discovers or reconstructs the confidential generation software may use the public seed to regenerate a solution. Keeping that software, its binaries, source, deployment endpoints, diagnostics, and algorithm details unavailable to competitors is an explicit administrator responsibility.

### Information-minimization consequences

- A puzzle cannot be selected, sorted, or audited by intended difficulty or tier cap without solving it again or consulting the private sidecar/catalog. A fresh solve reports observed technique use, which is not the same as the digger's original cap guarantee.
- `Empty-Cells` still reveals clue density and may act as a weak difficulty signal, while anyone with the solver can measure observed tier use.
- The public generator seed plus the private dig seed, exact command settings, tool revision, and hashes in the sidecar form the diagnostic bundle needed to reproduce generation and detect mismatched files.
- Metadata cannot go stale or contradict the body except for `Empty-Cells`, which the writer derives and the reader verifies.
- Existing filenames such as `*-t4-2.txt`, console diagnostics, and benchmark tables may still disclose removed information. Public event packaging needs a separate leak check.
- With no extension headers, adding a future public field requires a new format version. Private diagnostic fields can evolve through the sidecar schema instead.

## Private JSON Sidecar

JSON is the sidecar format. It is directly supported by C#, PowerShell, and CTFd plugins, and it keeps private reproduction and scoring data out of the public document.

Use one UTF-8 JSON file per competitor puzzle. A representative schema is:

```json
{
  "schemaVersion": 1,
  "seed": "0xbadc0ffee0ddf00d",
  "artifactFile": "competitor-0042.b64sud.txt",
  "puzzleSha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
  "solutionSha256": "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
  "generation": {
    "digSeed": "0x2222222222222222",
    "targetEmptyCells": 1884,
    "tierCaps": [2147483647, 2147483647, 2147483647, 0],
    "toolRevision": "<git-commit>"
  },
  "flag": {
    "value": "0123456789ABCDEFGHIJKLMNOPQRSTUV",
    "derivation": "upper-right-diagonal-radius",
    "coordinateBase": 0,
    "start": { "row": 31, "col": 32 },
    "end": { "row": 0, "col": 63 },
    "inclusive": true
  }
}
```

Schema rules:

- Store 64-bit values as fixed-width hex strings, not JSON numbers; JavaScript cannot exactly represent every 64-bit integer.
- `seed` equals the public document's `Puzzle-Id` exactly and is the provisioning join key.
- The SHA-256 fields hash the exact canonical serialized bytes. They are integrity checks only; the flag is not derived from a hash.
- `generation` is private replay/triage data and may grow in later sidecar schema versions without changing the public format.
- `flag.value` is the exact case-sensitive value registered with CTFd. Never print it in normal output, errors, CI logs, or public manifests.
- RFC 4648 flags may contain `+` and `/`. JSON handles both, but the plugin/API client must not form-decode `+` into a space.

### Flag derivation

Coordinates are zero-based. The 32-character upper-right diagonal radius is `(31,32)` through `(0,63)`, inclusive:

```text
(31,32), (30,33), ... (1,62), (0,63)
```

Extract these characters from the complete solution in that order, with no separators or case conversion. Extraction happens before or while digging the competitor puzzle, so a player can recover the same flag by solving only those 32 cells.

### Sidecar and CTFd security

- Write public artifacts and private sidecars to different roots with different access policies. A sidecar must not merely sit beside the artifact with another extension.
- Package only puzzle documents. An automated release check fails if JSON sidecars, complete solutions, or private manifests appear in the public bundle.
- During the event, do not publish or expose the B64Sud/DLX64S source, binaries, container images, generation endpoints, or reproducible algorithm details that would turn the public seed directly into a solution.
- The CTFd plugin ingests sidecars into server-side storage, associates the administrator-supplied `seed`/`Puzzle-Id` with the intended user or team and artifact, registers the exact flag, and removes or archives the import file outside web-served paths.
- The importer may reject an internally ambiguous assignment, but generating and policing unique seeds across competitors is not a B64Sud/DLX64S responsibility.
- Verify both hashes before assignment so a sidecar cannot be paired with the wrong documents.
- Protect database exports, backups, admin endpoints, debug logging, and plugin error pages because the sidecar contains the answer.

## Architecture

### 1. Establish one shared document implementation

Add a neutral class-library project (for example, `B64Sud.Format`) referenced by both executables and both test projects. Move the duplicated `Grid`, `Symbols`, and physical-grid codec into it, then remove the two independent `GridParser` implementations. The generator must not reference the B64Sud application, and B64Sud must not reference DLX64S; both reference only the format library.

The shared library should expose these concepts:

- `SudokuMetadata`: format version, alphabet, generator seed represented as the public puzzle ID, and empty-cell count.
- `SudokuDocument`: a `Grid` plus metadata. Puzzle-versus-solution status is derived from `Empty-Cells`, not stored independently.
- `GridBodyCodec`: strict parsing/serialization of the 71-line body. It is an implementation layer, not a public alternate file format.
- `SudokuDocumentReader` / `SudokuDocumentWriter`: single-document and stream APIs over `TextReader` / `TextWriter`, with path helpers only at the CLI layer.
- A structured parse exception carrying a line number and concise reason so malformed CLI input does not surface as an unhandled exception.

Keep Sudoku algorithms independent from presentation: `Solver.Solve`, `Digger.Dig`, and `Annealer.Run` may continue to consume or return `Grid`. Application and event-provisioning services construct documents, derive `Empty-Cells`, and preserve the public puzzle ID.

### 2. Define metadata ownership

Metadata must never become stale when a grid changes.

- The shared writer derives and validates `Empty-Cells`; callers cannot emit an inconsistent count.
- DLX64S renders the actual per-grid generation seed as `Puzzle-Id` when it emits a document with `Empty-Cells: 0`.
- B64Sud `--dig` accepts only a document whose `Empty-Cells` is zero, preserves its puzzle ID, and emits a document with a derived positive empty-cell count.
- B64Sud `--solve` accepts either form, preserves its puzzle ID, and emits a complete document with `Empty-Cells: 0` on success.
- Difficulty, tier caps, flags, hashes, dig seeds, and competitor identity are never serialized into the public document.
- Classification is structural only. `Empty-Cells: 0` does not prove Sudoku validity; the complete grid still undergoes normal validation.

### 3. Make the CLI contract document-oriented

Update the normal commands rather than adding a `wrap` command:

- `DLX64S [--seed N] [--count N] [--out file]` emits one or more complete documents. Each document carries the command seed as `Puzzle-Id`; no separate `--puzzle-id` option or new seed-allocation behavior is added.
- `B64Sud --dig <solution-file|-> ...` reads one complete document and emits one puzzle document.
- `B64Sud --solve <document-file|->` reads one document and emits a complete document on success (or the first completion in the existing non-unique-result case).
- Support `-` as stdin for B64Sud so the documented `DLX64S | B64Sud --dig - ...` composition actually works. Keep progress, status, and errors exclusively on stderr so stdout is always a parseable document stream. In event provisioning mode, suppress private dig seeds and flag material from logs.
- Preserve existing exit-code meanings. Add a distinct, documented input-format failure exit code rather than reporting malformed documents as invalid Sudoku constraints.
- `--out` and ordinary file extensions do not need to change: the boundary is self-identifying, and retaining `.txt` avoids coupling format adoption to a rename. Help text and examples must call the files Sudoku documents rather than raw grids.

No permanent converter executable should sit in the runtime path. A migration-only command or script may consume legacy raw files, but all standard producers write version 1 and all examples use it.

## Implementation Sequence

### Phase 1: Freeze the public document contract

1. Add the shared format project and tests to `B64Sud.slnx`.
2. Implement the typed document model and strict body/envelope reader/writer.
3. Add golden examples for a complete document, a puzzle document, and a two-document stream.
4. Test LF/CRLF input, canonical LF output, exact headers, common boundaries, puzzle-ID preservation, and stream parsing.
5. Add negative cases for bad boundaries, unsupported versions/alphabets, duplicate/extra/missing headers, malformed seeds/counts, wrong line shapes/gutters/symbols, incorrect empty counts, trailing content, and multiple documents passed to a single-document API.

This phase should not expose a general `ParseGrid(string)` escape hatch. Tests needing a grid should parse a document or build a `Grid` directly.

### Phase 2: Integrate DLX64S and B64Sud

1. Replace raw `GridParser` file I/O with the shared reader/writer.
2. Make DLX64S output document streams rather than blank-line-separated raw grids.
3. Make B64Sud preserve the puzzle ID across dig and solve transformations.
4. Validate complete-versus-puzzle preconditions from the derived empty count before expensive work.
5. Add stdin support and process-level tests that keep stdout machine-readable.
6. Add an end-to-end test: generate a complete document, dig it, solve it, and assert identical puzzle IDs and cell-correct output.

### Phase 3: Generate sidecars for event handoff

1. Extend the per-instance generation/dig workflow to accept the administrator-supplied seed, generate a complete grid and puzzle, and extract the private diagonal flag from the complete grid.
2. Canonically serialize puzzle and solution documents, compute their SHA-256 values, and write the private JSON sidecar atomically.
3. Verify exact equality among the supplied seed, sidecar `seed`, and both documents' `Puzzle-Id`; verify hashes and a fresh flag extraction.
4. Add a CTFd plugin import path that assigns one artifact and exact flag per user/team without exposing sidecar content.
5. Add a packaging gate that proves the public bundle contains only intended puzzle documents.

### Phase 4: Migrate repository data

Convert, do not merely exempt, the current bare-grid corpus:

- Root `solution.txt` and `poor_example1.txt`.
- The two DLX64S test exhibits.
- All 54 files in `B64Sud.Trial-Artifacts/`.
- All 70 files in `DLX64S.SA-Artifacts/`.

Use the documented generation seed for historical families where it is known. For older artifacts that predate seeded generation or whose seed is unavailable, require a caller-supplied synthetic migration ID and record that exception; such artifacts are not eligible for event provisioning without regeneration. Use each value consistently across matching documents, derive `Empty-Cells`, and verify all 4,096 cells remain unchanged.

Do not generate event flag sidecars for historical development artifacts unless they are deliberately imported into an event. A one-time raw-grid migration utility may exist, but normal readers remain strict and normal writers emit version 1.

### Phase 5: Update specifications and operational scripts

1. Make one format specification authoritative and have the other project reference it instead of maintaining another copied description.
2. Update B64Sud and DLX64S format, solver, digger/generator, milestone, and closeout documentation for the common boundary, fixed size, four headers, sidecar separation, stdin, and strict validation.
3. Update `DLX64S.Tests/m4-timing.ps1` and documented PowerShell examples to generate, validate, dig, and solve framed documents.
4. Document a private diagnostic-bundle procedure for bug triage: sidecar, exact tool revision, stderr, and canonical puzzle/solution documents.
5. Search for assumptions about bare grids, old boundary kinds, blank-line record separation, `GridParser`, and leaked secrets.

## Verification and Acceptance Criteria

The change is complete only when all of the following hold:

- Both applications depend on the same document parser/writer; no duplicated production format implementation remains.
- Every normal CLI output uses the common `BASE64 SUDOKU` boundary and exactly the four public headers.
- DLX64S output feeds B64Sud `--dig -`, digger output feeds `--solve -`, and no preprocessing/converter step is needed.
- Puzzle/solution pairs preserve the same `Puzzle-Id` and are distinguished solely by validated `Empty-Cells`.
- `Grid-Size`, difficulty, tier caps, flag data, private dig seeds, hashes, and competitor identity never appear in a public document.
- Existing `--count` behavior still emits and parses the requested number of documents; this change adds no per-member seed semantics.
- A player who solves the 32 cells from `(31,32)` through `(0,63)` obtains exactly the CTFd flag held in the private sidecar.
- Sidecar hashes match the assigned documents; sidecars and complete solutions are absent from public packages.
- Event packaging and deployment checks confirm that generation software and endpoints are not competitor-accessible.
- Malformed structure fails before solver/generator work begins and is distinguishable from an invalid Sudoku.
- All migrated fixtures and artifacts retain their original 4,096 cell values exactly.
- Existing algorithm tests still pass, new format and CLI integration tests pass, and the M4 timing/validation script still reports valid generated grids.
- A repository search finds no standard code path that accepts or emits a bare grid except the explicit, temporary legacy migration path.

## Out of Scope

- Encoding the grid body as Base64; the irony of plain Base64-symbol cells inside PEM-style boundaries is intentional.
- Putting the flag, hashes, difficulty, tier caps, or competitor identity in the public document.
- Changing solver techniques, digger acceptance logic, SA parameters, or the dormant DLX algorithms.
- Supporting puzzle sizes other than 64x64 or alternate alphabets in version 1.
- Allocating competitor/team seeds, checking seed uniqueness across invocations, or deciding how administrator identity maps to seeds.
- Cryptographic PEM features such as encryption, checksums, signatures, or RFC 1421 key processing. This format borrows encapsulation aesthetics and self-description only.
