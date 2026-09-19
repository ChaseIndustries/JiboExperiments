using System.Collections.Frozen;
using Jibo.Runtime.Abstractions;

namespace Jibo.Cloud.Application.Services;

public static class CloudChatTtsGate
{
    public static FrozenSet<string> CloneOwnedCloudSkills { get; } =
        FrozenSet.ToFrozenSet(["weather", "news", "commute", "calendar", "personal_report", "answer"],
            StringComparer.OrdinalIgnoreCase);

    public static bool IsEligible(ResponsePlan plan)
    {
        if (RobotOwnsPlayback(plan))
            return false;

        var speak = plan.Actions.OfType<SpeakAction>().FirstOrDefault();
        if (string.IsNullOrWhiteSpace(speak?.Text))
            return false;

        var skill = plan.Actions.OfType<InvokeNativeSkillAction>().FirstOrDefault();
        var skillName = skill?.SkillName ?? string.Empty;
        if (skillName.StartsWith("@be/", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(skillName) &&
            !string.Equals(skillName, "chitchat-skill", StringComparison.OrdinalIgnoreCase))
            return false;

        return string.IsNullOrWhiteSpace(ReadCloudSkill(skill));
    }

    public static bool IsKitchenEligible(ResponsePlan plan)
    {
        if (RobotOwnsPlayback(plan))
            return false;

        if (IsEligible(plan))
            return true;

        var speak = plan.Actions.OfType<SpeakAction>().FirstOrDefault();
        if (string.IsNullOrWhiteSpace(speak?.Text))
            return false;

        var skill = plan.Actions.OfType<InvokeNativeSkillAction>().FirstOrDefault();
        var skillName = skill?.SkillName ?? string.Empty;
        if (skillName.StartsWith("@be/", StringComparison.OrdinalIgnoreCase))
            return false;

        return IsCloneOwnedCloudSkill(skill);
    }

    public static bool IsWeather(InvokeNativeSkillAction? skill)
    {
        return IsCloneOwnedCloudSkill(skill, "weather");
    }

    public static bool RobotOwnsPlayback(ResponsePlan plan)
    {
        var skill = plan.Actions.OfType<InvokeNativeSkillAction>().FirstOrDefault();
        if (skill?.Payload is null)
            return false;

        if (!skill.Payload.TryGetValue("esml", out var esmlValue) || esmlValue is not string esml)
            return false;

        return esml.Contains("cat='dance'", StringComparison.OrdinalIgnoreCase) ||
               esml.Contains("cat=\"dance\"", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsCloneOwnedCloudSkill(InvokeNativeSkillAction? skill, string? requiredSkill = null)
    {
        var cloudSkill = ReadCloudSkill(skill);
        if (string.IsNullOrWhiteSpace(cloudSkill) || !CloneOwnedCloudSkills.Contains(cloudSkill))
            return false;

        return requiredSkill is null ||
               string.Equals(cloudSkill, requiredSkill, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadCloudSkill(InvokeNativeSkillAction? skill)
    {
        if (skill?.Payload is null)
            return null;

        return skill.Payload.TryGetValue("cloudSkill", out var value)
            ? value?.ToString()
            : null;
    }
}
