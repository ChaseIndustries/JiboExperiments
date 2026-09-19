using Jibo.Cloud.Application.Services;
using Jibo.Runtime.Abstractions;

namespace Jibo.Cloud.Tests.Application;

public sealed class DefaultTtsStrategySelectorTests
{
    [Fact]
    public void Select_PrefersElevenLabsWhenItCanHandle()
    {
        var elevenLabs = new StubTtsStrategy("elevenlabs", canHandle: true);
        var griffin = new StubTtsStrategy("on-robot-griffin", canHandle: true);
        var selector = new DefaultTtsStrategySelector([elevenLabs, griffin]);

        var selected = selector.Select(new TtsRequest { Text = "Hello", EligibleForCloudChat = true });

        Assert.Same(elevenLabs, selected);
    }

    [Fact]
    public void Select_PrefersLocalCloneWhenItCanHandle()
    {
        var localClone = new StubTtsStrategy("local-clone", canHandle: true);
        var griffin = new StubTtsStrategy("on-robot-griffin", canHandle: true);
        var selector = new DefaultTtsStrategySelector([localClone, griffin]);

        var selected = selector.Select(new TtsRequest { Text = "Hello", EligibleForCloudChat = true });

        Assert.Same(localClone, selected);
    }

    [Fact]
    public void Select_FallsBackToGriffin()
    {
        var elevenLabs = new StubTtsStrategy("elevenlabs", canHandle: false);
        var griffin = new StubTtsStrategy("on-robot-griffin", canHandle: true);
        var selector = new DefaultTtsStrategySelector([elevenLabs, griffin]);

        var selected = selector.Select(new TtsRequest { Text = "Okay.", EligibleForCloudChat = false });

        Assert.Same(griffin, selected);
    }

    private sealed class StubTtsStrategy(string name, bool canHandle) : ITtsStrategy
    {
        public string Name => name;

        public bool CanHandle(TtsRequest request) => canHandle;

        public Task<TtsResult> SynthesizeAsync(TtsRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TtsResult { Provider = name });
        }
    }
}
