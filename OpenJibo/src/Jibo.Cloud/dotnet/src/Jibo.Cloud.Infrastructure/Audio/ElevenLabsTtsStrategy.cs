using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Jibo.Cloud.Application.Services;
using Jibo.Runtime.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jibo.Cloud.Infrastructure.Audio;

public sealed class ElevenLabsTtsStrategy(
    TtsOptions options,
    HttpClient httpClient,
    ILogger<ElevenLabsTtsStrategy> logger)
    : ITtsStrategy
{
    public const string StrategyName = "elevenlabs";

    public ElevenLabsTtsStrategy(TtsOptions options, HttpClient httpClient)
        : this(options, httpClient, NullLogger<ElevenLabsTtsStrategy>.Instance)
    {
    }

    public string Name => StrategyName;

    public bool CanHandle(TtsRequest request)
    {
        return options.EnableElevenLabs &&
               request.EligibleForCloudChat &&
               !string.IsNullOrWhiteSpace(request.Text) &&
               !string.IsNullOrWhiteSpace(options.ElevenLabsApiKey) &&
               !string.IsNullOrWhiteSpace(options.ElevenLabsVoiceId);
    }

    public async Task<TtsResult> SynthesizeAsync(TtsRequest request, CancellationToken cancellationToken = default)
    {
        if (!CanHandle(request))
            throw new InvalidOperationException("ElevenLabs TTS is not configured for this request.");

        var voiceId = Uri.EscapeDataString(options.ElevenLabsVoiceId!);
        var uri = new Uri($"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}");
        using var message = new HttpRequestMessage(HttpMethod.Post, uri);
        message.Headers.TryAddWithoutValidation("xi-api-key", options.ElevenLabsApiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/mpeg"));
        var payload = JsonSerializer.Serialize(new
        {
            text = request.Text,
            model_id = options.ElevenLabsModelId
        });
        message.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        logger.LogDebug("ElevenLabs TTS start voiceId={VoiceId} chars={Chars}", options.ElevenLabsVoiceId,
            request.Text.Length);

        using var response = await httpClient.SendAsync(message, cancellationToken);
        var audio = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = Encoding.UTF8.GetString(audio);
            throw new InvalidOperationException(
                $"ElevenLabs TTS failed with {(int)response.StatusCode}: {detail}");
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "audio/mpeg";
        return new TtsResult
        {
            Provider = StrategyName,
            Delivery = TtsDelivery.AudioBytes,
            Audio = audio,
            ContentType = contentType,
            VoiceId = options.ElevenLabsVoiceId
        };
    }
}
