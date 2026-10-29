# Data Format

- Grid Size: 64x64 cells
- Sub-grid boxes: 8x8 blocks
- Digit symbols: Standard Base64 [`A-Za-z0-9+/`]
- Empty Cell: Period `.`
  - Complete solution has no empty cells
- Physical Layout: 71 total rows (64 data rows + 7 spacing rows)
  - 71 total columns (64 data columns + 7 spacing columns)
  - Space characters ` ` act as gutters between 8x8 blocks
- Persistence: Plaintext ASCII

## Reference Files

- `solution.txt` — a fully solved 64x64 grid; use as the canonical fixture for testing and as the Digger's starting point
- `poor_example1.txt` — a puzzle estimated to require effectively infinite solver time; retained as a negative test fixture to verify that the Solver correctly rejects or caps runaway computation
