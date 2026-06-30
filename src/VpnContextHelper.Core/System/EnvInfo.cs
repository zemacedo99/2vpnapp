using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VpnContextHelper.Core.SystemAccess;

/// <summary>Read-only environment facts for the diagnostic header.</summary>
public static class EnvInfo
{
    public static string OsCaption => RuntimeInformation.OSDescription;

    public static string OsVersion => Environment.OSVersion.VersionString;

    public static string MachineName
    {
        get
        {
            try { return Environment.MachineName; }
            catch { return "(unknown)"; }
        }
    }

    public static string FortiClientVersion(string fortiClientPath)
    {
        try
        {
            if (File.Exists(fortiClientPath))
            {
                var vi = FileVersionInfo.GetVersionInfo(fortiClientPath);
                return vi.ProductVersion ?? vi.FileVersion ?? "(unknown)";
            }
        }
        catch
        {
            // ignore — version is best-effort
        }
        return "(not found)";
    }
}
