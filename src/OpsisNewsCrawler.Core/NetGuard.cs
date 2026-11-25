using System.Net;
using System.Net.Sockets;

namespace OpsisNewsCrawler.Core;

/// <summary>
/// Small safety checks for addresses that come from downloaded content (feed items, image links, site icons):
/// such content must not make the program talk to the user's own machine or local network.
/// </summary>
public static class NetGuard
{
    /// <summary>False for localhost, single-label names, .local/.internal names and private, loopback or link-local IP addresses.</summary>
    public static bool IsPublicHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host)) return false;
        host = host.Trim().TrimEnd('.').ToLowerInvariant();
        if (host.StartsWith('[') && host.EndsWith(']')) host = host[1..^1];
        if (host == "localhost" || host.EndsWith(".localhost") || host.EndsWith(".local") || host.EndsWith(".internal") || host.EndsWith(".lan")) return false;
        if (IPAddress.TryParse(host, out var ip)) return IsPublic(ip);
        return host.Contains('.');
    }

    public static bool IsPublicHttpUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && IsPublicHost(uri.Host);

    private static bool IsPublic(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any)) return false;
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var v6 = ip.GetAddressBytes();
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast || (v6[0] & 0xFE) == 0xFC) return false;
            // IPv4 hidden inside an IPv6 address: IPv4-compatible (::a.b.c.d), NAT64 (64:ff9b::/96) and 6to4 (2002::/16) must obey the IPv4 rules.
            var zeros = v6.Take(12).All(x => x == 0);
            if (zeros) return IsPublic(new IPAddress(v6.Skip(12).ToArray()));
            if (v6[0] == 0x00 && v6[1] == 0x64 && v6[2] == 0xFF && v6[3] == 0x9B && v6.Skip(4).Take(8).All(x => x == 0)) return IsPublic(new IPAddress(v6.Skip(12).ToArray()));
            if (v6[0] == 0x20 && v6[1] == 0x02) return IsPublic(new IPAddress(v6.Skip(2).Take(4).ToArray()));
            return true;
        }
        var b = ip.GetAddressBytes();
        return !(b[0] == 10 || b[0] == 127 || b[0] == 0 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168) ||
                 (b[0] == 169 && b[1] == 254) || (b[0] == 100 && b[1] is >= 64 and <= 127) || (b[0] == 192 && b[1] == 0 && b[2] == 0) || (b[0] == 198 && b[1] is 18 or 19) || b[0] >= 224);
    }
}
