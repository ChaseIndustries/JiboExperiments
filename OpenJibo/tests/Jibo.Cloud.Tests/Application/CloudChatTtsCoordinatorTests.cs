using Jibo.Cloud.Application.Services;
using Jibo.Runtime.Abstractions;

namespace Jibo.Cloud.Tests.Application;

public sealed class CloudChatTtsCoordinatorTests
{
    [Fact]
    public async Task ApplyAsync_LeavesGriffinSpeechWhenPlaybackIsGriffin()
    {
        var plan = CloudChatPlan("Hello there.");
        var coordinator = CreateCoordinator(playbackMode: "griffin", strategy: new RecordingTtsStrategy());

        await coordinator.ApplyAsync(plan);

        Assert.Null(ReadEsml(plan));
        Assert.Equal("Hello there.", plan.Actions.OfType<SpeakAction>().Single().Text);
    }

    [Fact]
    public async Task ApplyAsync_WritesCaptureWithoutChangingEsml()
    {
        var captureDirectory = Path.Combine(Path.GetTempPath(), $"openjibo-tts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(captureDirectory);
        try
        {
            var plan = CloudChatPlan("Hello there.");
            var coordinator = CreateCoordinator(
                playbackMode: "capture",
                strategy: new RecordingTtsStrategy([9, 8, 7]),
                captureDirectory: captureDirectory);

            await coordinator.ApplyAsync(plan);

            Assert.Null(ReadEsml(plan));
            var files = Directory.GetFiles(captureDirectory, "cloud-chat-*.mp3");
            Assert.Single(files);
            Assert.Equal(new byte[] { 9, 8, 7 }, await File.ReadAllBytesAsync(files[0]));
        }
        finally
        {
            Directory.Delete(captureDirectory, true);
        }
    }

    [Fact]
    public async Task ApplyAsync_ReplacesCloudChatEsmlWithRemoteAudio()
    {
        var plan = CloudChatPlan("Hello there.");
        var cache = new InMemoryTtsClipCache();
        var coordinator = CreateCoordinator(
            playbackMode: "experimental-audio",
            strategy: new RecordingTtsStrategy([1, 2, 3]),
            cache: cache,
            publicAudioBaseUrl: "https://api.jibo.com/openjibo/tts");

        await coordinator.ApplyAsync(plan);

        var esml = ReadEsml(plan);
        Assert.NotNull(esml);
        Assert.StartsWith("<speak><audio src='https://api.jibo.com/openjibo/tts/cloud-chat-", esml, StringComparison.Ordinal);
        Assert.EndsWith(".mp3' /></speak>", esml);
        Assert.True(cache.TryGet(ExtractId(esml!, ".mp3"), out var clip));
        Assert.Equal(new byte[] { 1, 2, 3 }, clip.Audio);
    }

    [Fact]
    public async Task ApplyAsync_SkipsNativeSkills()
    {
        var plan = new ResponsePlan
        {
            Actions =
            {
                new SpeakAction { Text = "Okay." },
                new InvokeNativeSkillAction
                {
                    SkillName = "@be/clock",
                    Payload = new Dictionary<string, object?>()
                }
            }
        };
        var strategy = new RecordingTtsStrategy();
        var coordinator = CreateCoordinator("capture", strategy);

        await coordinator.ApplyAsync(plan);

        Assert.Equal(0, strategy.Calls);
    }

    [Fact]
    public async Task ApplyAsync_ReplacesCloudChatEsmlWithLocalCloneWav()
    {
        var plan = CloudChatPlan("Hello there.");
        var cache = new InMemoryTtsClipCache();
        var coordinator = CreateCoordinator(
            playbackMode: "experimental-audio",
            strategy: new RecordingTtsStrategy([9, 8, 7], name: "local-clone", contentType: "audio/wav"),
            cache: cache,
            publicAudioBaseUrl: "https://api.jibo.com/openjibo/tts");

        await coordinator.ApplyAsync(plan);

        var esml = ReadEsml(plan);
        Assert.NotNull(esml);
        Assert.StartsWith("<speak><audio src='https://api.jibo.com/openjibo/tts/cloud-chat-", esml, StringComparison.Ordinal);
        Assert.EndsWith(".wav' /></speak>", esml);
        Assert.True(cache.TryGet(ExtractId(esml!, ".wav"), out var clip));
        Assert.Equal(new byte[] { 9, 8, 7 }, clip.Audio);
    }

    [Fact]
    public async Task ApplyAsync_PlaysKitchenAudioAndMutesGriffin()
    {
        var plan = CloudChatPlan("Hello there.");
        var kitchen = new RecordingKitchenPlayer();
        var coordinator = CreateCoordinator(
            playbackMode: "griffin",
            strategy: new RecordingTtsStrategy([4, 5, 6], name: "local-clone", contentType: "audio/wav"),
            enableKitchenPlay: true,
            kitchenPlayer: kitchen);

        await coordinator.ApplyAsync(plan);

        Assert.Equal(1, kitchen.Calls);
        Assert.Equal(new byte[] { 4, 5, 6 }, kitchen.LastAudio);
        Assert.Equal("audio/wav", kitchen.LastContentType);
        Assert.Equal("<speak><break time='1ms'/></speak>", ReadEsml(plan));
        Assert.Equal("Hello there.", plan.Actions.OfType<SpeakAction>().Single().Text);
    }

    [Fact]
    public async Task ApplyAsync_KitchenPlayWinsOverRemoteAudioSrc()
    {
        var plan = CloudChatPlan("Hello there.");
        var kitchen = new RecordingKitchenPlayer();
        var coordinator = CreateCoordinator(
            playbackMode: "experimental-audio",
            strategy: new RecordingTtsStrategy([1, 2, 3], contentType: "audio/wav"),
            publicAudioBaseUrl: "https://api.jibo.com/openjibo/tts",
            enableKitchenPlay: true,
            kitchenPlayer: kitchen);

        await coordinator.ApplyAsync(plan);

        Assert.Equal(1, kitchen.Calls);
        Assert.Equal("<speak><break time='1ms'/></speak>", ReadEsml(plan));
    }

    [Fact]
    public async Task ApplyAsync_MutesGriffinWhenPlanHasNoSkillAction()
    {
        var plan = new ResponsePlan
        {
            IntentName = "how_are_you",
            Actions = { new SpeakAction { Text = "I am doing well.", Voice = "griffin" } }
        };
        var kitchen = new RecordingKitchenPlayer();
        var coordinator = CreateCoordinator(
            playbackMode: "griffin",
            strategy: new RecordingTtsStrategy([7, 7, 7], contentType: "audio/wav"),
            enableKitchenPlay: true,
            kitchenPlayer: kitchen);

        await coordinator.ApplyAsync(plan);

        Assert.Equal(1, kitchen.Calls);
        var skill = plan.Actions.OfType<InvokeNativeSkillAction>().Single();
        Assert.Equal("chitchat-skill", skill.SkillName);
        Assert.Equal("<speak><break time='1ms'/></speak>", ReadEsml(plan));
    }

    [Fact]
    public async Task ApplyAsync_KitchenPlayMutesWeatherSpeechAndKeepsAnim()
    {
        var plan = new ResponsePlan
        {
            IntentName = "weather",
            Actions =
            {
                new SpeakAction { Text = "It is rainy in Boston.", Voice = "griffin" },
                new InvokeNativeSkillAction
                {
                    SkillName = "chitchat-skill",
                    Payload = new Dictionary<string, object?>
                    {
                        ["cloudSkill"] = "weather",
                        ["esml"] =
                            "<speak><anim cat='weather' meta='rain' nonBlocking='true' /><break size='0.35'/><es cat='neutral' filter='!ssa-only, !sfx-only' endNeutral='true'>It is rainy in Boston.</es></speak>"
                    }
                }
            }
        };
        var kitchen = new RecordingKitchenPlayer();
        var coordinator = CreateCoordinator(
            playbackMode: "griffin",
            strategy: new RecordingTtsStrategy([8, 8, 8], contentType: "audio/wav"),
            enableKitchenPlay: true,
            kitchenPlayer: kitchen);

        await coordinator.ApplyAsync(plan);

        Assert.Equal(1, kitchen.Calls);
        Assert.Equal("It is rainy in Boston.", plan.Actions.OfType<SpeakAction>().Single().Text);
        var esml = ReadEsml(plan);
        Assert.Contains("cat='weather'", esml, StringComparison.Ordinal);
        Assert.Contains("<break time='1ms'/>", esml, StringComparison.Ordinal);
        Assert.DoesNotContain("It is rainy in Boston.", esml, StringComparison.Ordinal);
        Assert.Equal(true, plan.Actions.OfType<InvokeNativeSkillAction>().Single().Payload["kitchen_mute_speech"]);
    }

    [Fact]
    public async Task ApplyAsync_KitchenPlayMutesNewsSpeechAndKeepsAnim()
    {
        var plan = new ResponsePlan
        {
            IntentName = "news",
            Actions =
            {
                new SpeakAction { Text = "Here is today's news.", Voice = "griffin" },
                new InvokeNativeSkillAction
                {
                    SkillName = "chitchat-skill",
                    Payload = new Dictionary<string, object?>
                    {
                        ["cloudSkill"] = "news",
                        ["esml"] =
                            "<speak><anim cat='news' meta='news-stinger' nonBlocking='true' /><break size='0.75'/><es cat='neutral' filter='!ssa-only, !sfx-only' endNeutral='true'>Here is today's news.</es></speak>"
                    }
                }
            }
        };
        var kitchen = new RecordingKitchenPlayer();
        var coordinator = CreateCoordinator(
            playbackMode: "griffin",
            strategy: new RecordingTtsStrategy([8, 8, 8], contentType: "audio/wav"),
            enableKitchenPlay: true,
            kitchenPlayer: kitchen);

        await coordinator.ApplyAsync(plan);

        Assert.Equal(1, kitchen.Calls);
        var esml = ReadEsml(plan);
        Assert.Contains("cat='news'", esml, StringComparison.Ordinal);
        Assert.Contains("<break time='1ms'/>", esml, StringComparison.Ordinal);
        Assert.DoesNotContain("Here is today's news.", esml, StringComparison.Ordinal);
        Assert.Equal(true, plan.Actions.OfType<InvokeNativeSkillAction>().Single().Payload["kitchen_mute_speech"]);
    }

    [Fact]
    public async Task ApplyAsync_KitchenPlayMutesCommuteSpeechAndKeepsAnim()
    {
        var plan = CloneOwnedPlan(
            "commute",
            "report-skill",
            "Traffic is light.",
            "<speak><anim cat='commute' meta='car' nonBlocking='true' /><break size='0.75'/><es cat='neutral' filter='!ssa-only, !sfx-only' endNeutral='true'>Traffic is light.</es></speak>");
        var kitchen = new RecordingKitchenPlayer();
        var coordinator = CreateCoordinator(
            playbackMode: "griffin",
            strategy: new RecordingTtsStrategy([8], contentType: "audio/wav"),
            enableKitchenPlay: true,
            kitchenPlayer: kitchen);

        await coordinator.ApplyAsync(plan);

        Assert.Equal(1, kitchen.Calls);
        var esml = ReadEsml(plan);
        Assert.Contains("cat='commute'", esml, StringComparison.Ordinal);
        Assert.DoesNotContain("Traffic is light.", esml, StringComparison.Ordinal);
        Assert.Equal(true, plan.Actions.OfType<InvokeNativeSkillAction>().Single().Payload["kitchen_mute_speech"]);
    }

    [Fact]
    public async Task ApplyAsync_KitchenPlaySilencesCalendarWithoutEsml()
    {
        var plan = CloneOwnedPlan("calendar", "report-skill", "Nothing on the calendar today.", esml: null);
        var kitchen = new RecordingKitchenPlayer();
        var coordinator = CreateCoordinator(
            playbackMode: "griffin",
            strategy: new RecordingTtsStrategy([3], contentType: "audio/wav"),
            enableKitchenPlay: true,
            kitchenPlayer: kitchen);

        await coordinator.ApplyAsync(plan);

        Assert.Equal(1, kitchen.Calls);
        Assert.Equal("<speak><break time='1ms'/></speak>", ReadEsml(plan));
        Assert.Equal("Nothing on the calendar today.", plan.Actions.OfType<SpeakAction>().Single().Text);
    }

    [Fact]
    public async Task ApplyAsync_KitchenPlaySilencesAnswerWithoutEsml()
    {
        var plan = CloneOwnedPlan("answer", "chitchat-skill", "According to wikipedia.", esml: null);
        var kitchen = new RecordingKitchenPlayer();
        var coordinator = CreateCoordinator(
            playbackMode: "griffin",
            strategy: new RecordingTtsStrategy([2], contentType: "audio/wav"),
            enableKitchenPlay: true,
            kitchenPlayer: kitchen);

        await coordinator.ApplyAsync(plan);

        Assert.Equal(1, kitchen.Calls);
        Assert.Equal("<speak><break time='1ms'/></speak>", ReadEsml(plan));
    }

    [Fact]
    public async Task ApplyAsync_LeavesGriffinWhenCloneSynthThrows()
    {
        var plan = CloudChatPlan("Hello there.");
        var kitchen = new RecordingKitchenPlayer();
        var coordinator = CreateCoordinator(
            playbackMode: "griffin",
            strategy: new ThrowingTtsStrategy(),
            enableKitchenPlay: true,
            kitchenPlayer: kitchen);

        await coordinator.ApplyAsync(plan);

        Assert.Equal(0, kitchen.Calls);
        Assert.Null(ReadEsml(plan));
        Assert.False(plan.Actions.OfType<InvokeNativeSkillAction>().Single().Payload.ContainsKey("kitchen_mute_speech"));
    }

    [Fact]
    public async Task ApplyAsync_SkipsKitchenPlayForDanceMusicAnim()
    {
        const string danceEsml =
            "<speak>Okay.<break size='0.2'/> Watch this.<anim cat='dance' filter='music, rom-upbeat' /></speak>";
        var plan = new ResponsePlan
        {
            IntentName = "dance",
            Actions =
            {
                new SpeakAction { Text = "You got it.", Voice = "griffin" },
                new InvokeNativeSkillAction
                {
                    SkillName = "chitchat-skill",
                    Payload = new Dictionary<string, object?>
                    {
                        ["esml"] = danceEsml
                    }
                }
            }
        };
        var kitchen = new RecordingKitchenPlayer();
        var coordinator = CreateCoordinator(
            playbackMode: "griffin",
            strategy: new RecordingTtsStrategy([9], contentType: "audio/wav"),
            enableKitchenPlay: true,
            kitchenPlayer: kitchen);

        await coordinator.ApplyAsync(plan);

        Assert.Equal(0, kitchen.Calls);
        Assert.Equal(danceEsml, ReadEsml(plan));
        Assert.False(plan.Actions.OfType<InvokeNativeSkillAction>().Single().Payload.ContainsKey("kitchen_mute_speech"));
    }

    [Fact]
    public async Task ApplyAsync_KitchenPlayMutesHeardYouFallback()
    {
        var plan = CloudChatTtsCoordinator.CreateHeardYouPlan();
        var kitchen = new RecordingKitchenPlayer();
        var coordinator = CreateCoordinator(
            playbackMode: "griffin",
            strategy: new RecordingTtsStrategy([5], contentType: "audio/wav"),
            enableKitchenPlay: true,
            kitchenPlayer: kitchen);

        await coordinator.ApplyAsync(plan);

        Assert.Equal(1, kitchen.Calls);
        Assert.Equal(CloudChatTtsCoordinator.HeardYouText, plan.Actions.OfType<SpeakAction>().Single().Text);
        Assert.Equal("<speak><break time='1ms'/></speak>", ReadEsml(plan));
    }

    [Fact]
    public async Task ApplyAsync_ReturnsBeforeKitchenPlayFinishes()
    {
        var plan = CloudChatPlan("Hello there.");
        var kitchen = new DelayedKitchenPlayer();
        var coordinator = CreateCoordinator(
            playbackMode: "griffin",
            strategy: new RecordingTtsStrategy([1], contentType: "audio/wav"),
            enableKitchenPlay: true,
            kitchenPlayer: kitchen);

        await coordinator.ApplyAsync(plan).WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(1, kitchen.Calls);
        Assert.False(kitchen.Finished);
        Assert.Equal("<speak><break time='1ms'/></speak>", ReadEsml(plan));
        kitchen.Release.SetResult();
        await kitchen.Completed.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(kitchen.Finished);
    }

    private static CloudChatTtsCoordinator CreateCoordinator(
        string playbackMode,
        ITtsStrategy strategy,
        InMemoryTtsClipCache? cache = null,
        string? captureDirectory = null,
        string? publicAudioBaseUrl = null,
        bool enableKitchenPlay = false,
        IRobotKitchenAudioPlayer? kitchenPlayer = null)
    {
        var options = new TtsOptions
        {
            EnableElevenLabs = true,
            PlaybackMode = playbackMode,
            CaptureDirectory = captureDirectory,
            PublicAudioBaseUrl = publicAudioBaseUrl,
            EnableKitchenPlay = enableKitchenPlay,
            RobotIp = enableKitchenPlay ? "192.168.4.24" : null
        };
        var selector = new DefaultTtsStrategySelector([strategy, new OnRobotGriffinTtsStrategy()]);
        return new CloudChatTtsCoordinator(
            selector,
            options,
            cache ?? new InMemoryTtsClipCache(),
            kitchenPlayer);
    }

    private static ResponsePlan CloudChatPlan(string text)
    {
        return new ResponsePlan
        {
            IntentName = "chat",
            Actions =
            {
                new SpeakAction { Text = text, Voice = "griffin" },
                new InvokeNativeSkillAction
                {
                    SkillName = "chitchat-skill",
                    Payload = new Dictionary<string, object?>()
                }
            }
        };
    }

    private static ResponsePlan CloneOwnedPlan(string cloudSkill, string skillName, string text, string? esml)
    {
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["cloudSkill"] = cloudSkill
        };
        if (esml is not null)
            payload["esml"] = esml;

        return new ResponsePlan
        {
            IntentName = cloudSkill,
            Actions =
            {
                new SpeakAction { Text = text, Voice = "griffin" },
                new InvokeNativeSkillAction
                {
                    SkillName = skillName,
                    Payload = payload
                }
            }
        };
    }

