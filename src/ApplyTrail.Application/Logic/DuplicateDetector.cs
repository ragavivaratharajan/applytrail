using System.Text;

namespace ApplyTrail.Application.Logic;

public enum DuplicateReason { None, SameLink, SameCompanyAndRole }

/// <summary>
/// Spots "you already applied to this" before it becomes an awkward second application.
/// Deliberately forgiving about tracking links and company suffixes, deliberately strict about
/// seniority (Software Engineer and Senior Software Engineer are different jobs).
/// </summary>
public static class DuplicateDetector
{
    public const double RoleThreshold = 0.8;

    private static readonly HashSet<string> CompanySuffixes = new(StringComparer.Ordinal)
    {
        "ltd", "limited", "inc", "incorporated", "llc", "llp", "plc", "gmbh", "corp", "corporation",
        "co", "company", "dac", "uc", "ireland",
    };

    private static readonly HashSet<string> RoleStopWords = new(StringComparer.Ordinal)
    {
        "a", "an", "the", "of", "and", "for", "at", "in", "to", "with",
    };

    // Parameters that only describe how you reached the page, never which job it is.
    private static readonly HashSet<string> TrackingParams = new(StringComparer.Ordinal)
    {
        "gclid", "fbclid", "msclkid", "igshid", "mc_cid", "mc_eid", "trk", "trackingid", "refid", "trkemail",
        "lipi", "midtoken", "midsig", "eid", "ebp", "src", "source", "ref", "referrer", "referral",
        "gh_src", "lever-source", "lever-origin", "_hsenc", "_hsmi", "from", "vjs",
    };

    public static DuplicateReason Compare(
        string company, string role, string? url,
        string otherCompany, string otherRole, string? otherUrl)
    {
        var a = NormalizeUrl(url);
        if (a.Length > 0 && a == NormalizeUrl(otherUrl))
        {
            return DuplicateReason.SameLink;
        }

        var companyKey = NormalizeCompany(company);
        if (companyKey.Length > 0
            && companyKey == NormalizeCompany(otherCompany)
            && RoleSimilarity(role, otherRole) >= RoleThreshold)
        {
            return DuplicateReason.SameCompanyAndRole;
        }

        return DuplicateReason.None;
    }

    /// <summary>Host + path + the query parameters that identify the job, in a stable order.</summary>
    public static string NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return "";
        }

        var trimmed = url.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return trimmed.ToLowerInvariant();
        }

        var host = uri.Host.ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }

        var path = uri.AbsolutePath.TrimEnd('/');

        var kept = new List<string>();
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var key = Uri.UnescapeDataString(separator < 0 ? pair : pair[..separator]).ToLowerInvariant();
            if (key.StartsWith("utm_", StringComparison.Ordinal) || TrackingParams.Contains(key))
            {
                continue;
            }

            var value = separator < 0 ? "" : Uri.UnescapeDataString(pair[(separator + 1)..]);
            kept.Add($"{key}={value}");
        }

        kept.Sort(StringComparer.Ordinal);
        return kept.Count == 0 ? host + path : $"{host}{path}?{string.Join('&', kept)}";
    }

    /// <summary>"Stripe Ireland Ltd." and "stripe" are the same employer.</summary>
    public static string NormalizeCompany(string? company)
    {
        if (string.IsNullOrWhiteSpace(company))
        {
            return "";
        }

        var tokens = Tokenize(company.ToLowerInvariant().Replace("&", " and "));
        while (tokens.Count > 1 && CompanySuffixes.Contains(tokens[^1]))
        {
            tokens.RemoveAt(tokens.Count - 1);
        }

        return string.Join(' ', tokens);
    }

    /// <summary>Jaccard similarity of the meaningful words in two job titles, from 0 to 1.</summary>
    public static double RoleSimilarity(string? a, string? b)
    {
        var left = RoleTokens(a);
        var right = RoleTokens(b);
        if (left.Count == 0 || right.Count == 0)
        {
            return 0;
        }

        var shared = left.Intersect(right).Count();
        var total = left.Union(right).Count();
        return (double)shared / total;
    }

    private static HashSet<string> RoleTokens(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return [];
        }

        // Punctuation carries meaning in this field: C# and C++ are not the same language.
        var text = role.ToLowerInvariant()
            .Replace("c#", " csharp ")
            .Replace("f#", " fsharp ")
            .Replace("c++", " cpp ")
            .Replace(".net", " dotnet ");

        return Tokenize(text).Where(t => !RoleStopWords.Contains(t)).ToHashSet(StringComparer.Ordinal);
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();

        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                current.Append(ch);
            }
            else if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }
}
