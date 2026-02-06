using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using jRandomSkills.src.player;
using System.Collections.Concurrent;
using static jRandomSkills.jRandomSkills;

namespace jRandomSkills
{
    public class Gambler : ISkill
    {
        private const Skills skillName = Skills.Gambler;

        public static void LoadSkill()
        {
            SkillUtils.RegisterSkill(skillName, "Polityk", "Wybierasz sobie supermoc", "#7eff47");
        }

        public static void NewRound()
        {
            foreach (var player in Utilities.GetPlayers())
                SkillUtils.CloseMenu(player);
        }

        public static void TypeSkill(CCSPlayerController player, string[] commands)
        {
            if (player == null || !player.IsValid || !player.PawnIsAlive) return;
            var playerInfo = Instance?.SkillPlayer.FirstOrDefault(p => p.SteamID == player.SteamID);
            if (playerInfo?.Skill != skillName) return;

            if (playerInfo.SkillChance == 1)
            {
                player.PrintToChat($" {ChatColors.Red}Twoja moc została już wykorzystana.");
                return;
            }

            var skill = SkillData.Skills.FirstOrDefault(s => s.Name.Equals(commands[0], StringComparison.OrdinalIgnoreCase) || s.Skill.ToString().Equals(commands[0], StringComparison.OrdinalIgnoreCase));
            if (skill == null)
            {
                player.PrintToChat($" {ChatColors.Red}Nie znaleziono takiej supermocy.");
                return;
            }
            Instance?.AddTimer(.1f, () =>
            {
                playerInfo.Skill = skill.Skill;
                if (skill.Skill != skillName)
                    playerInfo.SpecialSkill = skillName;
                playerInfo.SkillChance = 1;
                Instance?.SkillAction(skill.Skill.ToString(), "EnableSkill", [player]);
            });
        }

        public static void EnableSkill(CCSPlayerController player)
        {
            var playerInfo = Instance?.SkillPlayer.FirstOrDefault(p => p.SteamID == player.SteamID);
            if (playerInfo == null) return;
            playerInfo.SkillChance = 0;

            var skills = GetSkills(player);
            ConcurrentBag<(string, string)> menuItems = [];

            // Add up to 3 random skills
            for (int i = 0; i < 3 && skills.Count > 0; i++)
            {
                var index = Instance?.Random.Next(skills.Count) ?? 0;
                var selectedSkill = skills[index];
                menuItems.Add((selectedSkill.Name, selectedSkill.Skill.ToString()));
                skills.RemoveAt(index);
            }

            SkillUtils.CreateMenu(player, menuItems);
        }

        public static void DisableSkill(CCSPlayerController player)
        {
            SkillUtils.CloseMenu(player);
        }

        private static List<jSkill_SkillInfo> GetSkills(CCSPlayerController player)
        {
            var skillPlayer = Instance?.SkillPlayer.FirstOrDefault(p => p.SteamID == player.SteamID);
            if (skillPlayer == null) return [Event.noneSkill];

            List<jSkill_SkillInfo> skillList = [.. SkillData.Skills];
            skillList.RemoveAll(s => s?.Skill == skillPlayer?.Skill || s?.Skill == skillPlayer?.SpecialSkill || s?.Skill == Skills.None);

            if (player.Team == CsTeam.Terrorist)
                skillList.RemoveAll(s => SkillData.Skills.Any(s2 => s2.Name == s.Skill.ToString() && s.TeamNumber == 2));
            else
                skillList.RemoveAll(s => SkillData.Skills.Any(s2 => s2.Name == s.Skill.ToString() && s.TeamNumber == 1));

            return skillList.Count == 0 ? [Event.noneSkill] : skillList;
        }
    }
}