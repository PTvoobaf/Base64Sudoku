namespace DLX64S.Tests;

public sealed class GridParserTests
{
    // AppContext.BaseDirectory is bin/Debug/net10.0/ — go up 3 levels to DLX64S.Tests/
    private static readonly string ExhibitsDir =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../exhibits/"));

    private static string ReadExhibit(string filename) =>
        File.ReadAllText(Path.Combine(ExhibitsDir, filename));

    [Fact]
    public void Parse_SolutionTxt_AllCellsFilled()
    {
        var grid = GridParser.Parse(ReadExhibit("solution.txt"));

        for (int r = 0; r < Grid.Size; r++)
        for (int c = 0; c < Grid.Size; c++)
            Assert.NotEqual(Grid.Empty, grid[r, c]);
    }

    [Fact]
    public void Serialize_RoundTrip_IsIdentical()
    {
        var original = ReadExhibit("solution.txt")
            .ReplaceLineEndings("\n")
            .TrimEnd('\n');

        var grid = GridParser.Parse(original);
        var serialized = GridParser.Serialize(grid);

        Assert.Equal(original, serialized);
    }

    [Fact]
    public void Parse_PoorExample_LoadsWithoutException()
    {
        var grid = GridParser.Parse(ReadExhibit("poor_example1.txt"));

        Assert.NotNull(grid);
        Assert.Equal(Grid.Empty, grid[0, 2]); // known empty cell in first row
    }

    [Fact]
    public void Grid_NewInstance_AllCellsAreEmpty()
    {
        var grid = new Grid();

        for (int r = 0; r < Grid.Size; r++)
        for (int c = 0; c < Grid.Size; c++)
            Assert.Equal(Grid.Empty, grid[r, c]);
    }

    [Fact]
    public void Grid_Clone_IsIndependent()
    {
        var grid = GridParser.Parse(ReadExhibit("solution.txt"));
        var clone = grid.Clone();

        clone[0, 0] = Grid.Empty;

        Assert.NotEqual(Grid.Empty, grid[0, 0]);
    }
}
