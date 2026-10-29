namespace DLX64S;

// Standard Base64 alphabet: A-Z=0..25, a-z=26..51, 0-9=52..61, +=62, /=63
internal static class Symbols
{
    internal static readonly char[] IndexToChar =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/".ToCharArray();

    private static readonly int[] s_charToIndex = BuildMap();

    private static int[] BuildMap()
    {
        var map = new int[128];
        Array.Fill(map, -1);
        for (int i = 0; i < Grid.Size; i++)
            map[IndexToChar[i]] = i;
        return map;
    }

    internal static int ToIndex(char c) => c < 128 ? s_charToIndex[c] : -1;
    internal static char ToChar(int index) => IndexToChar[index];
}
