namespace OpsisNewsCrawler.Core;

/// <summary>Pure slot-order operation used by drag-and-drop and its keyboard fallback.</summary>
public static class BarLayoutReorder
{
    public static BarSlots Swap(BarSlots current) => current.B is { } second
        ? new BarSlots(second, current.A)
        : current; // A is required; never erase content when B is empty.
}
