using System.Net.Http.Json;
using Jibo.Runtime.Abstractions;

namespace Jibo.Cloud.Infrastructure.Audio;

public sealed class GriffinRobotTtsClient(HttpClient httpClient)
{
    public async Task<TtsResult> SpeakAsync(string jiboIp, string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jiboIp);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var uri = new Uri($"http://{jiboIp}:8089/tts_speak");
        var payload = new
        {
            prompt = text,
            locale = "en-us",
            voice = "griffin",
            mode = "text",
            outputMode = "stream"
        };

        using var response = await httpClient.PostAsJsonAsync(uri, payload, cancellationToken);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        var isAudio = contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ||
                      LooksLikeWave(bytes) ||
                      LooksLikeMpeg(bytes);

        return new TtsResult
        {
            Provider = "griffin",
            Delivery = isAudio ? TtsDelivery.AudioBytes : TtsDelivery.OnRobotEsml,
            Audio = isAudio ? bytes : [],
            ContentType = contentType,
            VoiceId = "griffin"
        };
    }

    private static bool LooksLikeWave(byte[] bytes)
    {
        return bytes.Length >= 12 &&
               bytes[0] == (byte)'R' &&
               bytes[1] == (byte)'I' &&
               bytes[2] == (byte)'F' &&
               bytes[3] == (byte)'F';
    }

    private static bool LooksLikeMpeg(byte[] bytes)
    {
        return bytes.Length >= 3 &&
               bytes[0] == 0xFF &&
               (bytes[1] & 0xE0) == 0xE0;
    }
}
