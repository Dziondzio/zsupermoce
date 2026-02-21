using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using jRandomSkills.src.player;
using System.Collections.Concurrent;
using static jRandomSkills.jRandomSkills;

namespace jRandomSkills
{
    public class TimeDewastor : ISkill
    {
        private const Skills skillName = Skills.TimeDewastor;
        private static readonly ConcurrentDictionary<ulong, PlayerSkillInfo> SkillPlayerInfo = new();
        private static readonly Dictionary<ulong, float> originalRoundTimes = new();
        private const int COOLDOWN_SECONDS = 45;

        public static void LoadSkill()
        {
            SkillUtils.RegisterSkill(skillName, "Time Dewastor", "Możesz skracać i wydłużać czas meczu", "#8B4513");
        }

        public static void NewRound()
        {
            // Zapisz oryginalny czas rundy dla wszystkich graczy z tym skillem
            var conVar = ConVar.Find("mp_roundtime");
            if (conVar != null)
            {
                var currentTime = conVar.GetPrimitiveValue<float>();
                foreach (var player in SkillPlayerInfo.Keys)
                {
                    if (!originalRoundTimes.ContainsKey(player))
                        originalRoundTimes[player] = currentTime;
                }
            }
        }

        public static void PlayerDeath(EventPlayerDeath @event)
        {
            var player = @event.Userid;
            if (player == null || !player.IsValid) return;

            SkillPlayerInfo.TryRemove(player.SteamID, out _);
            originalRoundTimes.Remove(player.SteamID);
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
                        UpdateHUD(player, skillInfo);
            }
        }

        public static void EnableSkill(CCSPlayerController player)
        {
            SkillPlayerInfo.TryAdd(player.SteamID, new PlayerSkillInfo
            {
                SteamID = player.SteamID,
                CanUse = true,
                Cooldown = DateTime.MinValue,
                CurrentTimeModifier = 0
            });
        }

        public static void DisableSkill(CCSPlayerController player)
        {
            // Przywróć oryginalny czas jeśli był zmieniony
            if (originalRoundTimes.TryGetValue(player.SteamID, out var originalTime))
            {
                Server.ExecuteCommand($"mp_roundtime {originalTime}");
            }
            
            SkillPlayerInfo.TryRemove(player.SteamID, out _);
            originalRoundTimes.Remove(player.SteamID);
        }

        private static void UpdateHUD(CCSPlayerController player, PlayerSkillInfo skillInfo)
        {
            float cooldown = 0;
            if (skillInfo != null)
            {
                float time = (int)(skillInfo.Cooldown.AddSeconds(COOLDOWN_SECONDS) - DateTime.Now).TotalSeconds;
                cooldown = Math.Max(time, 0);

                if (cooldown == 0 && skillInfo?.CanUse == false)
                    skillInfo.CanUse = true;
            }

            var skillData = SkillData.Skills.FirstOrDefault(s => s.Skill == skillName);
            if (skillData == null) return;

            string skillLine = $"<font class='fontSize-m' class='fontWeight-Bold' color='{skillData.Color}'>{skillData.Name}</font> <br>";
            string modifierText = skillInfo?.CurrentTimeModifier switch
            {
                > 0 => $"<font class='fontSize-m' color='#00FF00'>+{skillInfo.CurrentTimeModifier:0.0}min</font>",
                < 0 => $"<font class='fontSize-m' color='#FF0000'>{skillInfo.CurrentTimeModifier:0.0}min</font>",
                _ => "<font class='fontSize-m' color='#FFFFFF'>Normalny</font>"
            };
            string remainingLine = cooldown != 0 
                ? $"<font class='fontSize-m' color='#FFFFFF'>Poczekaj <font color='#FF0000'>{cooldown}</font> sek.</font>" 
                : $"<font class='fontSize-s' class='fontWeight-Bold' color='#FFFFFF'>{skillData.Description}</font><br><font class='fontSize-s' color='#FFFFFF'>Czas: {modifierText}</font><br><font class='fontSize-s' color='#ffffff'>Wciśnij INSPEKT by zmienić</font>";

            var hudContent = skillLine + remainingLine;
            player.PrintToCenterHtml(hudContent);
        }

        public static void UseSkill(CCSPlayerController player)
        {
            if (SkillPlayerInfo.TryGetValue(player.SteamID, out var skillInfo))
            {
                if (!player.IsValid || !player.PawnIsAlive) return;
                if (skillInfo.CanUse)
                {
                    var menu = new ConcurrentBag<(string, string)>
                    {
                        ("Zwiększ czas o 1 minutę", "1"),
                        ("Zmniejsz czas o 1 minutę", "-1"),
                        ("Zresetuj czas do domyślnego", "0")
                    };
                    SkillUtils.CreateMenu(player, menu, "Wybierz opcję zmiany czasu:");
                }
            }
        }

        public static void TypeSkill(CCSPlayerController player, string[] commands)
        {
            if (player == null || !player.IsValid || !player.PawnIsAlive) return;
            var playerInfo = Instance?.SkillPlayer.FirstOrDefault(p => p.SteamID == player.SteamID);
            if (playerInfo?.Skill != skillName) return;

            if (commands.Length == 0 || !int.TryParse(commands[0], out int timeChange))
            {
                player.PrintToChat("Nieprawidłowy wybór!");
                return;
            }

            if (SkillPlayerInfo.TryGetValue(player.SteamID, out var skillInfo))
            {
                if (skillInfo.CanUse)
                {
                    skillInfo.CanUse = false;
                    skillInfo.Cooldown = DateTime.Now;

                    if (Instance?.GameRules == null) return;

                    float currentTime = Instance.GameRules.RoundTime / 60.0f; // Konwertuj sekundy na minuty
                    float newTime;

                    if (timeChange == 0)
                    {
                        newTime = 1.75f; // Domyślny czas w minutach
                        skillInfo.CurrentTimeModifier = 0;
                    }
                    else
                    {
                        newTime = Math.Max(0.5f, currentTime + (timeChange / 60.0f)); // timeChange w minutach
                        skillInfo.CurrentTimeModifier += timeChange;
                    }

                    // Zmień czas rundy (w sekundach)
                    Instance.GameRules.RoundTime = (int)(newTime * 60.0f);
                    
                    player.PrintToChat($"[Time Dewastor] Czas rundy zmieniony na {newTime:0.0} minut!");
                }
            }
        }

        public class PlayerSkillInfo
        {
            public ulong SteamID { get; set; }
            public bool CanUse { get; set; }
            public DateTime Cooldown { get; set; }
            public float CurrentTimeModifier { get; set; }
        }
    }
}