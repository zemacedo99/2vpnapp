using VpnContextHelper.Core.Net;

namespace VpnContextHelper.Core.Diagnostics;

/// <summary>Read-only facts gathered about the machine, fed into the report builders.</summary>
public sealed class DiagnosticData
{
    public DateTimeOffset GeneratedAt { get; set; }
    public string ToolVersion { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string OsCaption { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string FortiClientVersion { get; set; } = string.Empty;

    public IReadOnlyList<AdapterInfo> Adapters { get; set; } = Array.Empty<AdapterInfo>();
    public IReadOnlyList<RouteEntry> Routes { get; set; } = Array.Empty<RouteEntry>();

    public string RawRoutePrint { get; set; } = string.Empty;
    public string RawIpConfig { get; set; } = string.Empty;
}
