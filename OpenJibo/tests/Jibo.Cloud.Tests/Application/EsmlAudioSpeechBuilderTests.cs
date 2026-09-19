using Jibo.Cloud.Application.Services;

namespace Jibo.Cloud.Tests.Application;

public sealed class EsmlAudioSpeechBuilderTests
{
    [Fact]
    public void ForRemoteAudio_WrapsUrlInAudioTag()
    {
        var esml = EsmlAudioSpeechBuilder.ForRemoteAudio("https://api.jibo.com/openjibo/tts/cloud-chat-1.mp3");

        Assert.Equal(
            "<speak><audio src='https://api.jibo.com/openjibo/tts/cloud-chat-1.mp3' /></speak>",
            esml);
    }

    [Fact]
    public void ForRemoteAudio_EscapesXml()
    {
        var esml = EsmlAudioSpeechBuilder.ForRemoteAudio("https://example.test/a&b.mp3");

        Assert.Contains("a&amp;b.mp3", esml, StringComparison.Ordinal);
    }

    [Fact]
    public void ForSilence_UsesTinyBreak()
    {
        Assert.Equal("<speak><break time='1ms'/></speak>", EsmlAudioSpeechBuilder.ForSilence());
    }

    [Fact]
    public void SilenceSpokenElements_KeepsWeatherAnim()
    {
        var esml =
            "<speak><anim cat='weather' meta='rain' nonBlocking='true' /><break size='0.35'/><es cat='neutral' filter='!ssa-only, !sfx-only' endNeutral='true'>It is rainy in Boston.</es></speak>";

        var muted = EsmlAudioSpeechBuilder.SilenceSpokenElements(esml);

        Assert.Contains("cat='weather'", muted, StringComparison.Ordinal);
        Assert.Contains("meta='rain'", muted, StringComparison.Ordinal);
        Assert.Contains("<break time='1ms'/>", muted, StringComparison.Ordinal);
        Assert.DoesNotContain("It is rainy in Boston.", muted, StringComparison.Ordinal);
    }

    [Fact]
    public void MuteSpoken_BlankBecomesSilence()
    {
        Assert.Equal("<speak><break time='1ms'/></speak>", EsmlAudioSpeechBuilder.MuteSpoken(null));
        Assert.Equal("<speak><break time='1ms'/></speak>", EsmlAudioSpeechBuilder.MuteSpoken("  "));
    }

    [Fact]
    public void MuteSpoken_KeepsNewsAndCommuteAnim()
    {
        var news =
            "<speak><anim cat='news' meta='news-stinger' nonBlocking='true' /><break size='0.75'/><es cat='neutral' filter='!ssa-only, !sfx-only' endNeutral='true'>Here is today's news.</es></speak>";
        var commute =
            "<speak><anim cat='commute' meta='car' nonBlocking='true' /><break size='0.75'/><es cat='neutral' filter='!ssa-only, !sfx-only' endNeutral='true'>Traffic is light.</es></speak>";

        var mutedNews = EsmlAudioSpeechBuilder.MuteSpoken(news);
        var mutedCommute = EsmlAudioSpeechBuilder.MuteSpoken(commute);

        Assert.Contains("cat='news'", mutedNews, StringComparison.Ordinal);
        Assert.DoesNotContain("Here is today's news.", mutedNews, StringComparison.Ordinal);
        Assert.Contains("cat='commute'", mutedCommute, StringComparison.Ordinal);
        Assert.DoesNotContain("Traffic is light.", mutedCommute, StringComparison.Ordinal);
    }

    [Fact]
    public void MuteSpoken_KeepsDanceAnimAndDropsRawSpeech()
    {
        var esml = "<speak>Okay.<break size='0.2'/> Watch this.<anim cat='dance' filter='music, rom-upbeat' /></speak>";

        var muted = EsmlAudioSpeechBuilder.MuteSpoken(esml);

        Assert.Contains("cat='dance'", muted, StringComparison.Ordinal);
        Assert.Contains("rom-upbeat", muted, StringComparison.Ordinal);
        Assert.Contains("<break time='1ms'/>", muted, StringComparison.Ordinal);
        Assert.DoesNotContain("Okay.", muted, StringComparison.Ordinal);
        Assert.DoesNotContain("Watch this.", muted, StringComparison.Ordinal);
    }

    [Fact]
    public void MuteSpoken_KeepsDiceAnimAndDropsSpokenLine()
    {
        var esml = "<speak><anim cat='jiboji' filter='roll-die-4'/><break size='0.3'/> It landed on 4.</speak>";

        var muted = EsmlAudioSpeechBuilder.MuteSpoken(esml);

        Assert.Contains("roll-die-4", muted, StringComparison.Ordinal);
        Assert.DoesNotContain("It landed on 4.", muted, StringComparison.Ordinal);
    }

    [Fact]
    public void MuteSpoken_KeepsSantaAnimAndDropsSpokenLine()
    {
        var esml = "<speak>Let's see if I can spot him. <anim cat='jiboji' filter='santa-scanner' nonBlocking='true'/></speak>";

        var muted = EsmlAudioSpeechBuilder.MuteSpoken(esml);

        Assert.Contains("santa-scanner", muted, StringComparison.Ordinal);
        Assert.DoesNotContain("Let's see if I can spot him.", muted, StringComparison.Ordinal);
    }

    [Fact]
    public void MuteSpoken_SpeechOnlySpeakBecomesSilence()
    {
        var muted = EsmlAudioSpeechBuilder.MuteSpoken(
            "<speak>I can't do that yet, but I bet I'll be able to do that sometime in the near future.</speak>");

        Assert.Equal("<speak><break time='1ms'/></speak>", muted);
    }

    [Fact]
    public void MuteSpoken_KeepsCloudVersionBreaks()
    {
        var muted = EsmlAudioSpeechBuilder.MuteSpoken(OpenJiboCloudBuildInfo.EsmlVersion);

        Assert.Contains("<break time='10ms'/>", muted, StringComparison.Ordinal);
        Assert.DoesNotContain("Cloud version", muted, StringComparison.Ordinal);
        Assert.DoesNotContain("dot", muted, StringComparison.Ordinal);
    }

    [Fact]
    public void SpokenInner_UsesSilentBreakWhenMuted()
    {
        Assert.Equal("<break time='1ms'/>", EsmlAudioSpeechBuilder.SpokenInner(true, "It is rainy."));
        Assert.Equal("It is rainy.", EsmlAudioSpeechBuilder.SpokenInner(false, "It is rainy."));
    }
}
