namespace DLX64S.Tests;

public sealed class NodePoolTests
{
    // 3 columns, 3 rows:
    //   row 0 covers {col1, col2}
    //   row 1 covers {col2, col3}
    //   row 2 covers {col1, col3}
    // Each column has exactly 2 rows. No row covers all three columns.
    private static (NodePool pool, int col1, int col2, int col3) BuildThreeByThree()
    {
        var pool = new NodePool(capacity: 16);
        int col1 = pool.AllocHeader();
        int col2 = pool.AllocHeader();
        int col3 = pool.AllocHeader();
        pool.AddRow(rowId: 0, col1, col2);
        pool.AddRow(rowId: 1, col2, col3);
        pool.AddRow(rowId: 2, col1, col3);
        return (pool, col1, col2, col3);
    }

    [Fact]
    public void AllocHeader_LinksIntoHorizontalList()
    {
        var (pool, col1, col2, col3) = BuildThreeByThree();

        // Forward: Root → col1 → col2 → col3 → Root
        Assert.Equal(col1, pool[NodePool.Root].Right);
        Assert.Equal(col2, pool[col1].Right);
        Assert.Equal(col3, pool[col2].Right);
        Assert.Equal(NodePool.Root, pool[col3].Right);

        // Backward: Root ← col1 ← col2 ← col3 ← Root
        Assert.Equal(col3, pool[NodePool.Root].Left);
        Assert.Equal(col2, pool[col3].Left);
        Assert.Equal(col1, pool[col2].Left);
        Assert.Equal(NodePool.Root, pool[col1].Left);
    }

    [Fact]
    public void AddRow_ColumnSizesAreCorrect()
    {
        var (pool, col1, col2, col3) = BuildThreeByThree();

        Assert.Equal(2, pool[col1].Size); // rows 0 and 2
        Assert.Equal(2, pool[col2].Size); // rows 0 and 1
        Assert.Equal(2, pool[col3].Size); // rows 1 and 2
    }

    [Fact]
    public void Cover_RemovesColumnFromHeaderList()
    {
        var (pool, col1, col2, col3) = BuildThreeByThree();

        pool.Cover(col2);

        Assert.Equal(col3, pool[col1].Right);
        Assert.Equal(col1, pool[col3].Left);
    }

    [Fact]
    public void Cover_DecreasesAffectedColumnSizes()
    {
        var (pool, col1, col2, col3) = BuildThreeByThree();

        // Covering col2 removes row 0 ({col1,col2}) and row 1 ({col2,col3})
        // from their respective peer columns.
        pool.Cover(col2);

        Assert.Equal(1, pool[col1].Size); // row 2 ({col1,col3}) remains
        Assert.Equal(1, pool[col3].Size); // row 2 ({col1,col3}) remains
    }

    [Fact]
    public void Uncover_RestoresHeaderList()
    {
        var (pool, col1, col2, col3) = BuildThreeByThree();

        pool.Cover(col2);
        pool.Uncover(col2);

        Assert.Equal(col1, pool[NodePool.Root].Right);
        Assert.Equal(col2, pool[col1].Right);
        Assert.Equal(col3, pool[col2].Right);
        Assert.Equal(NodePool.Root, pool[col3].Right);
    }

    [Fact]
    public void Uncover_RestoresColumnSizes()
    {
        var (pool, col1, col2, col3) = BuildThreeByThree();

        pool.Cover(col2);
        pool.Uncover(col2);

        Assert.Equal(2, pool[col1].Size);
        Assert.Equal(2, pool[col2].Size);
        Assert.Equal(2, pool[col3].Size);
    }

    [Fact]
    public void CoverUncover_NestedPairRestoresFullState()
    {
        var (pool, col1, col2, col3) = BuildThreeByThree();

        pool.Cover(col1);
        pool.Cover(col3);
        pool.Uncover(col3);
        pool.Uncover(col1);

        // Header list fully restored
        Assert.Equal(col1, pool[NodePool.Root].Right);
        Assert.Equal(col2, pool[col1].Right);
        Assert.Equal(col3, pool[col2].Right);
        Assert.Equal(NodePool.Root, pool[col3].Right);

        // All sizes fully restored
        Assert.Equal(2, pool[col1].Size);
        Assert.Equal(2, pool[col2].Size);
        Assert.Equal(2, pool[col3].Size);
    }
}
