using System.Net;

namespace VpnContextHelper.Core.Net;

/// <summary>One row of the IPv4 "Active Routes" table from <c>route print -4</c>.</summary>
public sealed record RouteEntry(
    IPAddress Destination,
    IPAddress Netmask,
    string Gateway,
    IPAddress InterfaceIp,
    int Metric)
{
    private static readonly IPAddress Zero = IPAddress.Parse("0.0.0.0");

    public bool IsOnLink => string.Equals(Gateway, "On-link", StringComparison.OrdinalIgnoreCase);

    public int PrefixLength => Cidr.PrefixFromMask(Netmask);

    public Cidr ToCidr() => Cidr.FromMask(Destination, Netmask);

    public bool IsDefaultRoute => Destination.Equals(Zero) && PrefixLength == 0;

    public override string ToString()
        => $"{ToCidr()} via {(IsOnLink ? "on-link" : Gateway)} dev {InterfaceIp} metric {Metric}";
}
