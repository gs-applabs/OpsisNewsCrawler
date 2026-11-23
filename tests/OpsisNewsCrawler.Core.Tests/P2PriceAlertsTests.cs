using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;
public sealed class P2PriceAlertsTests
{
    [Fact] public void PercentChangeAlertFiresOnlyOnKnownValueAndRearms()
    {
        var instrument=new QuoteInstrument(QuoteSource.Yahoo,"ABC","ABC","A","A","€",enabled:true);
        var rule=new PriceAlert {Source=instrument.Source,Code=instrument.Code,Metric="changePercent",Threshold=5,Above=true};
        var blank=new MarketQuote("ABC","A",100,null,DateTimeOffset.UtcNow);
        Assert.Empty(PriceAlertCheck.Evaluate(new[]{rule},new[]{(instrument,blank)}));
        var up=blank with{ChangePercent=6m};
        Assert.Single(PriceAlertCheck.Evaluate(new[]{rule},new[]{(instrument,up)}));
        Assert.Empty(PriceAlertCheck.Evaluate(new[]{rule},new[]{(instrument,up)}));
        PriceAlertCheck.Evaluate(new[]{rule},new[]{(instrument,blank with{ChangePercent=2m})});
        Assert.Single(PriceAlertCheck.Evaluate(new[]{rule},new[]{(instrument,up)}));
        Assert.Equal("changePercent",rule.Clone().Metric);
    }
}
