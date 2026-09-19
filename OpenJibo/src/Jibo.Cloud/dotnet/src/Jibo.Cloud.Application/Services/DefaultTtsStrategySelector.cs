using Jibo.Runtime.Abstractions;

namespace Jibo.Cloud.Application.Services;

public sealed class DefaultTtsStrategySelector(IEnumerable<ITtsStrategy> strategies) : ITtsStrategySelector
{
    private readonly IReadOnlyList<ITtsStrategy> _strategies = strategies.ToArray();

    public ITtsStrategy Select(TtsRequest request)
    {
        var strategy = _strategies.FirstOrDefault(candidate => candidate.CanHandle(request));
        return strategy ?? throw new InvalidOperationException("No TTS strategy can handle the current request.");
    }
}
