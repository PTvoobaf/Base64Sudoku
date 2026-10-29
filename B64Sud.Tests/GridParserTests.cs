namespace B64Sud.Tests;

public sealed class GridParserTests
{
    // AppContext.BaseDirectory is bin/Debug/net10.0/ — go up 4 levels to repo root
    private static readonly string RepoRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));

    private static string ReadFixture(string filename) =>
        File.ReadAllText(Path.Combine(RepoRoot, filename));

    [Fact]
    public void Parse_SolutionTxt_AllCellsFilled()
    {
        var grid = GridParser.Parse(ReadFixture("solution.txt"));

        for (int r = 0; r < Grid.Size; r++)
        for (int c = 0; c < Grid.Size; c++)
            Assert.NotEqual(Grid.Empty, grid[r, c]);
    }

    [Fact]
    public void Serialize_RoundTrip_IsIdentical()
    {
        var original = ReadFixture("solution.txt")
            .ReplaceLineEndings("\n")
            .TrimEnd('\n');

        var grid = GridParser.Parse(original);
        var serialized = GridParser.Serialize(grid);

        Assert.Equal(original, serialized);
    }

    [Fact]
    public void Parse_PoorExample_LoadsWithoutException()
    {
        var grid = GridParser.Parse(ReadFixture("poor_example1.txt"));

        // confirms parse completed — has empty cells by design
        Assert.NotNull(grid);
        Assert.Equal(Grid.Empty, grid[0, 2]); // known empty cell from first line
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
        var grid = GridParser.Parse(ReadFixture("solution.txt"));
        var clone = grid.Clone();

        clone[0, 0] = Grid.Empty;

        Assert.NotEqual(Grid.Empty, grid[0, 0]);
    }
}
