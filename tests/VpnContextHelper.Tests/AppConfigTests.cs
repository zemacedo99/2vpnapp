using VpnContextHelper.Core.Config;
using Xunit;

namespace VpnContextHelper.Tests;

public class AppConfigTests
{
    private const string Json = """
        {
          "pollIntervalSeconds": 45,
          "companyAccess": {
            "label": "Acme Corp",
            "internalCidrs": ["10.40.0.0/16"],
            "targets": [
              { "name": "Bitbucket", "host": "bitbucket.example.local", "port": 443, "expectVia": "ztna" }
            ]
          },
          "clientAccess": {
            "label": "Client X",
            "vpnProfileName": "Client VPN",
            "targets": [
              { "name": "App", "host": "app.client.example", "port": 8443 }
            ]
          }
        }
        """;

    [Fact]
    public void Deserializes_AndAppliesValues()
    {
        var cfg = AppConfig.FromJson(Json);
        Assert.Equal(45, cfg.PollIntervalSeconds);
        Assert.Equal("Acme Corp", cfg.CompanyAccess.Label);
        Assert.Equal("10.40.0.0/16", Assert.Single(cfg.CompanyAccess.InternalCidrs));

        var target = Assert.Single(cfg.CompanyAccess.Targets);
        Assert.Equal("Bitbucket", target.Name);
        Assert.Equal(443, target.Port);
        Assert.Equal("ztna", target.ExpectVia);

        Assert.Equal("Client VPN", cfg.ClientAccess.VpnProfileName);
        Assert.Equal(8443, cfg.ClientAccess.Targets[0].Port);
    }

    [Fact]
    public void AppliesDefaults_ForMissingFields()
    {
        var cfg = AppConfig.FromJson(Json);
        Assert.Equal(2000, cfg.ProbeTimeoutMs);          // default
        Assert.True(cfg.Redaction.MaskMacAddresses);     // default
        Assert.EndsWith("FortiClient.exe", cfg.FortiClientPath);
    }

    [Fact]
    public void IsCaseInsensitive_AndTolerant()
    {
        // PascalCase keys, trailing comma, and a // comment all accepted.
        const string loose = """
            {
              "PollIntervalSeconds": 10, // tweak
              "CompanyAccess": { "Label": "X", },
            }
            """;
        var cfg = AppConfig.FromJson(loose);
        Assert.Equal(10, cfg.PollIntervalSeconds);
        Assert.Equal("X", cfg.CompanyAccess.Label);
    }

    [Fact]
    public void RoundTrips()
    {
        var cfg = AppConfig.FromJson(Json);
        var again = AppConfig.FromJson(cfg.ToJson());
        Assert.Equal(cfg.PollIntervalSeconds, again.PollIntervalSeconds);
        Assert.Equal(cfg.CompanyAccess.Label, again.CompanyAccess.Label);
        Assert.Equal(cfg.ClientAccess.Targets[0].Port, again.ClientAccess.Targets[0].Port);
    }
}
