namespace Jibo.Runtime.Abstractions;

public enum TtsDelivery
{
    OnRobotEsml = 0,
    AudioBytes = 1
}

public sealed class TtsRequest
{
    public string Text { get; init; } = string.Empty;
    public string? Voice { get; init; }
    public bool EligibleForCloudChat { get; init; }
}

public sealed class TtsResult
{
    public string Provider { get; init; } = string.Empty;
    public TtsDelivery Delivery { get; init; } = TtsDelivery.OnRobotEsml;
    public byte[] Audio { get; init; } = [];
    public string? ContentType { get; init; }
    public string? VoiceId { get; init; }
}

public interface ITtsStrategy
{
    string Name { get; }
    bool CanHandle(TtsRequest request);
    Task<TtsResult> SynthesizeAsync(TtsRequest request, CancellationToken cancellationToken = default);
}

public interface ITtsStrategySelector
{
    ITtsStrategy Select(TtsRequest request);
}
