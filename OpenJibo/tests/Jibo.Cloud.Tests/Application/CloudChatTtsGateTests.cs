using Jibo.Cloud.Application.Services;
using Jibo.Runtime.Abstractions;

namespace Jibo.Cloud.Tests.Application;

public sealed class CloudChatTtsGateTests
{
    [Fact]
    public void IsEligible_AllowsChitchatWithoutCloudSkill()
    {
        var plan = Plan("chitchat-skill", "Hello there.", cloudSkill: null);

        Assert.True(CloudChatTtsGate.IsEligible(plan));
    }

    [Fact]
    public void IsEligible_RejectsNativeBeSkills()
    {
        var plan = Plan("@be/clock", "Okay.");

        Assert.False(CloudChatTtsGate.IsEligible(plan));
    }

    [Fact]
    public void IsEligible_RejectsNimbusCloudSkills()
    {
        var plan = Plan("chitchat-skill", "Here is the news.", cloudSkill: "news");

        Assert.False(CloudChatTtsGate.IsEligible(plan));
    }

    [Fact]
    public void IsEligible_RejectsKnowledgeSearchAnswerSkill()
    {
        var plan = Plan("chitchat-skill", "According to wikipedia.", cloudSkill: "answer");

        Assert.False(CloudChatTtsGate.IsEligible(plan));
    }

    [Fact]
    public void IsEligible_RejectsEmptySpeech()
    {
        var plan = Plan("chitchat-skill", "  ");

        Assert.False(CloudChatTtsGate.IsEligible(plan));
    }

    [Fact]
    public void IsKitchenEligible_AllowsCloneOwnedCloudSkills()
    {
        foreach (var cloudSkill in new[] { "weather", "news", "commute", "calendar", "personal_report", "answer" })
        {
            var plan = Plan("chitchat-skill", "Spoken line.", cloudSkill: cloudSkill);

            Assert.False(CloudChatTtsGate.IsEligible(plan));
            Assert.True(CloudChatTtsGate.IsKitchenEligible(plan), cloudSkill);
        }
    }

    [Fact]
    public void IsKitchenEligible_AllowsNewsAndReportSkillNames()
    {
        foreach (var skillName in new[] { "news", "report-skill" })
        {
            var plan = Plan(skillName, "Spoken line.", cloudSkill: "news");

            Assert.True(CloudChatTtsGate.IsKitchenEligible(plan), skillName);
        }
    }

    [Fact]
    public void IsKitchenEligible_StillRejectsUnknownCloudSkills()
    {
        var plan = Plan("chitchat-skill", "Here is a fact.", cloudSkill: "trivia");

        Assert.False(CloudChatTtsGate.IsKitchenEligible(plan));
    }

    [Fact]
    public void IsKitchenEligible_RejectsDanceMusicAnim()
    {
        var plan = Plan("chitchat-skill", "You got it.");
        plan.Actions.OfType<InvokeNativeSkillAction>().Single().Payload["esml"] =
            "<speak>Okay.<break size='0.2'/> Watch this.<anim cat='dance' filter='music, rom-upbeat' /></speak>";

        Assert.True(CloudChatTtsGate.RobotOwnsPlayback(plan));
        Assert.False(CloudChatTtsGate.IsEligible(plan));
        Assert.False(CloudChatTtsGate.IsKitchenEligible(plan));
    }

    [Fact]
    public void IsKitchenEligible_StillRejectsNativeBeSkills()
    {
        var plan = Plan("@be/clock", "Okay.");

        Assert.False(CloudChatTtsGate.IsKitchenEligible(plan));
    }

    private static ResponsePlan Plan(string skillName, string text, string? cloudSkill = null)
    {
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(cloudSkill))
            payload["cloudSkill"] = cloudSkill;

        return new ResponsePlan
        {
            IntentName = "chat",
            Actions =
            {
                new SpeakAction { Sequence = 0, Text = text, Voice = "griffin" },
                new InvokeNativeSkillAction
                {
                    Sequence = 1,
                    SkillName = skillName,
                    Payload = payload
                }
            }
        };
    }
}
