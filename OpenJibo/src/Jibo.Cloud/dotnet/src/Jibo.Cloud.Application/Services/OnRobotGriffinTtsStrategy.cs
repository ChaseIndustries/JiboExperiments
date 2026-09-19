using Jibo.Runtime.Abstractions;

namespace Jibo.Cloud.Application.Services;

public sealed class OnRobotGriffinTtsStrategy : ITtsStrategy
{
    public const string StrategyName = "on-robot-griffin";

    public string Name => StrategyName;

    public bool CanHandle(TtsRequest request) => true;

    public Task<TtsResult> SynthesizeAsync(TtsRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new TtsResult
        {
            Provider = StrategyName,
            Delivery = TtsDelivery.OnRobotEsml
        });
    }
}
