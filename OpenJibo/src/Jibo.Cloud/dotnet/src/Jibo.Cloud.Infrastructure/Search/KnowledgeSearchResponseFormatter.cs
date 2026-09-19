using System.Text.RegularExpressions;

namespace Jibo.Cloud.Infrastructure.Search;

internal static partial class KnowledgeSearchResponseFormatter
{
    private const string LinkUrl = @"[^\s()]*(?:\([^\s()]*\)[^\s()]*)*";

    public static string NormalizeForSpeech(string answerText)
    {
        if (string.IsNullOrWhiteSpace(answerText)) return string.Empty;

        var normalized = answerText.Trim();
        normalized = ParentheticalCitationPattern().Replace(normalized, string.Empty);
        normalized = MarkdownLinkPattern().Replace(normalized, "$1");
        normalized = BareUrlPattern().Replace(normalized, string.Empty);
        normalized = BoldPattern().Replace(normalized, "$1");
        normalized = WhitespacePattern().Replace(normalized, " ");
        return normalized.Trim();
    }

    [GeneratedRegex(
        @"\s*\(\s*(?:\[[^\]]*\]\(" + LinkUrl + @"\)\s*[,;]?\s*)+\)",
        RegexOptions.CultureInvariant)]
    private static partial Regex ParentheticalCitationPattern();

    [GeneratedRegex(@"\[([^\]]+)\]\(" + LinkUrl + @"\)", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownLinkPattern();

    [GeneratedRegex(@"https?://[^\s)]+", RegexOptions.CultureInvariant)]
    private static partial Regex BareUrlPattern();

    [GeneratedRegex(@"\*\*(.+?)\*\*", RegexOptions.CultureInvariant)]
    private static partial Regex BoldPattern();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespacePattern();
}
