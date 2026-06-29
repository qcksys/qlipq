namespace Qlipq.Core;

public static class Ids
{
    private const string Base36 = "0123456789abcdefghijklmnopqrstuvwxyz";

    /// <summary>
    /// Generate a short, URL/file-safe random id for queue items. Mirrors the shape of the
    /// TypeScript <c>createId</c> (8 random base36 chars + 4 base36 chars of the timestamp).
    /// </summary>
    public static string CreateId()
    {
        Span<char> chars = stackalloc char[12];
        for (var i = 0; i < 8; i++)
        {
            chars[i] = Base36[Random.Shared.Next(36)];
        }
        var ticks = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (var i = 11; i >= 8; i--)
        {
            chars[i] = Base36[(int)(ticks % 36)];
            ticks /= 36;
        }
        return new string(chars);
    }
}
