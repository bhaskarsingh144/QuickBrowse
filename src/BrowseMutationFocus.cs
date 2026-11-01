/// <summary>Pure helpers for post-mutation list focus (after reload).</summary>
internal static class BrowseMutationFocus
{
    /// <summary>
    /// Index to focus after deleting items that started at <paramref name="firstDeletedIndex"/>.
    /// Prefers the item that slid into the deleted slot; clamps to last item; -1 if none remain.
    /// </summary>
    public static int NeighborIndexAfterDelete(int firstDeletedIndex, int remainingCount)
    {
        if (remainingCount <= 0)
            return -1;
        if (firstDeletedIndex < 0)
            return 0;
        if (firstDeletedIndex >= remainingCount)
            return remainingCount - 1;
        return firstDeletedIndex;
    }
}
