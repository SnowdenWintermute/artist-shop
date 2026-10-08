namespace ArtistShop.Web.Utilities;

public static class ListMoves
{
    // a copy with one item moved, as SortableList's OnMove reports it
    public static List<T> Moved<T>(this IReadOnlyList<T> list, (int OldIndex, int NewIndex) move)
    {
        List<T> moved = [.. list];
        var item = moved[move.OldIndex];
        moved.RemoveAt(move.OldIndex);
        moved.Insert(move.NewIndex, item);
        return moved;
    }
}
