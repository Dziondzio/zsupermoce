using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using jRandomSkills.src.player;
using System.Collections.Concurrent;
using static jRandomSkills.jRandomSkills;

namespace jRandomSkills
{
    public class SmallCharacter : ISkill
    {
        private const Skills skillName = Skills.SmallCharacter;
        private static readonly ConcurrentDictionary<ulong, PlayerSkillInfo> SkillPlayerInfo = new();
        private const float SMALL_SCALE = 0.6f;
        private const float NORMAL_SCALE = 1.0f;
        private const int COOLDOWN_SECONDS = 60;
        private const int DURATION_SECONDS = 30;

        public static void LoadSkill()
        {
            SkillUtils.RegisterSkill(skillName, "Small Character", "Zmniejsz swoją postać, ale z ograniczeniami", "#9370DB");
        }

        public static void NewRound()
        {
            // Resetuj wszystkie efekty na początku rundy
            foreach (var playerInfo in SkillPlayerInfo)
            {
                var player = Utilities.GetPlayerFromSteamId(playerInfo.Key);
                if (player != null && player.IsValid)
                {
                    ResetPlayerScale(player);
                }
            }
            SkillPlayerInfo.Clear();
        }

        public static void PlayerDeath(EventPlayerDeath @event)
        {
            var player = @event.Userid;
            if (player == null || !player.IsValid) return;

            if (SkillPlayerInfo.TryRemove(player.SteamID, out _))
            {
                ResetPlayerScale(player);
            }
        }

        public static void OnTick()
        {
            if (SkillUtils.IsFreezetime()) return;
            
            foreach (var player in Utilities.GetPlayers())
            {
                if (player == null || !player.IsValid || !player.PawnIsAlive) continue;
                
                var playerInfo = Instance?.SkillPlayer.FirstOrDefault(p => p.SteamID == player.SteamID);
                if (playerInfo?.Skill == skillName)
                    if (SkillPlayerInfo.TryGetValue(player.SteamID, out var skillInfo))
                    {
                        if (skillInfo.IsActive && DateTime.Now >= skillInfo.EndTime)
                        {
                            DeactivateSkill(player, skillInfo);
                        }
                        UpdateHUD(player, skillInfo);
                    }
            }
        }

        public static void EnableSkill(CCSPlayerController player)
        {
            SkillPlayerInfo.TryAdd(player.SteamID, new PlayerSkillInfo
            {
                SteamID = player.SteamID,
                CanUse = true,
                Cooldown = DateTime.MinValue,
                IsActive = false,
                EndTime = DateTime.MinValue
            });
        }

        public static void DisableSkill(CCSPlayerController player)
        {
            if (SkillPlayerInfo.TryRemove(player.SteamID, out _))
            {
                ResetPlayerScale(player);
            }
        }

        public static void UseSkill(CCSPlayerController player)
        {
            if (SkillPlayerInfo.TryGetValue(player.SteamID, out var skillInfo))
            {
                if (!player.IsValid || !player.PawnIsAlive) return;
                if (skillInfo.CanUse && !skillInfo.IsActive)
                {
                    skillInfo.CanUse = false;
                    skillInfo.Cooldown = DateTime.Now;
                    skillInfo.IsActive = true;
                    skillInfo.EndTime = DateTime.Now.AddSeconds(DURATION_SECONDS);

                    // Zmniejsz gracza
                    SkillUtils.ChangePlayerScale(player, SMALL_SCALE);
                    
                    player.PrintToChat("[Small Character] Zmniejszyłeś się! Mniejsze obrażenia, ale szybszy ruch!");
                }
            }
        }

        public static void WeaponFire(EventWeaponFire @event)
        {
            var player = @event.Userid;
            if (player == null || !player.IsValid) return;

            var playerInfo = Instance?.SkillPlayer.FirstOrDefault(p => p.SteamID == player.SteamID);
            if (playerInfo?.Skill != skillName) return;

            if (SkillPlayerInfo.TryGetValue(player.SteamID, out var skillInfo))
            {
                if (skillInfo.IsActive)
                {
                    // Gracz strzela z mniejszymi obrażeniami
                }
            }
        }

