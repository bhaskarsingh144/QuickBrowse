using System;

internal static class BrowseMutationFocusTests
{
    private static int _failed;

    private static void Main()
    {
        Expect(BrowseMutationFocus.NeighborIndexAfterDelete(0, 0) == -1, "empty remains -1");
        Expect(BrowseMutationFocus.NeighborIndexAfterDelete(3, 0) == -1, "empty remains -1 b");
        Expect(BrowseMutationFocus.NeighborIndexAfterDelete(0, 5) == 0, "delete first keeps 0");
        Expect(BrowseMutationFocus.NeighborIndexAfterDelete(2, 5) == 2, "delete middle keeps slot");
        Expect(BrowseMutationFocus.NeighborIndexAfterDelete(5, 5) == 4, "delete last clamps");
        Expect(BrowseMutationFocus.NeighborIndexAfterDelete(9, 3) == 2, "past end clamps");
        Expect(BrowseMutationFocus.NeighborIndexAfterDelete(-1, 4) == 0, "negative -> 0");

        if (_failed != 0)
            Environment.Exit(1);
        Console.WriteLine("BrowseMutationFocusTests OK");
    }

    private static void Expect(bool cond, string name)
    {
        if (cond)
        {
            Console.WriteLine("  PASS  " + name);
            return;
        }
        _failed++;
        Console.WriteLine("  FAIL  " + name);
    }
}
