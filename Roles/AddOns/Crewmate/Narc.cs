using System;
using AmongUs.GameOptions;
using TOHE.Roles.Core.AssignManager;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Impostor;
using TOHE.Roles.Double;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE;

public static class NarcManager
{
    /// <summary>
    /// List of roles that can be Narc
    /// </summary>
    public static List<CustomRoles> SelectedNarcRoles()
    {
        var list  = new List<CustomRoles>();
        foreach (var role in CustomRolesHelper.AllRoles
                                    .Where(r => r.IsEnable() && !r.IsVanilla() && !r.IsGhostRole() && !r.IsAdditionRole()))
        {
            if (role is CustomRoles.Mini && Mini.EvilMiniSpawnChances.GetInt() > 0)
            {
                list.Add(CustomRoles.EvilMini);
                continue;
            }
            if (!role.IsImpostor() && !role.IsMadmate()) continue;
            if (role is CustomRoles.Arrogance && Arrogance.BardChance.GetInt() > 0)
                list.Add(CustomRoles.Bard);
            if (role is CustomRoles.Parasite 
                || (role is CustomRoles.Crewpostor && !Crewpostor.AlliesKnowCrewpostor.GetBool()))
                continue;
            if (role is CustomRoles.PhantomTOHE) continue;
            
            list.Add(role);
        }
        return list;
    }

    /// <summary>
    /// Checks if a role can be Narc
    /// </summary>
    public static bool CanBeNarc(this CustomRoles role) => SelectedNarcRoles().Contains(role);
    
    //===========================SETUP================================\\
    public static CustomRoles RoleForNarcToSpawnAs;
    public static bool IsNarcAssigned() => RoleForNarcToSpawnAs != CustomRoles.NotAssigned;
    public static bool AssignedToHost = false;
    //==================================================================\\
    private static OptionItem NarcSpawnChance;
    private static OptionItem NarcCanUseSabotage;
    private static OptionItem NarcHasCrewVision;
    //public static OptionItem MadmateCanBeNarc;
    public static OptionItem ImpsCanKillEachOther;

    public static void SetUpOptionsForNarc(int id = 31200, CustomRoles role = CustomRoles.Narc, CustomGameMode customGameMode = CustomGameMode.Standard, TabGroup tab = TabGroup.Addons)
    {
        var spawnOption = StringOptionItem.Create(id, role.ToString(), EnumHelper.GetAllNames<RatesZeroOne>(), 0, tab, false).SetColor(Utils.GetRoleColor(role))
            .SetHeader(true)
            .SetGameMode(customGameMode) as StringOptionItem;

        NarcSpawnChance = IntegerOptionItem.Create(id + 2, "ChanceToSpawn", new(0, 100, 5), 65, tab, false)
            .SetParent(spawnOption)
            .SetValueFormat(OptionFormat.Percent)
            .SetGameMode(customGameMode);

        NarcCanUseSabotage = BooleanOptionItem.Create(id + 3, "NarcCanUseSabotage", true, tab, false)
            .SetParent(spawnOption)
            .SetGameMode(customGameMode);

        NarcHasCrewVision = BooleanOptionItem.Create(id + 4, "NarcHasCrewVision", false, tab, false)
            .SetParent(spawnOption)
            .SetGameMode(customGameMode);

        //MadmateCanBeNarc = BooleanOptionItem.Create(id + 5, "MadmateCanBeNarc", false, tab, false)
        //    .SetParent(spawnOption)
        //    .SetGameMode(customGameMode);
        
        ImpsCanKillEachOther = BooleanOptionItem.Create(id + 6, "ImpsCanKillEachOther", false, tab, false)
            .SetParent(spawnOption)
            .SetGameMode(customGameMode);        


        var countOption = IntegerOptionItem.Create(id + 1, "Maximum", new(1, 1, 1), 1, tab, false)
            .SetParent(spawnOption)
            .SetHidden(true)
            .SetGameMode(customGameMode);

        CustomRoleSpawnChances.Add(role, spawnOption);
        CustomRoleCounts.Add(role, countOption);
    }

