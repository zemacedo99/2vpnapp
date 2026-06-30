using VpnContextHelper.Core.Config;
using VpnContextHelper.Core.Diagnostics;
using Xunit;

namespace VpnContextHelper.Tests;

public class RedactorTests
{
    private static Redactor Default(bool maskMac = true)
        => new(new RedactionConfig { MaskMacAddresses = maskMac });

    [Theory]
    [InlineData("password = hunter2", "hunter2")]
    [InlineData("token: abc123DEF456", "abc123DEF456")]
    [InlineData("PreShared-Key=verysecretvalue", "verysecretvalue")]
    [InlineData("client_secret: \"s3cr3t\"", "s3cr3t")]
    public void MasksSensitiveKeyValues(string input, string secret)
    {
        var output = Default().Redact(input);
        Assert.DoesNotContain(secret, output);
        Assert.Contains(Redactor.Mask, output);
    }

    [Fact]
    public void PreservesIpAddressesAndRoutes()
    {
        const string evidence = "172.16.0.0/12 via 10.20.0.1 dev 10.20.0.2 metric 1";
        var output = Default().Redact(evidence);
        Assert.Equal(evidence, output); // routing evidence must survive intact
    }

    [Fact]
    public void RemovesPemBlocks()
    {
        const string pem = "before\n-----BEGIN CERTIFICATE-----\nMIIabc\nDEFghi\n-----END CERTIFICATE-----\nafter";
        var output = Default().Redact(pem);
        Assert.DoesNotContain("MIIabc", output);
        Assert.Contains("before", output);
        Assert.Contains("after", output);
    }

    [Fact]
    public void MasksAuthorizationHeader()
    {
        var output = Default().Redact("Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9");
        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9", output);
        Assert.Contains(Redactor.Mask, output);
    }

    [Fact]
    public void MasksLongOpaqueBlobs()
    {
        var blob = new string('A', 50);
        var output = Default().Redact($"sessionvalue {blob} end");
        Assert.DoesNotContain(blob, output);
    }

    [Fact]
    public void MacAddress_MaskedOrPreservedPerConfig()
    {
        const string line = "Physical Address. . . : 02-00-5E-00-00-01";
        Assert.Contains("[MAC]", Default(maskMac: true).Redact(line));
        Assert.Contains("02-00-5E-00-00-01", Default(maskMac: false).Redact(line));
    }

    [Fact]
    public void MacAddress_SpaceSeparatedForm_AlsoMasked()
    {
        // route print "Interface List" uses space-separated MAC bytes.
        const string line = " 10...02 00 5e 00 00 01 ......Fortinet SSL VPN Virtual Ethernet Adapter";
        var output = Default(maskMac: true).Redact(line);
        Assert.Contains("[MAC]", output);
        Assert.DoesNotContain("02 00 5e 00 00 01", output);
        Assert.Contains("Fortinet SSL VPN", output); // adapter evidence preserved
    }

    [Fact]
    public void MasksConfiguredLiterals()
    {
        var redactor = new Redactor(new RedactionConfig(), literalsToMask: new[] { "jdoe" });
        var output = redactor.Redact(@"C:\Users\jdoe\config and user jdoe");
        Assert.DoesNotContain("jdoe", output);
    }

    [Fact]
    public void PreservesLongHostnamesAndPaths()
    {
        // Hostnames with hyphens and Windows paths with underscores/hyphens are evidence, not secrets.
        const string text = "host=git-server-prod-eu-west-1.internal.corp.example.com path=C:\\Builds\\my_project-v2\\bin";
        var output = Default().Redact(text);
        Assert.Contains("git-server-prod-eu-west-1.internal.corp.example.com", output);
        Assert.Contains("my_project-v2", output);
        Assert.DoesNotContain(Redactor.Mask, output);
    }

    [Fact]
    public void MasksBareJwt()
    {
        const string jwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0In0.SflKxwRJSMeKKF2QT4";
        var output = Default().Redact($"id_token {jwt} done");
        Assert.DoesNotContain("eyJzdWIiOiIxMjM0In0", output);
        Assert.Contains(Redactor.Mask, output);
    }

    [Fact]
    public void MasksBearerTokenWithSpaceSeparator()
    {
        var output = Default().Redact("X-Auth: Bearer abc.def.ghijklmnop");
        Assert.DoesNotContain("abc.def.ghijklmnop", output);
        Assert.Contains(Redactor.Mask, output);
    }

    [Fact]
    public void MasksUnquotedMultiWordSecretToEndOfLine()
    {
        var output = Default().Redact("passphrase: correct horse battery staple");
        Assert.DoesNotContain("horse", output);
        Assert.DoesNotContain("staple", output);
        Assert.Contains(Redactor.Mask, output);
    }

    [Fact]
    public void EmptyInput_Unchanged()
        => Assert.Equal("", Default().Redact(""));
}
