using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Jibo.Cloud.Application.Services;
using Jibo.Runtime.Abstractions;
using Microsoft.Extensions.Logging;

namespace Jibo.Cloud.Infrastructure.Audio;

public sealed class LocalCloneTtsStrategy(
    TtsOptions options,
    HttpClient httpClient,
    ILogger<LocalCloneTtsStrategy> logger)
    : ITtsStrategy
{
    public const string StrategyName = "local-clone";

    public string Name => StrategyName;

    public bool CanHandle(TtsRequest request)
    {
        return options.EnableLocalClone &&
               request.EligibleForCloudChat &&
               !string.IsNullOrWhiteSpace(request.Text) &&
               !string.IsNullOrWhiteSpace(options.LocalCloneUrl);
    }

    public async Task<TtsResult> SynthesizeAsync(TtsRequest request, CancellationToken cancellationToken = default)
    {
        if (!CanHandle(request))
            throw new InvalidOperationException("Local clone TTS is not configured for this request.");

        var baseUrl = options.LocalCloneUrl.TrimEnd('/');
        var uri = new Uri($"{baseUrl}/speak");
        using var message = new HttpRequestMessage(HttpMethod.Post, uri);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/wav"));
        message.Content = new StringContent(
            JsonSerializer.Serialize(new { text = request.Text }),
            Encoding.UTF8,
            "application/json");

        logger.LogDebug("Local clone TTS start chars={Chars}", request.Text.Length);

        using var response = await httpClient.SendAsync(message, cancellationToken);
        var audio = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = Encoding.UTF8.GetString(audio);
            throw new InvalidOperationException(
                $"Local clone TTS failed with {(int)response.StatusCode}: {detail}");
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "audio/wav";
        return new TtsResult
        {
            Provider = StrategyName,
            Delivery = TtsDelivery.AudioBytes,
            Audio = audio,
            ContentType = contentType,
            VoiceId = "melissa"
        };
    }
}
