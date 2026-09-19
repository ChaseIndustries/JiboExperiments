namespace Jibo.Cloud.Application.Services;

public sealed class TtsOptions
{
    public bool EnableElevenLabs { get; set; }
    public string? ElevenLabsApiKey { get; set; }
    public string? ElevenLabsVoiceId { get; set; }
    public string ElevenLabsModelId { get; set; } = "eleven_multilingual_v2";
    public bool EnableLocalClone { get; set; }
    public string LocalCloneUrl { get; set; } = "http://127.0.0.1:8091";
    public bool EnableKitchenPlay { get; set; }
    public string? RobotIp { get; set; }
    public string RobotSshUser { get; set; } = "root";
    public string PlaybackMode { get; set; } = "griffin";
    public string? CaptureDirectory { get; set; }
    public string? PublicAudioBaseUrl { get; set; }

    public bool UseExperimentalAudioPlayback =>
        string.Equals(PlaybackMode, "experimental-audio", StringComparison.OrdinalIgnoreCase);

    public bool ShouldCapture =>
        UseExperimentalAudioPlayback ||
        EnableKitchenPlay ||
        string.Equals(PlaybackMode, "capture", StringComparison.OrdinalIgnoreCase);
}
