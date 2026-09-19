using Jibo.Cloud.Infrastructure.Audio;
using Jibo.Runtime.Abstractions;

namespace Jibo.Cloud.Tests.Infrastructure;

public sealed class GriffinRobotTtsClientTests
{
    [Fact]
    public async Task SpeakAsync_SavesAudioWhenRobotStreamsBytes()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("http://192.168.1.40:8089/tts_speak", request.RequestUri!.ToString());
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent("RIFF....WAVE"u8.ToArray())
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav") }
                }
            };
        });

        var client = new GriffinRobotTtsClient(new HttpClient(handler));
        var result = await client.SpeakAsync("192.168.1.40", "Hello, I am Jibo.");

        Assert.Equal("griffin", result.Provider);
        Assert.Equal(TtsDelivery.AudioBytes, result.Delivery);
        Assert.Equal("audio/wav", result.ContentType);
        Assert.NotEmpty(result.Audio);
    }

    [Fact]
    public async Task SpeakAsync_MarksOnRobotWhenResponseIsNotAudio()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("""{"status":"ok"}""", System.Text.Encoding.UTF8, "application/json")
        });

        var client = new GriffinRobotTtsClient(new HttpClient(handler));
        var result = await client.SpeakAsync("192.168.1.40", "Hello, I am Jibo.");

        Assert.Equal("griffin", result.Provider);
        Assert.Equal(TtsDelivery.OnRobotEsml, result.Delivery);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
        }
    }
}
