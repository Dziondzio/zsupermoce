using System.Collections.Concurrent;
using System.Drawing;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using jRandomSkills.src.player;
using static jRandomSkills.jRandomSkills;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace jRandomSkills
{
    public class RainbowShots : ISkill
    {
        private const Skills skillName = Skills.RainbowShots;
        private static readonly Dictionary<int, List<Color>> rainbowColors = new()
        {
            { 0, new List<Color> { Color.Red, Color.Orange, Color.Yellow, Color.Green, Color.Blue, Color.Purple, Color.Pink } },
            { 1, new List<Color> { Color.Cyan, Color.Magenta, Color.Lime, Color.Teal, Color.Indigo, Color.Violet, Color.Gold } },
            { 2, new List<Color> { Color.Silver, Color.HotPink, Color.SpringGreen, Color.SkyBlue, Color.Tomato, Color.Orchid, Color.Khaki } }
        };

        private static readonly Dictionary<ulong, int> playerTargets = new();
        private static readonly Dictionary<ulong, int> colorIndices = new();
        private static int globalColorIndex = 0;

        public static void LoadSkill()
        {
            SkillUtils.RegisterSkill(skillName, "Rainbow Shots", "Wybierz wroga i zobacz kolorowe linie jego strzałów", "#FF69B4");
        }

        public static void EnableSkill(CCSPlayerController player)
        {
            var enemies = GetEnemies(player);
            if (enemies.Count == 0)
            {
                player.PrintToChat("Nie ma wrogów do wyboru!");
                return;
            }

            var enemiesBag = new ConcurrentBag<(string, string)>();
            foreach (var enemy in enemies)
                enemiesBag.Add((enemy.Item2, enemy.Item1.ToString()));

            SkillUtils.CreateMenu(player, enemiesBag);
        }

        public static void TypeSkill(CCSPlayerController player, string[] commands)
        {
            if (player == null || !player.IsValid || !player.PawnIsAlive) return;
            var playerInfo = Instance?.SkillPlayer.FirstOrDefault(p => p.SteamID == player.SteamID);
            if (playerInfo?.Skill != skillName) return;

            if (commands.Length == 0 || !int.TryParse(commands[0], out int enemySlot))
            {
                player.PrintToChat("Nieprawidłowy wybór!");
                return;
            }

            var enemy = Utilities.GetPlayerFromSlot(enemySlot);
            if (enemy == null || !enemy.IsValid || enemy.TeamNum == player.TeamNum)
            {
                player.PrintToChat("Nieprawidłowy wróg!");
                return;
            }

            playerTargets[player.SteamID] = enemySlot;
            colorIndices[player.SteamID] = Instance?.Random.Next(rainbowColors.Count) ?? 0;
            player.PrintToChat($"Wybrałeś {enemy.PlayerName} - jego strzały będą kolorowe!");
        }

        public static void WeaponFire(EventWeaponFire @event)
        {
            if (@event.Userid == null || !@event.Userid.IsValid) return;

            var shooter = @event.Userid;
            
            // Sprawdź czy ktoś wybrał tego strzelca jako cel
            var targetPair = playerTargets.FirstOrDefault(p => p.Value == shooter.Slot);
            if (targetPair.Key == 0) return; // Nikt nie wybrał tego gracza
            
            var targeter = targetPair.Key;
            var targeterPlayer = Utilities.GetPlayerFromSteamId(targeter);
            if (targeterPlayer == null || !targeterPlayer.IsValid) return;

            // Pobierz pozycję strzelca
            var shooterPawn = shooter.PlayerPawn?.Value;
            if (shooterPawn == null) return;

            var startPos = shooterPawn.AbsOrigin;
            if (startPos == null) return;

            // Oblicz kierunek strzału na podstawie rotacji
            var eyeAngles = shooterPawn.EyeAngles;
            if (eyeAngles == null) return;

            var forward = AngleVectors(eyeAngles);
            var endPos = startPos + forward * 1000f; // Maksymalny zasięg linii

            // Wybierz kolor
            var colorSet = rainbowColors[colorIndices.GetValueOrDefault(targeter, 0)];
            var color = colorSet[globalColorIndex % colorSet.Count];
            globalColorIndex++;

            // Stwórz kolorową linię dla targetera
            CreateRainbowBeam(targeterPlayer, startPos, endPos, color);
        }

        public static void DisableSkill(CCSPlayerController player)
        {
            playerTargets.Remove(player.SteamID);
            colorIndices.Remove(player.SteamID);
        }

        private static List<(int, string)> GetEnemies(CCSPlayerController player)
        {
            var enemies = new List<(int, string)>();
            if (player.TeamNum == (byte)CsTeam.Spectator) return enemies;

            foreach (var p in Utilities.GetPlayers())
            {
                if (p.IsValid && p != player && p.TeamNum != player.TeamNum && p.TeamNum != (byte)CsTeam.Spectator && p.PawnIsAlive)
                {
                    var name = p.PlayerName;
                    if (name.Length > 15)
                        name = name.Substring(0, 14) + "...";
                    enemies.Add((p.Slot, name));
                }
            }
            return enemies;
        }

        private static void CreateRainbowBeam(CCSPlayerController targeter, Vector start, Vector end, Color color)
        {
            CBeam beam = Utilities.CreateEntityByName<CBeam>("beam");
            if (beam == null) return;

            beam.DispatchSpawn();
            if (!beam.IsValid) return;

            // Ustaw kolor tęczy z przezroczystością
            beam.Render = Color.FromArgb(150, color.R, color.G, color.B);
            beam.Width = 3.0f;
            beam.EndWidth = 3.0f;
            beam.Teleport(start);

            beam.EndPos.X = end.X;
            beam.EndPos.Y = end.Y;
            beam.EndPos.Z = end.Z;

            // Zniszcz linię po 0.5 sekundy
            Server.NextFrame(() =>
            {
                Server.NextFrame(() =>
                {
                    if (beam != null && beam.IsValid)
                        beam.AcceptInput("Kill");
                });
            });
        }

        private static Vector AngleVectors(QAngle angles)
        {
            var pitch = angles.X * Math.PI / 180.0;
            var yaw = angles.Y * Math.PI / 180.0;
            
            var cp = Math.Cos(pitch);
            var sp = Math.Sin(pitch);
            var cy = Math.Cos(yaw);
            var sy = Math.Sin(yaw);
            
            return new Vector((float)(cp * cy), (float)(cp * sy), (float)(-sp));
        }
    }
}