using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class P0HostContextTests
{
    [Fact]
    public void HostContextIsVersionedIdempotentAndDoesNotTrackLocation()
    {
        var ctx = new HostContextState(hosted: true);
        Assert.True(ctx.Snapshot.Hosted);
        Assert.Equal(1, ctx.Snapshot.Schema);
        Assert.Null(ctx.Snapshot.SafeMode);
        ctx.AcceptLanguage("EL-gr");
        ctx.AcceptTier("Low");
        ctx.AcceptTheme(false);
        ctx.AcceptUiScale(125);
        Assert.Equal(125, ctx.Snapshot.UiScalePercent);
        Assert.Equal("el-gr", ctx.Snapshot.Language);
        Assert.Equal("Low", ctx.Snapshot.PerformanceTier);
        Assert.False(ctx.Snapshot.IsDark);
        Assert.Equal(4, ctx.Snapshot.Revision);
        ctx.AcceptTheme(false);
        ctx.AcceptTier("low");
        ctx.AcceptLanguage("el-GR");
        ctx.AcceptUiScale(125);
        ctx.AcceptUiScale(500);
        Assert.Equal(4, ctx.Snapshot.Revision);
        Assert.DoesNotContain(typeof(HostContextSnapshot).GetProperties(),
            p => p.Name.Contains("Location", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InvalidHostMessagesAreIgnoredAndStandaloneHasSafeDefaults()
    {
        var ctx = new HostContextState();
        ctx.AcceptLanguage("fr");
        ctx.AcceptLanguage("../invalid");
        ctx.AcceptLanguage(new string('x', 64));
        Assert.Equal("fr", ctx.Snapshot.Language);
        ctx.AcceptTier("not-a-profile");
        Assert.Equal("Balanced", ctx.Snapshot.PerformanceTier);
        Assert.False(ctx.Snapshot.Hosted);
    }
}
