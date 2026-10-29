namespace DLX64S.Tests;

public sealed class GeneratorTests
{
    // Generate once with a fixed seed and share across all facts.
    private static readonly Grid s_grid = GenerateOne(seed: 42);

    private static Grid GenerateOne(long seed)
    {
        var sb = new System.Text.StringBuilder();
        using var sw = new System.IO.StringWriter(sb);
        Generator.Generate(seed, count: 1, sw);
        return GridParser.Parse(sb.ToString());
    }

    [Fact]
    public void Generate_NoEmptyCells()
    {
        for (int r = 0; r < Grid.Size; r++)
        for (int c = 0; c < Grid.Size; c++)
            Assert.NotEqual(Grid.Empty, s_grid[r, c]);
    }

    [Fact]
    public void Generate_SerializeRoundTripIsIdentical()
    {
        var sb = new System.Text.StringBuilder();
        using var sw = new System.IO.StringWriter(sb);
        Generator.Generate(seed: 42, count: 1, sw);

        var text = sb.ToString().ReplaceLineEndings("\n").TrimEnd('\n');
        var grid = GridParser.Parse(text);
        Assert.Equal(text, GridParser.Serialize(grid));
    }

    [Fact]
    public void Generate_EachRowHasAllSymbols()
    {
        for (int r = 0; r < Grid.Size; r++)
        {
            var seen = new HashSet<char>();
            for (int c = 0; c < Grid.Size; c++)
                Assert.True(seen.Add(s_grid[r, c]), $"Duplicate in row {r}");
        }
    }

    [Fact]
    public void Generate_EachColumnHasAllSymbols()
    {
        for (int c = 0; c < Grid.Size; c++)
        {
            var seen = new HashSet<char>();
            for (int r = 0; r < Grid.Size; r++)
                Assert.True(seen.Add(s_grid[r, c]), $"Duplicate in col {c}");
        }
    }

    [Fact]
    public void Generate_EachBoxHasAllSymbols()
    {
        int B = Grid.BlockSize;
        for (int br = 0; br < Grid.Size / B; br++)
        for (int bc = 0; bc < Grid.Size / B; bc++)
        {
            var seen = new HashSet<char>();
            for (int r = br * B; r < (br + 1) * B; r++)
            for (int c = bc * B; c < (bc + 1) * B; c++)
                Assert.True(seen.Add(s_grid[r, c]), $"Duplicate in box ({br},{bc})");
        }
    }

    [Fact]
    public void Generate_DifferentSeedsProduceDifferentGrids()
    {
        var grid2 = GenerateOne(seed: 43);
        bool anyDifference = false;
        for (int r = 0; r < Grid.Size && !anyDifference; r++)
        for (int c = 0; c < Grid.Size && !anyDifference; c++)
            if (s_grid[r, c] != grid2[r, c]) anyDifference = true;
        Assert.True(anyDifference);
    }
}
