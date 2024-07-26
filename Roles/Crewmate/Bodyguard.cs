﻿using TOHE.Roles.Core;
using UnityEngine;
using static TOHE.Options;

namespace TOHE.Roles.Crewmate;

internal class Bodyguard : RoleBase
{
    //===========================SETUP================================\\
    private const int Id = 10300;
    public static bool HasEnabled => CustomRoleManager.HasEnabled(CustomRoles.Bodyguard);

    public override CustomRoles ThisRoleBase => CustomRoles.Crewmate;
    public override Custom_RoleType ThisRoleType => Custom_RoleType.CrewmateKilling;
    //==================================================================\\

    private static OptionItem ProtectRadiusOpt;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Bodyguard);
        ProtectRadiusOpt = FloatOptionItem.Create(10302, "BodyguardProtectRadius", new(0.5f, 5f, 0.5f), 1.5f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bodyguard])
            .SetValueFormat(OptionFormat.Multiplier);
    }
    public override bool CheckMurderOnOthersTarget(PlayerControl killer, PlayerControl target)
    {
        if (killer?.PlayerId == target.PlayerId || killer.Is(CustomRoles.Taskinator)) return false;

        var bodyguard = _Player;
        if (!bodyguard.IsAlive()) return false;

        var pos = target.transform.position;
        var dis = Vector2.Distance(pos, bodyguard.transform.position);
        if (dis > ProtectRadiusOpt.GetFloat()) return false;

        if (bodyguard.Is(CustomRoles.Madmate) && killer.GetCustomRole().IsImpostorTeam())
        {
            Logger.Info($"{bodyguard.GetRealName()} He was a impostor, so he chose to ignore the murder scene", "Bodyguard");
        }
        else if (bodyguard.CheckForInvalidMurdering(killer))
        {
            bodyguard.SetDeathReason(PlayerState.DeathReason.Sacrifice);
            bodyguard.RpcMurderPlayer(killer);
            bodyguard.SetRealKiller(killer);
            bodyguard.RpcMurderPlayer(bodyguard);
            Logger.Info($"{bodyguard.GetRealName()} Stand up and die with the gangster {killer.GetRealName()}", "Bodyguard");
            return true;
        }

        return false;
    }
}
