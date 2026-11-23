using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class P2BarLayoutReorderTests
{
    [Fact]
    public void SwappingTwoBarHalvesPreservesExactlyTheirContents()
    {
        var x = new BarSlots(new BarSlot(SlotKind.News,"economy"),new BarSlot(SlotKind.Stocks));
        var y = BarLayoutReorder.Swap(x);
        Assert.Equal(x.B,y.A);
        Assert.Equal(x.A,y.B);
        Assert.Equal(x,BarLayoutReorder.Swap(y));
    }
    [Fact]
    public void NeverEraseSingleSlotWhenDragHasNoSecondHalf()
    {
        var x = new BarSlots(new BarSlot(SlotKind.Hazards),null);
        Assert.Equal(x,BarLayoutReorder.Swap(x));
    }
}
