using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;
public sealed class P2PriceAlertsTests
{
    [Fact] public void MissingAndStaleQuotesDoNotRearmAndMarginPreventsChatter()
    {
        var instrument=new QuoteInstrument(QuoteSource.Yahoo,"ABC","ABC","A","A","€",enabled:true);
        var rule=new PriceAlert {Source=instrument.Source,Code=instrument.Code,Metric="changePercent",Threshold=5,RearmMargin=1,MaxAgeMinutes=60};
        var quote=new MarketQuote("ABC","A",100,6m,DateTimeOffset.UtcNow);
        Assert.Single(PriceAlertCheck.Evaluate(new[]{rule},new[]{(instrument,quote)}));
        Assert.Empty(PriceAlertCheck.Evaluate(new[]{rule},new[]{(instrument,quote with{ChangePercent=null})}));
        Assert.Empty(PriceAlertCheck.Evaluate(new[]{rule},new[]{(instrument,quote with{ChangePercent=0m,AsOf=DateTimeOffset.UtcNow.AddHours(-2)})}));
        Assert.Empty(PriceAlertCheck.Evaluate(new[]{rule},new[]{(instrument,quote with{ChangePercent=4.5m})}));
        Assert.True(rule.Fired);
        PriceAlertCheck.Evaluate(new[]{rule},new[]{(instrument,quote with{ChangePercent=4m})});
        Assert.False(rule.Fired);
        Assert.Single(PriceAlertCheck.Evaluate(new[]{rule},new[]{(instrument,quote)}));
    }

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
        var negative=new PriceAlert {Source=instrument.Source,Code=instrument.Code,
            Metric="changePercent",Threshold=-5,Above=false};
        var down=blank with { ChangePercent=-6m };
        Assert.Single(PriceAlertCheck.Evaluate(new[]{negative},new[]{(instrument,down)}));
        var config=new AppConfig { PriceAlerts=new(){negative.Clone()} };
        config.Normalize();
        Assert.Single(config.PriceAlerts);
    }
}
