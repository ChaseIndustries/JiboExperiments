using Jibo.Runtime.Abstractions;
using Microsoft.Extensions.Logging;

namespace Jibo.Cloud.Application.Services;

public sealed class CloudChatTtsCoordinator(
    ITtsStrategySelector selector,
    TtsOptions options,
    ITtsClipCache clipCache,
    IRobotKitchenAudioPlayer? kitchenPlayer = null,
    ILogger<CloudChatTtsCoordinator>? logger = null)
{
    public async Task ApplyAsync(ResponsePlan plan, CancellationToken cancellationToken = default)
    {
        if (!options.ShouldCapture)
            return;

        var kitchenEnabled = kitchenPlayer is { IsEnabled: true };
        if (!(kitchenEnabled ? CloudChatTtsGate.IsKitchenEligible(plan) : CloudChatTtsGate.IsEligible(plan)))
            return;

        var speak = plan.Actions.OfType<SpeakAction>().FirstOrDefault();
        if (speak is null || string.IsNullOrWhiteSpace(speak.Text))
            return;

        var request = new TtsRequest
        {
            Text = speak.Text,
            Voice = speak.Voice,
            EligibleForCloudChat = true
        };
        var strategy = selector.Select(request);
        TtsResult result;
        try
        {
            result = await strategy.SynthesizeAsync(request, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Clone synth failed. Griffin stays.");
            return;
        }

        if (result.Delivery != TtsDelivery.AudioBytes || result.Audio.Length == 0)
            return;

        var id = $"cloud-chat-{Guid.NewGuid():N}";
        var extension = result.ContentType?.Contains("wav", StringComparison.OrdinalIgnoreCase) == true
            ? ".wav"
            : ".mp3";
        clipCache.Put(id, result.ContentType ?? "audio/mpeg", result.Audio);

        if (!string.IsNullOrWhiteSpace(options.CaptureDirectory))
        {
            Directory.CreateDirectory(options.CaptureDirectory);
            var capturePath = Path.Combine(options.CaptureDirectory, id + extension);
            await File.WriteAllBytesAsync(capturePath, result.Audio, cancellationToken);
        }

        var skill = plan.Actions.OfType<InvokeNativeSkillAction>().FirstOrDefault();
        if (kitchenPlayer is { IsEnabled: true })
        {
            MuteGriffinSpeech(plan);
            _ = PlayKitchenInBackground(
                kitchenPlayer,
                result.Audio,
                result.ContentType ?? "audio/wav",
                cancellationToken);
            return;
        }

        if (!options.UseExperimentalAudioPlayback)
            return;

        if (skill?.Payload is null)
            return;

        var baseUrl = string.IsNullOrWhiteSpace(options.PublicAudioBaseUrl)
            ? "https://api.jibo.com/openjibo/tts"
            : options.PublicAudioBaseUrl.TrimEnd('/');
        skill.Payload["esml"] = EsmlAudioSpeechBuilder.ForRemoteAudio($"{baseUrl}/{id}{extension}");
    }

    private static void MuteGriffinSpeech(ResponsePlan plan)
    {
        var skill = plan.Actions.OfType<InvokeNativeSkillAction>().FirstOrDefault();
        if (skill is null)
        {
            skill = new InvokeNativeSkillAction
            {
                Sequence = 2,
                SkillName = "chitchat-skill",
                Payload = new Dictionary<string, object?>()
            };
            plan.Actions.Add(skill);
        }

        skill.Payload["kitchen_mute_speech"] = true;
        skill.Payload.TryGetValue("esml", out var esmlValue);
        skill.Payload["esml"] = EsmlAudioSpeechBuilder.MuteSpoken(esmlValue as string);
    }

    internal static ResponsePlan CreateHeardYouPlan()
    {
        return new ResponsePlan
        {
            IntentName = "heyJibo",
            Actions =
            {
                new SpeakAction
                {
                    Sequence = 0,
                    Text = HeardYouText,
                    Voice = "griffin"
                },
                new InvokeNativeSkillAction
                {
                    Sequence = 1,
                    SkillName = "chitchat-skill",
                    Payload = new Dictionary<string, object?>()
                }
            }
        };
    }

    internal const string HeardYouText = "I heard you.";

    private static async Task PlayKitchenInBackground(
        IRobotKitchenAudioPlayer kitchenPlayer,
        byte[] audio,
        string contentType,
        CancellationToken cancellationToken)
    {
        try
        {
            await kitchenPlayer.PlayAsync(audio, contentType, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
