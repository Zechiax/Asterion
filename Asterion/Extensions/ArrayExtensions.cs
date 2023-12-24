namespace Asterion.Extensions;

public static class ArrayExtensions
{
    public static IEnumerable<ArraySegment<T>> Split<T>(this T[] array, int blockSize)
    {
        var offset = 0;
        while (offset < array.Length)
        {
            var remaining = array.Length - offset;
            var blockSizeToUse = Math.Min(remaining, blockSize);
            yield return new ArraySegment<T>(array, offset, blockSizeToUse);
            offset += blockSizeToUse;
        }
    }
}