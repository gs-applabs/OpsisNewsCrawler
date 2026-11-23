namespace OpsisNewsCrawler.Core;

/// <summary>Strict media launch targets and attribution; integration is optional and does not fetch copyrighted media.</summary>
public sealed record MediaSuiteAsset(string SourceUrl, string DisplayTitle, string? Attribution, string? Mime);

public static class MediaSuiteBridge
{
    public static bool IsSafeRemoteAsset(MediaSuiteAsset asset)
    {
        if(!Uri.TryCreate(asset.SourceUrl,UriKind.Absolute,out var uri))return false;
        if(uri.Scheme!=Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(asset.DisplayTitle))return false;
        var host=uri.Host.ToLowerInvariant();
        if(host=="localhost"||host.EndsWith(".localhost")||host.EndsWith(".local")
          ||System.Net.IPAddress.TryParse(host,out var address) &&
             (System.Net.IPAddress.IsLoopback(address)||address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork &&
                (address.GetAddressBytes()[0] is 10 or 127 or 0)))return false;
        return asset.Mime is null or "image/jpeg" or "image/png" or "image/webp" or "video/mp4" or "text/html";
    }

    public static MediaSuiteAsset? FromNews(NewsItem item)
    {
        var raw=string.IsNullOrWhiteSpace(item.ImageUrl)?item.Url:item.ImageUrl;
        if(string.IsNullOrWhiteSpace(raw))return null;
        var asset=new MediaSuiteAsset(raw,item.Title,item.Source,null);
        return IsSafeRemoteAsset(asset)?asset:null;
    }
}