    public static void InitForNarc()
    {
        if (AssignedToHost) return;
        RoleForNarcToSpawnAs = CustomRoles.NotAssigned;

        int value = IRandom.Instance.Next(1, 100);

        if (value <= NarcSpawnChance.GetInt() && CustomRoles.Narc.IsEnable())
        {
            if (!SelectedNarcRoles().Any()) return;
            var RolesToSelect = SelectedNarcRoles().Shuffle().Shuffle().ToList();
            RoleForNarcToSpawnAs = RolesToSelect.RandomElement();
            Logger.Info("Select Role for Narc:" + RoleForNarcToSpawnAs.ToString(), "NarcManager");
        }
    }

    public static void ApplyGameOptions(IGameOptions opt, PlayerControl player)
    {
        float vision = Utils.IsActive(SystemTypes.Electrical) ? Main.DefaultCrewmateVision / 5 : Main.DefaultCrewmateVision;
        if (NarcHasCrewVision.GetBool())
        {
            opt.SetVision(true);
            opt.SetFloat(FloatOptionNames.CrewLightMod, Main.DefaultCrewmateVision);
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, vision);
        }
    }

    public static bool NarcCanUseKillButton(PlayerControl pc) 
        => !Main.AllAlivePlayerControls.Any(x => x.GetCustomRole().IsImpostorTeamV3() && !x.IsPlayerCrewmateTeam()) || ImpsCanKillEachOther.GetBool();
    public static bool CantUseSabotage(PlayerControl pc) => pc.Is(CustomRoles.Narc) && !NarcCanUseSabotage.GetBool();

    /// <summary>
    /// Checks whether a player is Sheriff or ChiefOfPolice and not converted
    /// </summary>
    public static bool IsPolice(this PlayerControl player)
        => (player.Is(CustomRoles.Sheriff) || player.Is(CustomRoles.ChiefOfPolice))
            && player.IsPlayerCrewmateTeam() && !CopyCat.playerIdList.Contains(player.PlayerId);
    
    public static bool KnowRoleOfTarget(PlayerControl seer, PlayerControl target)
    {
        return (seer.IsPolice() && target.Is(CustomRoles.Narc)) || 
            (seer.Is(CustomRoles.Narc) && target.IsPolice());
    }

    public static string NarcAndPoliceSeeColor(PlayerControl seer, PlayerControl target)
    {
        var color = "";
        if (seer.Is(CustomRoles.Narc) && target.IsPolice())
            color = Main.roleColors[target.GetCustomRole()];
        if (seer.IsPolice() && target.Is(CustomRoles.Narc))
            color = Main.roleColors[CustomRoles.Narc];

        return color;
    }

    /// <summary>
    /// Checks if killer and target are teammates and should not kill each other
    /// </summary>
    public static bool CheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.CheckImpCanSeeAllies(CheckAsSeer: true) 
            && target.CheckImpCanSeeAllies(CheckAsTarget: true) 
            && !ImpsCanKillEachOther.GetBool())
            return false;

        if ((killer.IsPolice() && target.Is(CustomRoles.Narc)) || 
            (killer.Is(CustomRoles.Narc) && target.IsPolice()))
            return false;

        return true;
    }

    public static bool CheckBlockGuesses(PlayerControl guesser, PlayerControl target, bool isUI = false)
    {
        if (guesser.Is(CustomRoles.Narc) && target.IsPlayerCrewmateTeam())
        {
            if (target.Is(CustomRoles.Sheriff))
            {
                guesser.ShowInfoMessage(isUI, CopyCat.playerIdList.Contains(target.PlayerId) ? GetString("GuessImmune") : GetString("GuessSheriff"));
                return true;
            }
            if (target.Is(CustomRoles.ChiefOfPolice))
            {
                guesser.ShowInfoMessage(isUI, CopyCat.playerIdList.Contains(target.PlayerId) ? GetString("GuessImmune") : GetString("GuessCoP"));
                return true;
            }
        }

        if (guesser.IsPolice() && target.Is(CustomRoles.Narc))
        {
            guesser.ShowInfoMessage(isUI, GetString("GuessNarc"));
            return true;
        }

        return false;
    }
}
