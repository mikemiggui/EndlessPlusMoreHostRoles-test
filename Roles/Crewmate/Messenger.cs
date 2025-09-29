using System;
using System.Collections.Generic;
using EHR.Crewmate;
using EHR.Modules;
using AmongUs.GameOptions;

namespace EHR.Crewmate;

internal class Messenger : RoleBase
{
    public static bool On;

    private static OptionItem AbilityExpires;
    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityUseGainWithEachMeeting;
    private static OptionItem MaxMessagedPlayersPerMeeting;
    private static OptionItem MaxMessagedPlayersAtOnce;
    private static OptionItem WhoSeesMessagedPlayers;
    private static OptionItem MessageCooldown;
    private static OptionItem VisionBuffAmount;
    private static OptionItem VisionBuffDuration;
    private static OptionItem KillCooldownPenaltyAmount;
    private static OptionItem KillCooldownPenaltyDuration;
    private static OptionItem NotificationStyle;

    public List<byte> MessagedPlayerIds;
    private int NumMessagedThisMeeting;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(65000)
            .AutoSetupOption(ref AbilityExpires, 0, new[] { "MessengerExpire.AfterMeeting", "MessengerExpire.Never" })
            .AutoSetupOption(ref AbilityUseLimit, 3f, new FloatValueRule(0, 20, 1), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachMeeting, 0f, new FloatValueRule(0, 5, 0.1f), OptionFormat.Times)
            .AutoSetupOption(ref MaxMessagedPlayersPerMeeting, 1, new IntegerValueRule(1, 14, 1), OptionFormat.Players)
            .AutoSetupOption(ref MaxMessagedPlayersAtOnce, 2, new IntegerValueRule(1, 14, 1), OptionFormat.Players)
            .AutoSetupOption(ref WhoSeesMessagedPlayers, 0, new[] { "MessengerOnly", "MessengerAndTarget", "AllCrewmates", "Everyone" })
            .AutoSetupOption(ref MessageCooldown, 25f, new FloatValueRule(2.5f, 60f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref VisionBuffAmount, 1.5f, new FloatValueRule(0.1f, 3f, 0.1f))
            .AutoSetupOption(ref VisionBuffDuration, 10f, new FloatValueRule(1f, 60f, 1f), OptionFormat.Seconds)
            .AutoSetupOption(ref KillCooldownPenaltyAmount, 5f, new FloatValueRule(0.1f, 15f, 0.1f), OptionFormat.Seconds)
            .AutoSetupOption(ref KillCooldownPenaltyDuration, 10f, new FloatValueRule(1f, 60f, 1f), OptionFormat.Seconds)
            .AutoSetupOption(ref NotificationStyle, 0, new[] { "Funny", "Serious" });
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        MessagedPlayerIds = new List<byte>();
        NumMessagedThisMeeting = 0;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.GetAbilityUseLimit() < 1) return true;
        if (NumMessagedThisMeeting >= MaxMessagedPlayersPerMeeting.GetInt()) return true;
        if (MessagedPlayerIds.Count >= MaxMessagedPlayersAtOnce.GetInt()) return true;
        if (MessagedPlayerIds.Contains(target.PlayerId)) return true;

        return killer.CheckDoubleTrigger(target, () =>
        {
            // Add target to messaged list
            MessagedPlayerIds.Add(target.PlayerId);
            NumMessagedThisMeeting++;

            // Apply effects based on alignment
            if (target.IsCrewmate())
            {
                Main.AllPlayerVision[target.PlayerId] += VisionBuffAmount.GetFloat();
                LateTask.New(() =>
                {
                    Main.AllPlayerVision[target.PlayerId] -= VisionBuffAmount.GetFloat();
                    target.MarkDirtySettings();
                }, VisionBuffDuration.GetFloat(), log: false);
            }
            else
            {
                target.SetKillCooldown(target.GetKillCooldown() + KillCooldownPenaltyAmount.GetFloat());
                LateTask.New(() =>
                {
                    target.SetKillCooldown(target.GetKillCooldown() - KillCooldownPenaltyAmount.GetFloat());
                }, KillCooldownPenaltyDuration.GetFloat(), log: false);
            }

            // Notification
            NotifyTarget(target);

            // Reduce ability use
            killer.RpcRemoveAbilityUse();
            killer.SetKillCooldown(MessageCooldown.GetFloat());
        });
    }

    private void NotifyTarget(PlayerControl target)
    {
        string message;
        if (NotificationStyle.GetInt() == 0) // Funny
        {
            int num = IRandom.Instance.Next(0, 5);
            message = $"MessengerMessage-{num}";
        }
        else // Serious
        {
            message = "You received a secret message!";
        }

        target.Notify(message);
    }

    public override void AfterMeetingTasks()
    {
        if (AbilityExpires.GetInt() == 0) MessagedPlayerIds.Clear();
        NumMessagedThisMeeting = 0;
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (!On || !meeting || !MessagedPlayerIds.Contains(target.PlayerId)) return string.Empty;

        switch (WhoSeesMessagedPlayers.GetInt())
        {
            case 0 when seer.Is(CustomRoles.Messenger):
            case 1 when seer.Is(CustomRoles.Messenger) || (MessagedPlayerIds.Contains(seer.PlayerId) && seer.PlayerId == target.PlayerId):
            case 2 when seer.IsCrewmate():
            case 3:
                return Translator.GetString("MessagedSuffix");
            default:
                return string.Empty;
        }
    }
}
