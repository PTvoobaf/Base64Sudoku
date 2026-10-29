namespace DLX64S;

public sealed class Grid
{
    public const int Size = 64;
    public const int BlockSize = 8;
    public const char Empty = '.';

    private readonly char[] _cells;

    public Grid()
    {
        _cells = new char[Size * Size];
        Array.Fill(_cells, Empty);
    }

    private Grid(char[] cells) => _cells = cells;

    public char this[int row, int col]
    {
        get => _cells[row * Size + col];
        set => _cells[row * Size + col] = value;
    }

    public Grid Clone() => new((char[])_cells.Clone());
}
