using Jibo.Cloud.Application.Services;
using Jibo.Cloud.Infrastructure.Audio;
using Jibo.Runtime.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jibo.Cloud.Tests.Infrastructure;

public sealed class ElevenLabsTtsStrategyTests
{
    [Fact]
    public void CanHandle_ReturnsFalse_WhenElevenLabsIsDisabled()
    {
        var strategy = CreateStrategy(enabled: false);

        Assert.False(strategy.CanHandle(new TtsRequest
        {
            Text = "Hello, I am Jibo.",
            EligibleForCloudChat = true
        }));
    }

    [Fact]
    public void CanHandle_ReturnsFalse_WhenTurnIsNotCloudChat()
    {
        var strategy = CreateStrategy(enabled: true);

        Assert.False(strategy.CanHandle(new TtsRequest
        {
            Text = "Okay.",
            EligibleForCloudChat = false
        }));
    }

    [Fact]
    public void CanHandle_ReturnsTrue_WhenConfiguredForCloudChat()
    {
        var strategy = CreateStrategy(enabled: true);

        Assert.True(strategy.CanHandle(new TtsRequest
        {
            Text = "Hello, I am Jibo.",
            EligibleForCloudChat = true
        }));
    }

    [Fact]
    public async Task SynthesizeAsync_PostsTextToConfiguredVoice()
    {
        string? capturedBody = null;
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            captured = request;
            capturedBody = request.Content is null
                ? null
                : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3, 4])
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg") }
                }
            };
        });

        var strategy = CreateStrategy(enabled: true, handler);
        var result = await strategy.SynthesizeAsync(new TtsRequest
        {
            Text = "Hello, I am Jibo.",
            EligibleForCloudChat = true
        });

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal("https://api.elevenlabs.io/v1/text-to-speech/voice-jibo",
            captured.RequestUri!.ToString());
        Assert.Equal("secret-key", captured.Headers.GetValues("xi-api-key").Single());
        Assert.Equal("elevenlabs", result.Provider);
        Assert.Equal("audio/mpeg", result.ContentType);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, result.Audio);
        Assert.Equal(TtsDelivery.AudioBytes, result.Delivery);
        Assert.Contains("Hello, I am Jibo.", capturedBody, StringComparison.Ordinal);
        Assert.Contains("eleven_multilingual_v2", capturedBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SynthesizeAsync_Throws_WhenElevenLabsReturnsError()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("bad key")
        });
        var strategy = CreateStrategy(enabled: true, handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => strategy.SynthesizeAsync(new TtsRequest
        {
            Text = "Hello, I am Jibo.",
            EligibleForCloudChat = true
        }));

        Assert.Contains("ElevenLabs", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ElevenLabsTtsStrategy CreateStrategy(bool enabled, HttpMessageHandler? handler = null)
    {
        return new ElevenLabsTtsStrategy(
            new TtsOptions
            {
                EnableElevenLabs = enabled,
                ElevenLabsApiKey = "secret-key",
                ElevenLabsVoiceId = "voice-jibo",
                ElevenLabsModelId = "eleven_multilingual_v2"
            },
            new HttpClient(handler ?? new StubHttpMessageHandler()),
            NullLogger<ElevenLabsTtsStrategy>.Instance);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage>? responder = null)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(responder?.Invoke(request)
                                   ?? new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
