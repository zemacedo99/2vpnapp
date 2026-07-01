using System.Text.RegularExpressions;
using Xunit;

namespace VpnContextHelper.Tests;

/// <summary>
/// Guard test: committed docs and the sample config must not contain real internal values.
/// <para>
/// This file itself carries NO real identifiers. It applies (a) a structural check (no MAC-shaped
/// tokens in committed text) and (b) an optional deny-list loaded from the git-ignored
/// <c>config/sensitive-tokens.local</c> — so the real tokens live only in an ignored file, never in
/// source control. On a fresh clone/CI without that file, the deny-list check is skipped.
/// </para>
/// </summary>
public class NoSecretsCommittedTests
{
    // Any MAC address shape (real or synthetic). Committed docs/README/sample should contain none.
    private static readonly Regex AnyMac =
        new(@"\b(?:[0-9A-Fa-f]{2}[:\-]){5}[0-9A-Fa-f]{2}\b", RegexOptions.Compiled);

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "VpnContextHelper.slnx"))) return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    private static IEnumerable<string> ScannedFiles(string root)
    {
        var sample = Path.Combine(root, "config", "config.sample.json");
        if (File.Exists(sample)) yield return sample;
        var readme = Path.Combine(root, "README.md");
        if (File.Exists(readme)) yield return readme;
        var docs = Path.Combine(root, "docs");
        if (Directory.Exists(docs))
            foreach (var f in Directory.GetFiles(docs, "*.md", SearchOption.AllDirectories))
                yield return f;
    }

    [Fact]
    public void CommittedDocs_ContainNoMacAddresses()
    {
        var root = FindRepoRoot();
        Assert.False(root is null, "Could not locate repo root (VpnContextHelper.slnx).");

        var offenders = ScannedFiles(root!)
            .Where(f => AnyMac.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(root!, f)) // path only — never print the matched value
            .ToList();

        Assert.True(offenders.Count == 0, "MAC-shaped tokens found in committed files: " + string.Join(", ", offenders));
    }

    [Fact]
    public void CommittedDocs_ContainNoDenyListedValues_WhenDenyListPresent()
    {
        var root = FindRepoRoot();
        Assert.False(root is null);

        // Deny-list of real tokens (one regex per line) lives ONLY in this git-ignored file.
        var denyFile = Path.Combine(root!, "config", "sensitive-tokens.local");
        if (!File.Exists(denyFile)) return; // absent on fresh clone/CI — nothing to check against

        var patterns = File.ReadAllLines(denyFile)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Select(l => new Regex(l, RegexOptions.IgnoreCase))
            .ToList();
        if (patterns.Count == 0) return;

        var offenders = new List<string>();
        foreach (var f in ScannedFiles(root!))
        {
            var text = File.ReadAllText(f);
            if (patterns.Any(p => p.IsMatch(text)))
                offenders.Add(Path.GetRelativePath(root!, f));
        }

        Assert.True(offenders.Count == 0,
            "Real internal values (from local deny-list) found in committed files: " + string.Join(", ", offenders));
    }

    [Fact]
    public void SampleConfig_IsNotTheRealConfig()
    {
        var root = FindRepoRoot();
        Assert.False(root is null);
        var sample = Path.Combine(root!, "config", "config.sample.json");
        Assert.True(File.Exists(sample), "config.sample.json must exist as the placeholder template.");
        Assert.Contains("example", File.ReadAllText(sample), StringComparison.OrdinalIgnoreCase);
    }
}
