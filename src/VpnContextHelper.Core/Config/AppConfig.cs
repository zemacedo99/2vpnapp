using System.Text.Json;
using System.Text.Json.Serialization;

namespace VpnContextHelper.Core.Config;

/// <summary>
/// User-editable configuration. All endpoints/subnets live here — nothing is hardcoded.
/// Loaded from a JSON file; see config/config.sample.json.
/// </summary>
public sealed class AppConfig
{
    public int PollIntervalSeconds { get; set; } = 30;
    public int ProbeTimeoutMs { get; set; } = 2000;
    public string FortiClientPath { get; set; } = @"C:\Program Files\Fortinet\FortiClient\FortiClient.exe";

    public TrustedNetworkConfig TrustedNetwork { get; set; } = new();
    public AccessGroup CompanyAccess { get; set; } = new() { Label = "Company" };
    public AccessGroup ClientAccess { get; set; } = new() { Label = "Client" };
    public RedactionConfig Redaction { get; set; } = new();

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static AppConfig FromJson(string json)
        => JsonSerializer.Deserialize<AppConfig>(json, Options) ?? new AppConfig();

    public static AppConfig Load(string path)
        => FromJson(File.ReadAllText(path));

    public string ToJson() => JsonSerializer.Serialize(this, Options);
}

public sealed class TrustedNetworkConfig
{
    /// <summary>If any connected adapter advertises one of these DNS suffixes, treat as on-net.</summary>
    public List<string> DnsSuffixes { get; set; } = new();

    /// <summary>Hosts that are only routable on the trusted/office LAN (used as a presence beacon).</summary>
    public List<Beacon> Beacons { get; set; } = new();
}

public sealed class Beacon
{
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 443;
}

public sealed class AccessGroup
{
    public string Label { get; set; } = string.Empty;

    /// <summary>FortiClient VPN profile name for this group, if any (display only — never automated).</summary>
    public string? VpnProfileName { get; set; }

    /// <summary>Internal CIDRs belonging to this group; used to detect tunnel/route overlaps.</summary>
    public List<string> InternalCidrs { get; set; } = new();

    public List<Target> Targets { get; set; } = new();
}

public sealed class Target
{
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 443;

    /// <summary>Expected access path: "ztna" | "vpn" | "lan" | "internet". Informational only.</summary>
    public string? ExpectVia { get; set; }
}

public sealed class RedactionConfig
{
    public bool MaskMacAddresses { get; set; } = true;
    public bool MaskUsernames { get; set; } = true;

    /// <summary>Extra regex patterns whose matches are replaced with [REDACTED].</summary>
    public List<string> ExtraPatterns { get; set; } = new();
}
