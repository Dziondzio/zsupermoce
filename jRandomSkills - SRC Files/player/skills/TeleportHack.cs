using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using jRandomSkills.src.player;
using System.Collections.Concurrent;
using static jRandomSkills.jRandomSkills;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace jRandomSkills
{
    public class TeleportHack : ISkill
    {
        private const Skills skillName = Skills.TeleportHack;
        private static readonly ConcurrentDictionary<ulong, PlayerSkillInfo> SkillPlayerInfo = new();
        private const int COOLDOWN_SECONDS = 45;
        private const float MAX_TELEPORT_DISTANCE = 1000f;

        public static void LoadSkill()
        {
            SkillUtils.RegisterSkill(skillName, "Teleport Hack", "Teleportuj się do miejsca, w które celujesz", "#FF4500");
        }

        public static void NewRound()
        {
            SkillPlayerInfo.Clear();
        }

        public static void PlayerDeath(EventPlayerDeath @event)
        {
            var player = @event.Userid;
            if (player == null || !player.IsValid) return;

            SkillPlayerInfo.TryRemove(player.SteamID, out _);
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
                Cooldown = DateTime.MinValue
            });
        }

        public static void DisableSkill(CCSPlayerController player)
        {
            SkillPlayerInfo.TryRemove(player.SteamID, out _);
        }

        public static void UseSkill(CCSPlayerController player)
        {
            if (SkillPlayerInfo.TryGetValue(player.SteamID, out var skillInfo))
            {
                if (!player.IsValid || !player.PawnIsAlive) return;
                if (skillInfo.CanUse)
                {
                    skillInfo.CanUse = false;
                    skillInfo.Cooldown = DateTime.Now;

                    var playerPawn = player.PlayerPawn?.Value;
                    if (playerPawn == null || !playerPawn.IsValid) return;

                    // Pobierz pozycję i kąt gracza
                    var startPos = playerPawn.AbsOrigin;
                    var eyeAngles = playerPawn.EyeAngles;
                    
                    if (startPos == null || eyeAngles == null) return;

                    // Oblicz kierunek patrzenia
                    var forward = SkillUtils.GetForwardVector(eyeAngles);
                    
                    // Oblicz pozycję teleportacji (maksymalnie 1000 jednostek przed graczem)
                    var teleportPos = startPos + forward * MAX_TELEPORT_DISTANCE;
                    
                    // Sprawdź czy miejsce jest bezpieczne (nie w ścianie)
                    if (!IsSafeTeleportPosition(teleportPos, playerPawn))
                    {
                        // Spróbuj znaleźć bezpieczne miejsce bliżej gracza
                        for (float distance = MAX_TELEPORT_DISTANCE; distance >= 100f; distance -= 100f)
                        {
                            teleportPos = startPos + forward * distance;
                            if (IsSafeTeleportPosition(teleportPos, playerPawn))
                                break;
                        }
                        
                        // Jeśli nadal niebezpieczne, nie teleportuj
                        if (!IsSafeTeleportPosition(teleportPos, playerPawn))
                        {
                            player.PrintToChat("[Teleport Hack] Nie można teleportować - zbyt blisko ściany!");
                            skillInfo.CanUse = true; // Zwróć cooldown
                            return;
                        }
                    }

                    // Wykonaj teleportację
                    TeleportPlayer(player, teleportPos);
                    
                    // Efekt dźwiękowy i wizualny
                    player.ExecuteClientCommand("play buttons/button24");
                    
                    player.PrintToChat("[Teleport Hack] Teleportacja zakończona!");
                    
                    // Dodaj efekt dymu na miejscu teleportacji
                    CreateTeleportEffect(teleportPos);
                }
            }
        }

        private static bool IsSafeTeleportPosition(Vector position, CCSPlayerPawn playerPawn)
        {
            // Proste sprawdzenie - upewnij się, że pozycja nie jest zbyt blisko ścian
            // W prawdziwej implementacji można by użyć bardziej zaawansowanych testów kolizji
            
            // Sprawdź czy pozycja jest wystarczająco wysoko nad ziemią
            if (position.Z < -1000) return false; // Za nisko
            
            // Sprawdź czy nie jest zbyt blisko krawędzi mapy (proste ograniczenie)
            if (Math.Abs(position.X) > 5000 || Math.Abs(position.Y) > 5000) return false;
            
            return true;
        }

        private static void TeleportPlayer(CCSPlayerController player, Vector position)
        {
            if (player == null || !player.IsValid || player.PawnHealth <= 0) return;

            var pawn = player.PlayerPawn.Value;
            if (pawn == null || !pawn.IsValid) return;

            // Zachowaj aktualną rotację
            var currentAngles = pawn.EyeAngles;
            
            // Wykonaj teleportację
            pawn.Teleport(position, currentAngles, new Vector(0, 0, 0));

            // Tymczasowo zmień grupę kolizji aby uniknąć problemów
            pawn.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_DISSOLVING;
            pawn.Collision.CollisionAttribute.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_DISSOLVING;
            Utilities.SetStateChanged(player, "CCollisionProperty", "m_CollisionGroup");
            Utilities.SetStateChanged(player, "VPhysicsCollisionAttribute_t", "m_nCollisionGroup");

            // Przywróć normalną kolizję po klatce
            Server.NextFrame(() =>
            {
                if (!pawn.IsValid || pawn.LifeState != (int)LifeState_t.LIFE_ALIVE) return;

                pawn.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_PLAYER;
                pawn.Collision.CollisionAttribute.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_PLAYER;

                Utilities.SetStateChanged(player, "CCollisionProperty", "m_CollisionGroup");
                Utilities.SetStateChanged(player, "VPhysicsCollisionAttribute_t", "m_nCollisionGroup");
            });
        }

        private static void CreateTeleportEffect(Vector position)
        {
            // Stwórz efekt dymu na miejscu teleportacji
            var smoke = Utilities.CreateEntityByName<CSmokeGrenadeProjectile>("smokegrenade_projectile");
            if (smoke != null)
            {
                smoke.DispatchSpawn();
                smoke.Teleport(position, new QAngle(0, 0, 0), new Vector(0, 0, 0));
                
                // Zniszcz efekt po 2 sekundach
                Server.NextFrame(() =>
                {
                    Server.NextFrame(() =>
                    {
                        if (smoke != null && smoke.IsValid)
                            smoke.AcceptInput("Kill");
                    });
                });
            }
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
            string remainingLine = cooldown != 0 
                ? $"<font class='fontSize-m' color='#FFFFFF'>Poczekaj <font color='#FF0000'>{cooldown}</font> sek.</font>" 
                : $"<font class='fontSize-s' class='fontWeight-Bold' color='#FFFFFF'>{skillData.Description}</font><br><font class='fontSize-s' color='#ffffff'>Wciśnij INSPEKT by teleportować</font>";

            var hudContent = skillLine + remainingLine;
            player.PrintToCenterHtml(hudContent);
        }

        public class PlayerSkillInfo
        {
            public ulong SteamID { get; set; }
            public bool CanUse { get; set; }
            public DateTime Cooldown { get; set; }
        }
    }
}