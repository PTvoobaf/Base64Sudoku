namespace DLX64S;

internal sealed class NodePool
{
    private readonly Node[] _nodes;
    private int _next;

    public const int Root = 0;

    // capacity: number of nodes excluding the root sentinel (index 0).
    public NodePool(int capacity)
    {
        _nodes = new Node[capacity + 1];
        _next = 1;

        ref var root = ref _nodes[Root];
        root.Left = Root;
        root.Right = Root;
        root.Up = Root;
        root.Down = Root;
        root.Column = Root;
        root.RowId = -1;
    }

    public ref Node this[int i] => ref _nodes[i];

    // Allocate a column header and append it to the end of the horizontal header list.
    public int AllocHeader()
    {
        int idx = _next++;
        ref var n = ref _nodes[idx];
        n.Up = idx;
        n.Down = idx;
        n.Column = idx;
        n.RowId = -1;

        int left = _nodes[Root].Left;
        n.Left = left;
        n.Right = Root;
        _nodes[left].Right = idx;
        _nodes[Root].Left = idx;
        return idx;
    }

    // Allocate all data nodes for a row and link them horizontally and into their columns.
    // params ReadOnlySpan<int> avoids heap allocation at each call site (C# 13 / .NET 10).
    public void AddRow(int rowId, params ReadOnlySpan<int> columns)
    {
        int last = -1;
        for (int i = 0; i < columns.Length; i++)
        {
            int col = columns[i];
            int idx = _next++;
            ref var n = ref _nodes[idx];
            n.Column = col;
            n.RowId = rowId;
            n.Left = idx;
            n.Right = idx;

            // Append to bottom of column (insert above header).
            int above = _nodes[col].Up;
            n.Up = above;
            n.Down = col;
            _nodes[above].Down = idx;
            _nodes[col].Up = idx;
            _nodes[col].Size++;

            // Link into horizontal row list.
            if (last >= 0)
            {
                int right = _nodes[last].Right; // = first node (circular)
                n.Left = last;
                n.Right = right;
                _nodes[last].Right = idx;
                _nodes[right].Left = idx;
            }
            last = idx;
        }
    }

    // Remove column col from the header list and remove all rows that intersect it
    // from every other column they appear in.
    public void Cover(int col)
    {
        _nodes[_nodes[col].Right].Left = _nodes[col].Left;
        _nodes[_nodes[col].Left].Right = _nodes[col].Right;

        for (int row = _nodes[col].Down; row != col; row = _nodes[row].Down)
            for (int node = _nodes[row].Right; node != row; node = _nodes[node].Right)
            {
                _nodes[_nodes[node].Down].Up = _nodes[node].Up;
                _nodes[_nodes[node].Up].Down = _nodes[node].Down;
                _nodes[_nodes[node].Column].Size--;
            }
    }

    // Restore column col and all rows removed by Cover — exact reverse of Cover.
    // No separate undo stack required; the linked structure encodes the undo order.
    public void Uncover(int col)
    {
        for (int row = _nodes[col].Up; row != col; row = _nodes[row].Up)
            for (int node = _nodes[row].Left; node != row; node = _nodes[node].Left)
            {
                _nodes[_nodes[node].Down].Up = node;
                _nodes[_nodes[node].Up].Down = node;
                _nodes[_nodes[node].Column].Size++;
            }

        _nodes[_nodes[col].Right].Left = col;
        _nodes[_nodes[col].Left].Right = col;
    }
}
