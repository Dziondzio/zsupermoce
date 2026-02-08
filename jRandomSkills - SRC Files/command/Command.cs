using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using jRandomSkills.src.player;
using static jRandomSkills.jRandomSkills;

namespace jRandomSkills
{
    public static class Command
    {
        public static void Load()
        {
            Instance?.AddCommand($"css_useskill", "Use/Type skill", Command_UseTypeSkill);
            Instance?.AddCommand($"css_testskill", "Test skill", Command_TestSkill);
        }

        [RequiresPermissions("@css/root")]
        [CommandHelper(minArgs: 1, usage: "<skill_name>", whoCanExecute: CommandUsage.CLIENT_ONLY)]
        private static void Command_TestSkill(CCSPlayerController? player, CommandInfo info)
        {
            if (player == null || Instance?.IsPlayerValid(player) == false) return;

            string arg = info.ArgString.Trim();
            if (string.IsNullOrEmpty(arg))
            {
                player.PrintToChat($" {ChatColors.Red}Podaj nazwę skilla!");
                return;
            }

            jSkill_SkillInfo? skillInfo = null;

            if (int.TryParse(arg, out int skillId) && Enum.IsDefined(typeof(Skills), skillId))
            {
                Skills skillEnum = (Skills)skillId;
                skillInfo = SkillData.Skills.FirstOrDefault(s => s.Skill == skillEnum);
            }

            if (skillInfo == null)
            {
                skillInfo = SkillData.Skills.FirstOrDefault(s =>
                    s.Name.Contains(arg, StringComparison.OrdinalIgnoreCase) ||
                    s.Skill.ToString().Equals(arg, StringComparison.OrdinalIgnoreCase));
            }

            if (skillInfo == null)
            {
                player.PrintToChat($" {ChatColors.Red}Nie znaleziono skilla: {arg}");
                return;
            }

            var playerInfo = Instance?.SkillPlayer.FirstOrDefault(p => p.SteamID == player.SteamID);
            if (playerInfo == null) return;

            if (playerInfo.Skill != Skills.None)
            {
                Instance?.SkillAction(playerInfo.Skill.ToString(), "DisableSkill", [player]);
            }

            playerInfo.Skill = skillInfo.Skill;
            playerInfo.SpecialSkill = Skills.None;
            playerInfo.SkillChance = 1;
            playerInfo.RandomPercentage = "";

            Instance?.SkillAction(skillInfo.Skill.ToString(), "EnableSkill", [player]);

            player.PrintToChat($" {ChatColors.Green}Ustawiono skill: {skillInfo.Name}");
            Debug.WriteToDebug($"{player.PlayerName} ustawił sobie skill {skillInfo.Name} przez komendę testskill");
        }

        [CommandHelper(minArgs: 0, whoCanExecute: CommandUsage.CLIENT_ONLY)]
        private static void Command_UseTypeSkill(CCSPlayerController? player, CommandInfo _)
        {
            if (player == null || Instance?.GameRules?.FreezePeriod == true) return;
            var playerInfo = Instance?.SkillPlayer.FirstOrDefault(p => p.SteamID == player.SteamID);
            if (playerInfo == null || playerInfo.IsDrawing) return;

            var playerPawn = player.PlayerPawn.Value;
            if (playerPawn?.CBodyComponent == null) return;
            if (!player.IsValid || !player.PawnIsAlive) return;

            string[] commands = _.ArgString.Trim().Split(" ", StringSplitOptions.RemoveEmptyEntries);
            Debug.WriteToDebug($"{player.PlayerName} użył {playerInfo.Skill}");
            if (commands == null || commands.Length == 0)
                Instance?.SkillAction(playerInfo.Skill.ToString(), "UseSkill", [player]);
            else
                Instance?.SkillAction(playerInfo.Skill.ToString(), "TypeSkill", [player, commands]);
        }
    }
}