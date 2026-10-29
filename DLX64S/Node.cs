namespace DLX64S;

internal struct Node
{
    public int Left, Right; // horizontal circular list (row peers or header list)
    public int Up, Down;    // vertical circular list (column peers)
    public int Column;      // index of column header; self-referential for headers
    public int RowId;       // candidate id for data nodes; -1 for headers
    public int Size;        // uncovered row count; meaningful only for column headers
}
