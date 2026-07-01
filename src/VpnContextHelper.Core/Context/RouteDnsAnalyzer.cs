using VpnContextHelper.Core.Net;

namespace VpnContextHelper.Core.Context;

/// <summary>A per-interface DNS assignment (suffixes + resolver IPs). Non-secret.</summary>
public sealed record DnsAssignment(string InterfaceAlias, IReadOnlyList<string> Suffixes, IReadOnlyList<string> Servers);

public sealed record SubnetConflict(Cidr A, Cidr B);

public sealed record DnsConflict(string Suffix, string InterfaceA, string InterfaceB);

/// <summary>
/// Pure detection of the two conflict classes that matter if two VPN contexts were ever active at
/// once: overlapping routed subnets, and the same DNS suffix resolved by different servers on
/// different interfaces. Detection only — this class proposes nothing and changes nothing.
/// </summary>
public static class RouteDnsAnalyzer
{
    /// <summary>Overlapping routed subnets between the two contexts (excludes default route /0).</summary>
    public static IReadOnlyList<SubnetConflict> FindSubnetConflicts(IEnumerable<Cidr> a, IEnumerable<Cidr> b)
    {
        var listB = b.Where(c => c.PrefixLength != 0).ToList();
        var conflicts = new List<SubnetConflict>();
        foreach (var x in a.Where(c => c.PrefixLength != 0))
            foreach (var y in listB)
                if (x.Overlaps(y))
                    conflicts.Add(new SubnetConflict(x, y));
        return conflicts;
    }

    /// <summary>
    /// A conflict is the same DNS suffix advertised on two interfaces with DIFFERENT resolver sets —
    /// resolution of that suffix becomes ambiguous / order-dependent.
    /// </summary>
    public static IReadOnlyList<DnsConflict> FindDnsSuffixConflicts(IEnumerable<DnsAssignment> assignments)
    {
        var list = assignments.ToList();
        var conflicts = new List<DnsConflict>();
        for (int i = 0; i < list.Count; i++)
        {
            for (int j = i + 1; j < list.Count; j++)
            {
                var common = list[i].Suffixes
                    .Select(s => s.Trim().TrimStart('.'))
                    .Where(s => s.Length > 0)
                    .Intersect(
                        list[j].Suffixes.Select(s => s.Trim().TrimStart('.')).Where(s => s.Length > 0),
                        StringComparer.OrdinalIgnoreCase);

                foreach (var suffix in common)
                {
                    var serversI = new HashSet<string>(list[i].Servers, StringComparer.OrdinalIgnoreCase);
                    var serversJ = new HashSet<string>(list[j].Servers, StringComparer.OrdinalIgnoreCase);
                    if (!serversI.SetEquals(serversJ))
                        conflicts.Add(new DnsConflict(suffix, list[i].InterfaceAlias, list[j].InterfaceAlias));
                }
            }
        }
        return conflicts;
    }
}
