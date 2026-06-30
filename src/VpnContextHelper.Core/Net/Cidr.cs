using System.Net;
using System.Net.Sockets;

namespace VpnContextHelper.Core.Net;

/// <summary>
/// An immutable IPv4 CIDR block. Used to reason about which destination subnets a VPN
/// tunnel carries and whether two ranges overlap (e.g. a client split-tunnel range
/// shadowing a company internal range). Pure value type — no I/O.
/// </summary>
public readonly struct Cidr : IEquatable<Cidr>
{
    public uint Network { get; }
    public int PrefixLength { get; }

    public Cidr(uint network, int prefixLength)
    {
        if (prefixLength is < 0 or > 32)
            throw new ArgumentOutOfRangeException(nameof(prefixLength));
        PrefixLength = prefixLength;
        Network = network & MaskFor(prefixLength);
    }

    public static uint MaskFor(int prefix) => prefix == 0 ? 0u : 0xFFFFFFFFu << (32 - prefix);

    public static Cidr Parse(string text)
    {
        if (!TryParse(text, out var cidr))
            throw new FormatException($"Invalid IPv4 CIDR: '{text}'");
        return cidr;
    }

    public static bool TryParse(string? text, out Cidr cidr)
    {
        cidr = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var parts = text.Trim().Split('/');
        if (parts.Length is < 1 or > 2) return false;
        if (!IPAddress.TryParse(parts[0], out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
            return false;
        int prefix = 32;
        if (parts.Length == 2 && !int.TryParse(parts[1], out prefix)) return false;
        if (prefix is < 0 or > 32) return false;
        cidr = new Cidr(ToUInt(ip), prefix);
        return true;
    }

    public static Cidr FromMask(IPAddress network, IPAddress mask)
        => new(ToUInt(network), PrefixFromMask(mask));

    /// <summary>Counts the leading 1-bits of a dotted-decimal subnet mask.</summary>
    public static int PrefixFromMask(IPAddress mask)
    {
        uint m = ToUInt(mask);
        int count = 0;
        for (int i = 31; i >= 0; i--)
        {
            if ((m & (1u << i)) != 0) count++;
            else break;
        }
        return count;
    }

    public bool Contains(IPAddress ip)
    {
        if (ip.AddressFamily != AddressFamily.InterNetwork) return false;
        uint v = ToUInt(ip);
        return (v & MaskFor(PrefixLength)) == Network;
    }

    /// <summary>True if this block and <paramref name="other"/> share any address.</summary>
    public bool Overlaps(Cidr other)
    {
        int shorter = Math.Min(PrefixLength, other.PrefixLength);
        uint mask = MaskFor(shorter);
        return (Network & mask) == (other.Network & mask);
    }

    public static uint ToUInt(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
    }

    public IPAddress NetworkAddress => new(new byte[]
    {
        (byte)(Network >> 24), (byte)(Network >> 16), (byte)(Network >> 8), (byte)Network,
    });

    public override string ToString() => $"{NetworkAddress}/{PrefixLength}";
    public bool Equals(Cidr other) => Network == other.Network && PrefixLength == other.PrefixLength;
    public override bool Equals(object? obj) => obj is Cidr c && Equals(c);
    public override int GetHashCode() => HashCode.Combine(Network, PrefixLength);
}

/// <summary>Helpers for reasoning about sets of CIDR blocks.</summary>
public static class CidrTools
{
    /// <summary>
    /// Returns the pairs of (tunneled, companyInternal) ranges that overlap. An overlap means
    /// that while the client VPN is connected, traffic to the company range is being captured by
    /// the client tunnel — a routing conflict worth flagging to IT.
    /// </summary>
    public static IReadOnlyList<(Cidr Tunneled, Cidr Company)> FindOverlaps(
        IEnumerable<Cidr> tunneled, IEnumerable<Cidr> companyInternal)
    {
        var company = companyInternal.ToList();
        var result = new List<(Cidr, Cidr)>();
        foreach (var t in tunneled)
            foreach (var c in company)
                if (t.Overlaps(c))
                    result.Add((t, c));
        return result;
    }
}
