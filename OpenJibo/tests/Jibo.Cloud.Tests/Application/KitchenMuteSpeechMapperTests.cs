using System.Text.Json;
using Jibo.Cloud.Application.Abstractions;
using Jibo.Cloud.Application.Services;
using Jibo.Cloud.Domain.Models;
using Jibo.Runtime.Abstractions;

namespace Jibo.Cloud.Tests.Application;

public sealed class KitchenMuteSpeechMapperTests
{
    [Fact]
    public void Map_NewsSequence_MutesSpokenChildrenWhenKitchenFlagSet()
    {
        var plan = new ResponsePlan
        {
            IntentName = "news",
            Actions =
            {
                new SpeakAction
                {
                    Sequence = 0,
                    Text = "Here's today's news. Robotics club opens a new community lab.",
                    Voice = "griffin"
                },
                new InvokeNativeSkillAction
                {
                    Sequence = 1,
                    SkillName = "chitchat-skill",
                    Payload = new Dictionary<string, object?>
                    {
                        ["cloudSkill"] = "news",
                        ["skillId"] = "news",
                        ["kitchen_mute_speech"] = true,
                        ["news_sections"] = new[]
                        {
                            Section("news_intro", "Here's today's news.", "news", "news-intro, no-eye-end"),
                            Section("news_headline", "Robotics club opens a new community lab.", "news", "news-stinger")
                        }
                    }
                }
            }
        };

        var replies = ResponsePlanToSocketMessagesMapper.Map(
            plan,
            Turn("tell me the news"),
            new CloudSession(),
            emitSkillActions: true);

        using var skillAction = replies
            .Select(reply => reply.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => JsonDocument.Parse(text!))
            .First(document => document.RootElement.GetProperty("type").GetString() == "SKILL_ACTION");
        var children = skillAction.RootElement
            .GetProperty("data")
            .GetProperty("action")
            .GetProperty("config")
            .GetProperty("jcp")
            .GetProperty("children");
        var joined = string.Join(
            ' ',
            children.EnumerateArray()
                .Select(child => child.GetProperty("config").GetProperty("play").GetProperty("esml").GetString()));

        Assert.Contains("news-stinger", joined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<break time='1ms'/>", joined, StringComparison.Ordinal);
        Assert.DoesNotContain("Here's today's news", joined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Robotics club opens a new community lab", joined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Map_NewsSequence_KeepsSpokenChildrenWhenKitchenFlagMissing()
    {
        var plan = NewsPlan(muteSpoken: false);
        var joined = JoinChildEsml(MapSkillAction(plan));

        Assert.Contains("Here's today's news", joined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Robotics club opens a new community lab", joined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Map_PersonalReportSequence_MutesSpokenChildrenWhenKitchenFlagSet()
    {
        var plan = new ResponsePlan
        {
            IntentName = "personal_report",
            Actions =
            {
                new SpeakAction
                {
                    Sequence = 0,
                    Text = "Good morning. It is rainy. Traffic is light.",
                    Voice = "griffin"
                },
                new InvokeNativeSkillAction
                {
                    Sequence = 1,
                    SkillName = "report-skill",
                    Payload = new Dictionary<string, object?>
                    {
                        ["cloudSkill"] = "personal_report",
                        ["skillId"] = "report-skill",
                        ["kitchen_mute_speech"] = true,
                        ["personal_report_sections"] = new[]
                        {
                            Section("weather", "It is rainy.", "weather", "rain"),
                            Section("commute", "Traffic is light.", "commute", "car")
                        }
                    }
                }
            }
        };

        var joined = JoinChildEsml(MapSkillAction(plan));
        Assert.Contains("cat='weather'", joined, StringComparison.Ordinal);
        Assert.Contains("cat='commute'", joined, StringComparison.Ordinal);
        Assert.Contains("<break time='1ms'/>", joined, StringComparison.Ordinal);
        Assert.DoesNotContain("It is rainy.", joined, StringComparison.Ordinal);
        Assert.DoesNotContain("Traffic is light.", joined, StringComparison.Ordinal);
    }

    [Fact]
    public void Map_Answer_UsesSilentPlayEsmlWhenKitchenFlagSet()
    {
        var plan = new ResponsePlan
        {
            IntentName = "knowledge_search",
            Actions =
            {
                new SpeakAction
                {
                    Sequence = 0,
                    Text = "According to wikipedia dot org. James Abram Garfield was the 20th president.",
                    Voice = "griffin"
                },
                new InvokeNativeSkillAction
                {
                    Sequence = 1,
                    SkillName = "chitchat-skill",
                    Payload = new Dictionary<string, object?>
                    {
                        ["cloudSkill"] = SearchThinkingPreludeFactory.AnswerSkillId,
                        ["kitchen_mute_speech"] = true,
                        ["esml"] = "<speak><break time='1ms'/></speak>"
                    }
                }
            }
        };

        using var skillAction = MapSkillAction(plan);
        var esml = skillAction.RootElement
            .GetProperty("data")
            .GetProperty("action")
            .GetProperty("config")
            .GetProperty("jcp")
            .GetProperty("config")
            .GetProperty("play")
            .GetProperty("esml")
            .GetString();

        Assert.Equal("<speak><break time='1ms'/></speak>", esml);
        Assert.DoesNotContain("Thinking_Eye", esml, StringComparison.Ordinal);
        Assert.DoesNotContain("Garfield", esml, StringComparison.Ordinal);
    }

    [Fact]
    public void MapFallback_UsesKitchenEsmlWhenProvided()
    {
        var replies = ResponsePlanToSocketMessagesMapper.MapFallback(
            "trans-heard-you",
            ["launch"],
            EsmlAudioSpeechBuilder.ForSilence());
        using var skillAction = replies
            .Select(reply => reply.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => JsonDocument.Parse(text!))
            .First(document => document.RootElement.GetProperty("type").GetString() == "SKILL_ACTION");
        var esml = skillAction.RootElement
            .GetProperty("data")
            .GetProperty("action")
            .GetProperty("config")
            .GetProperty("jcp")
            .GetProperty("config")
            .GetProperty("play")
            .GetProperty("esml")
            .GetString();

        Assert.Equal("<speak><break time='1ms'/></speak>", esml);
        Assert.DoesNotContain("I heard you.", esml, StringComparison.Ordinal);
    }

    [Fact]
    public void MapFallback_KeepsGriffinWhenKitchenEsmlMissing()
    {
        var replies = ResponsePlanToSocketMessagesMapper.MapFallback("trans-heard-you", ["launch"]);
        using var skillAction = replies
            .Select(reply => reply.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => JsonDocument.Parse(text!))
            .First(document => document.RootElement.GetProperty("type").GetString() == "SKILL_ACTION");
        var esml = skillAction.RootElement
            .GetProperty("data")
            .GetProperty("action")
            .GetProperty("config")
            .GetProperty("jcp")
            .GetProperty("config")
            .GetProperty("play")
            .GetProperty("esml")
            .GetString();

        Assert.Contains("I heard you.", esml, StringComparison.Ordinal);
    }

    private static ResponsePlan NewsPlan(bool muteSpoken)
    {
        return new ResponsePlan
        {
            IntentName = "news",
            Actions =
            {
                new SpeakAction
                {
                    Sequence = 0,
                    Text = "Here's today's news. Robotics club opens a new community lab.",
                    Voice = "griffin"
                },
                new InvokeNativeSkillAction
                {
                    Sequence = 1,
                    SkillName = "chitchat-skill",
                    Payload = new Dictionary<string, object?>
                    {
                        ["cloudSkill"] = "news",
                        ["skillId"] = "news",
                        ["kitchen_mute_speech"] = muteSpoken,
                        ["news_sections"] = new[]
                        {
                            Section("news_intro", "Here's today's news.", "news", "news-intro, no-eye-end"),
                            Section("news_headline", "Robotics club opens a new community lab.", "news", "news-stinger")
                        }
                    }
                }
            }
        };
    }

    private static JsonDocument MapSkillAction(ResponsePlan plan)
    {
        var replies = ResponsePlanToSocketMessagesMapper.Map(
            plan,
            Turn("tell me the news"),
            new CloudSession(),
            emitSkillActions: true);
        return replies
            .Select(reply => reply.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => JsonDocument.Parse(text!))
            .First(document => document.RootElement.GetProperty("type").GetString() == "SKILL_ACTION");
    }

    private static string JoinChildEsml(JsonDocument skillAction)
    {
        var children = skillAction.RootElement
            .GetProperty("data")
            .GetProperty("action")
            .GetProperty("config")
            .GetProperty("jcp")
            .GetProperty("children");
        return string.Join(
            ' ',
            children.EnumerateArray()
                .Select(child => child.GetProperty("config").GetProperty("play").GetProperty("esml").GetString()));
    }

    private static Dictionary<string, object?> Section(string kind, string text, string animCat, string animMeta)
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["kind"] = kind,
            ["text"] = text,
            ["anim_cat"] = animCat,
            ["anim_meta"] = animMeta,
            ["hold_timeout"] = 6
        };
    }

    private static TurnContext Turn(string transcript)
    {
        return new TurnContext
        {
            NormalizedTranscript = transcript,
            Attributes = new Dictionary<string, object?>
            {
                ["transID"] = "trans-kitchen-news",
                ["messageType"] = "LISTEN",
                ["listenRules"] = new[] { "launch" }
            }
        };
    }
}
