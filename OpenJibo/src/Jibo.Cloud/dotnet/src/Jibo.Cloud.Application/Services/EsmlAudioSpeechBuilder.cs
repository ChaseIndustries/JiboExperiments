using System.Text.RegularExpressions;

namespace Jibo.Cloud.Application.Services;

public static class EsmlAudioSpeechBuilder
{
    private static readonly Regex SpokenEsElement = new(
        @"<es\b[^>]*>.*?</es>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static readonly Regex TagOrText = new(
        @"<[^>]+>|[^<]+",
        RegexOptions.CultureInvariant);

    public static string ForRemoteAudio(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Audio URL is required.", nameof(url));

        return $"<speak><audio src='{Encode(url)}' /></speak>";
    }

    public static string ForSilence() => "<speak><break time='1ms'/></speak>";

    public static string SilentSpokenInner => "<break time='1ms'/>";

    public static string SpokenInner(bool muteSpoken, string escapedText) =>
        muteSpoken ? SilentSpokenInner : escapedText;

    public static string MuteSpoken(string? esml)
    {
        if (string.IsNullOrWhiteSpace(esml))
            return ForSilence();

        if (SpokenEsElement.IsMatch(esml))
            return SilenceSpokenElements(esml);

        return StripRawSpeechKeepMarkup(esml);
    }

    public static string SilenceSpokenElements(string esml)
    {
        if (string.IsNullOrWhiteSpace(esml) || !SpokenEsElement.IsMatch(esml))
            return ForSilence();

        return SpokenEsElement.Replace(esml, static match =>
        {
            var openEnd = match.Value.IndexOf('>', StringComparison.Ordinal);
            if (openEnd < 0)
                return "<es><break time='1ms'/></es>";

            return match.Value[..(openEnd + 1)] + $"{SilentSpokenInner}</es>";
        });
    }

    private static string StripRawSpeechKeepMarkup(string esml)
    {
        var parts = new List<string>();
        var keptMarkup = false;
        foreach (Match match in TagOrText.Matches(esml))
        {
            var value = match.Value;
            if (value.StartsWith('<'))
            {
                parts.Add(value);
                if (!value.StartsWith("<speak", StringComparison.OrdinalIgnoreCase) &&
                    !value.StartsWith("</speak", StringComparison.OrdinalIgnoreCase))
                    keptMarkup = true;
                continue;
            }

            if (string.IsNullOrWhiteSpace(value))
                parts.Add(value);
        }

        if (!keptMarkup)
            return ForSilence();

        var body = string.Concat(parts).Trim();
        if (!body.Contains("<speak", StringComparison.OrdinalIgnoreCase))
            return $"<speak>{SilentSpokenInner}{body}</speak>";

        if (body.Contains(SilentSpokenInner, StringComparison.Ordinal))
            return body;

        return body.Replace("<speak>", $"<speak>{SilentSpokenInner}", StringComparison.OrdinalIgnoreCase);
    }

    private static string Encode(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }
}
