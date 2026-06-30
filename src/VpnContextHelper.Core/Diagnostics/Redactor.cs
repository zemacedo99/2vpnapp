using System.Text.RegularExpressions;
using VpnContextHelper.Core.Config;

namespace VpnContextHelper.Core.Diagnostics;

/// <summary>
/// Scrubs anything that looks like a secret from text before it is written to a log or the IT
/// diagnostic report.
/// <para>
/// IMPORTANT: IP addresses, routes, subnet masks and adapter descriptions are intentionally
/// PRESERVED — they are the evidence IT needs. Only credentials, tokens, cookies, pre-shared
/// keys, SAML assertions, certificate/PEM material and (optionally) MAC addresses / the local
/// username are removed.
/// </para>
/// </summary>
public sealed class Redactor
{
    public const string Mask = "[REDACTED]";

    private readonly RedactionConfig _config;
    private readonly List<Regex> _extra;
    private readonly List<Regex> _literals;

    public Redactor(RedactionConfig? config = null, IEnumerable<string>? literalsToMask = null)
    {
        _config = config ?? new RedactionConfig();
        _extra = _config.ExtraPatterns
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled))
            .ToList();
        _literals = (literalsToMask ?? Enumerable.Empty<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s) && s.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(s => new Regex(Regex.Escape(s), RegexOptions.IgnoreCase | RegexOptions.Compiled))
            .ToList();
    }

    // key=value / key: value where the key is sensitive. Keeps the key, masks the WHOLE value
    // (to end of line for unquoted values, so multi-word secrets do not leak their tail).
    private static readonly Regex KeyValue = new(
        @"(?i)\b(pass(?:word|wd)?|secret|token|cookie|authorization|api[_-]?key|bearer|psk|pre-?shared(?:[_-]?key)?|assertion|samlresponse|saml|private[_-]?key|passphrase|client[_-]?secret|session[_-]?id|credential)\b\s*[:=]\s*(?:""[^""]*""|'[^']*'|[^\r\n]+)",
        RegexOptions.Compiled);

    private static readonly Regex PemBlock = new(
        @"-----BEGIN [^-]+-----.*?-----END [^-]+-----",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex AuthHeader = new(
        @"(?im)^\s*Authorization\s*:\s*.+$",
        RegexOptions.Compiled);

    // "Bearer <token>" / "Basic <token>" with a whitespace separator (no :/= delimiter).
    private static readonly Regex BearerBasic = new(
        @"(?i)\b(Bearer|Basic)\s+[A-Za-z0-9._/+=\-]{8,}",
        RegexOptions.Compiled);

    // JSON Web Tokens — dot-separated segments each shorter than the LongBlob threshold.
    private static readonly Regex Jwt = new(
        @"\beyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\b",
        RegexOptions.Compiled);

    // Long opaque base64/hex blobs (>= 40 chars) — likely keys/tokens. The char class deliberately
    // EXCLUDES '-' and '_' so that hostnames, Windows paths and GUID/DUID evidence survive intact.
    private static readonly Regex LongBlob = new(
        @"(?<![A-Za-z0-9+/=])[A-Za-z0-9+/=]{40,}(?![A-Za-z0-9+/=])",
        RegexOptions.Compiled);

    // Colon/hyphen-separated (ipconfig "Physical Address") and space-separated (route print
    // "Interface List") MAC forms.
    private static readonly Regex MacAddress = new(
        @"\b(?:[0-9A-Fa-f]{2}[:\-]){5}[0-9A-Fa-f]{2}\b",
        RegexOptions.Compiled);

    private static readonly Regex MacAddressSpaced = new(
        @"\b(?:[0-9A-Fa-f]{2} ){5}[0-9A-Fa-f]{2}\b",
        RegexOptions.Compiled);

    public string Redact(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var s = input;
        s = PemBlock.Replace(s, $"{Mask}(PEM-block)");
        s = AuthHeader.Replace(s, $"Authorization: {Mask}");
        s = BearerBasic.Replace(s, m => $"{m.Groups[1].Value} {Mask}");
        s = Jwt.Replace(s, Mask);
        s = KeyValue.Replace(s, m =>
        {
            int sep = m.Value.IndexOfAny(new[] { ':', '=' });
            var key = sep > 0 ? m.Value[..sep].TrimEnd() : m.Value;
            return $"{key}={Mask}";
        });
        s = LongBlob.Replace(s, Mask);
        if (_config.MaskMacAddresses)
        {
            s = MacAddress.Replace(s, "[MAC]");
            s = MacAddressSpaced.Replace(s, "[MAC]");
        }
        foreach (var rx in _literals) s = rx.Replace(s, Mask);
        foreach (var rx in _extra) s = rx.Replace(s, Mask);
        return s;
    }
}