        public static HookResult OnTakeDamage(CEntityInstance entity, CTakeDamageInfo info)
        {
            if (entity == null || entity.Entity == null || info == null || info.Attacker == null || info.Attacker.Value == null)
                return HookResult.Continue;

            CCSPlayerPawn attackerPawn = new(info.Attacker.Value.Handle);
            CCSPlayerPawn victimPawn = new(entity.Handle);

            if (attackerPawn.DesignerName != "player" || victimPawn.DesignerName != "player")
                return HookResult.Continue;

            if (attackerPawn == null || attackerPawn.Controller?.Value == null || victimPawn == null || victimPawn.Controller?.Value == null)
                return HookResult.Continue;

            CCSPlayerController attacker = attackerPawn.Controller.Value.As<CCSPlayerController>();

            var playerInfo = Instance?.SkillPlayer.FirstOrDefault(p => p.SteamID == attacker.SteamID);
            if (playerInfo == null) return HookResult.Continue;

            if (playerInfo.Skill == skillName)
            {
                // Zmniejsz obrażenia o 30%
                info.Damage *= 0.7f;
            }
            return HookResult.Continue;
        }

        private static void DeactivateSkill(CCSPlayerController player, PlayerSkillInfo skillInfo)
        {
            if (!skillInfo.IsActive) return;
            
            skillInfo.IsActive = false;
            skillInfo.EndTime = DateTime.MinValue;
            
            // Przywróć normalny rozmiar
            ResetPlayerScale(player);
            
            player.PrintToChat("[Small Character] Wróciłeś do normalnego rozmiaru!");
        }

        private static void ResetPlayerScale(CCSPlayerController player)
        {
            if (player == null || !player.IsValid) return;
            SkillUtils.ChangePlayerScale(player, NORMAL_SCALE);
        }

        private static void UpdateHUD(CCSPlayerController player, PlayerSkillInfo skillInfo)
        {
            float cooldown = 0;
            float remainingTime = 0;
            
            if (skillInfo != null)
            {
                if (skillInfo.IsActive)
                {
                    remainingTime = (int)(skillInfo.EndTime - DateTime.Now).TotalSeconds;
                    remainingTime = Math.Max(remainingTime, 0);
                }
                else
                {
                    float time = (int)(skillInfo.Cooldown.AddSeconds(COOLDOWN_SECONDS) - DateTime.Now).TotalSeconds;
                    cooldown = Math.Max(time, 0);

                    if (cooldown == 0 && skillInfo?.CanUse == false)
                        skillInfo.CanUse = true;
                }
            }

            var skillData = SkillData.Skills.FirstOrDefault(s => s.Skill == skillName);
            if (skillData == null) return;

            string skillLine = $"<font class='fontSize-m' class='fontWeight-Bold' color='{skillData.Color}'>{skillData.Name}</font> <br>";
            string statusLine;
            
            if (skillInfo.IsActive)
            {
                statusLine = $"<font class='fontSize-m' color='#00FF00'>Aktywny: <font color='#FFFF00'>{remainingTime}s</font></font><br><font class='fontSize-s' color='#FFFFFF'>Mniejsze obrażenia, szybszy ruch</font>";
            }
            else if (cooldown != 0)
            {
                statusLine = $"<font class='fontSize-m' color='#FFFFFF'>Poczekaj <font color='#FF0000'>{cooldown}</font> sek.</font>";
            }
            else
            {
                statusLine = $"<font class='fontSize-s' class='fontWeight-Bold' color='#FFFFFF'>{skillData.Description}</font><br><font class='fontSize-s' color='#ffffff'>Wciśnij INSPEKT by użyć</font>";
            }

            var hudContent = skillLine + statusLine;
            player.PrintToCenterHtml(hudContent);
        }

        public class PlayerSkillInfo
        {
            public ulong SteamID { get; set; }
            public bool CanUse { get; set; }
            public DateTime Cooldown { get; set; }
            public bool IsActive { get; set; }
            public DateTime EndTime { get; set; }
        }
    }
}