namespace VpnContextHelper.Core.Probes;

public enum ProbeKind
{
    Tcp,
    Dns,
    Icmp,
}

/// <summary>
/// Result of a single read-only reachability check. Never contains payload or secrets — only
/// whether a TCP handshake / DNS lookup succeeded, how long it took, and a short status word.
/// </summary>
public sealed record ProbeResult(
    string Name,
    string Target,
    ProbeKind Kind,
    bool Ok,
    long LatencyMs,
    string Detail)
{
    /// <summary>
    /// The access path the user EXPECTED for this target (from config: "ztna"|"vpn"|"lan"|
    /// "internet"). This is a label only — the probe measures reachability, not the path taken.
    /// </summary>
    public string? ExpectVia { get; init; }

    public string Glyph => Ok ? "OK " : "ERR";

    public override string ToString()
        => $"[{Glyph}] {Name} ({Kind} {Target}) {LatencyMs}ms — {Detail}";
}
