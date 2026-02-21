using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using jRandomSkills.src.player;
using static jRandomSkills.jRandomSkills;

namespace jRandomSkills
{
    public class JumpingJack : ISkill
    {
        private const Skills skillName = Skills.JumpingJack;

        public static void LoadSkill()
        {
            SkillUtils.RegisterSkill(skillName, "Kangurek", "Skakanie przywraca zdrowie", "#a86eff");
        }

        public static void PlayerJump(EventPlayerJump @event)
        {
            var player = @event.Userid;
            if (player == null || !player.IsValid || player.PlayerPawn.Value == null) return;
            var playerInfo = Instance?.SkillPlayer.FirstOrDefault(p => p.SteamID == player.SteamID);
            if (playerInfo?.Skill != skillName) return;
            
            int healthToAdd = Instance?.Random.Next(3, 7) ?? 3;
            SkillUtils.AddHealth(player.PlayerPawn.Value, healthToAdd);
        }
    }
}
