namespace DLX64S;

using System.Text;

public static class GridParser
{
    // Physical layout: 71 rows and 71 cols.
    // Gutter row/col when index % 9 == 8 (after every 8 data positions).
    private const int PhysicalSize = Grid.Size + Grid.BlockSize - 1; // 71

    public static Grid Parse(string text)
    {
        var grid = new Grid();
        var lines = text.ReplaceLineEndings("\n").Split('\n');

        int physRow = 0;
        int lineIndex = 0;

        while (physRow < PhysicalSize && lineIndex < lines.Length)
        {
            var line = lines[lineIndex++];

            if (physRow % 9 == 8)
            {
                physRow++;
                continue;
            }

            int gridRow = (physRow / 9) * Grid.BlockSize + (physRow % 9);

            for (int physCol = 0; physCol < PhysicalSize; physCol++)
            {
                if (physCol % 9 == 8) continue;
                int gridCol = (physCol / 9) * Grid.BlockSize + (physCol % 9);
                grid[gridRow, gridCol] = physCol < line.Length ? line[physCol] : Grid.Empty;
            }

            physRow++;
        }

        return grid;
    }

    public static string Serialize(Grid grid)
    {
        var sb = new StringBuilder(PhysicalSize * (PhysicalSize + 1));

        for (int physRow = 0; physRow < PhysicalSize; physRow++)
        {
            if (physRow > 0) sb.Append('\n');

            if (physRow % 9 == 8) continue;

            int gridRow = (physRow / 9) * Grid.BlockSize + (physRow % 9);

            for (int physCol = 0; physCol < PhysicalSize; physCol++)
            {
                if (physCol % 9 == 8)
                    sb.Append(' ');
                else
                {
                    int gridCol = (physCol / 9) * Grid.BlockSize + (physCol % 9);
                    sb.Append(grid[gridRow, gridCol]);
                }
            }
        }

        return sb.ToString();
    }
}