    private static string? ReadEsml(ResponsePlan plan)
    {
        var skill = plan.Actions.OfType<InvokeNativeSkillAction>().First();
        return skill.Payload.TryGetValue("esml", out var value) ? value?.ToString() : null;
    }

    private static string ExtractId(string esml, string extension = ".mp3")
    {
        const string prefix = "https://api.jibo.com/openjibo/tts/";
        var start = esml.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
        var end = esml.IndexOf(extension, start, StringComparison.Ordinal);
        return esml[start..end];
    }

    private sealed class ThrowingTtsStrategy : ITtsStrategy
    {
        public string Name => "local-clone";

        public bool CanHandle(TtsRequest request) => request.EligibleForCloudChat;

        public Task<TtsResult> SynthesizeAsync(TtsRequest request, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("sidecar down");
        }
    }

    private sealed class RecordingTtsStrategy(
        byte[]? audio = null,
        string name = "elevenlabs",
        string contentType = "audio/mpeg") : ITtsStrategy
    {
        public string Name => name;
        public int Calls { get; private set; }

        public bool CanHandle(TtsRequest request) => request.EligibleForCloudChat;

        public Task<TtsResult> SynthesizeAsync(TtsRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new TtsResult
            {
                Provider = Name,
                Delivery = TtsDelivery.AudioBytes,
                Audio = audio ?? [1],
                ContentType = contentType
            });
        }
    }

    private sealed class RecordingKitchenPlayer : IRobotKitchenAudioPlayer
    {
        public bool IsEnabled => true;
        public int Calls { get; private set; }
        public byte[]? LastAudio { get; private set; }
        public string? LastContentType { get; private set; }

        public Task PlayAsync(byte[] audio, string contentType, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastAudio = audio;
            LastContentType = contentType;
            return Task.CompletedTask;
        }
    }

    private sealed class DelayedKitchenPlayer : IRobotKitchenAudioPlayer
    {
        public bool IsEnabled => true;
        public int Calls { get; private set; }
        public bool Finished { get; private set; }
        public TaskCompletionSource Release { get; } = new();
        public TaskCompletionSource Completed { get; } = new();

        public async Task PlayAsync(byte[] audio, string contentType, CancellationToken cancellationToken = default)
        {
            Calls++;
            try
            {
                await Release.Task.WaitAsync(cancellationToken);
            }
            finally
            {
                Finished = true;
                Completed.TrySetResult();
            }
        }
    }
}
