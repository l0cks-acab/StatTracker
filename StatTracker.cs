using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using MySql.Data.MySqlClient;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using global::Rust;

namespace Oxide.Plugins
{
    [Info("StatTracker", "locks", "1.0.0")]
    [Description("Tracks various player statistics and stores them in a local or remote database.")]
    public class StatTracker : CovalencePlugin
    {
        private Dictionary<string, PlayerData> playerStats;
        private Configuration config;
        private Timer webhookTimer;

        private class Configuration
        {
            public bool UseRemoteDatabase { get; set; }
            public string Host { get; set; }
            public int Port { get; set; }
            public string Database { get; set; }
            public string User { get; set; }
            public string Password { get; set; }
            public string WebhookUrl { get; set; }
            public int WebhookInterval { get; set; } // Interval in minutes
            public string TableName { get; set; }
            public string LBTableName { get; set; }
        }

        private class PlayerData
        {
            public int PVPKills { get; set; }
            public float PVPDistance { get; set; }
            public int PVEKills { get; set; }
            public int SleepersKilled { get; set; }
            public int HeadShots { get; set; }
            public int Deaths { get; set; }
            public int Suicides { get; set; }
            public float KDR { get; set; }
            public int HeliKills { get; set; }
            public int APCKills { get; set; }
            public int RocketsLaunched { get; set; }
            public double TimePlayed { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(new Configuration
            {
                UseRemoteDatabase = false,
                Host = "localhost",
                Port = 3306,
                Database = "rust",
                User = "root",
                Password = "password",
                WebhookUrl = "",
                WebhookInterval = 720, // Default to 12 hours
                TableName = "player_stats",
                LBTableName = "leaderboard"
            }, true);
        }

        private void Init()
        {
            config = Config.ReadObject<Configuration>();
            if (config == null)
            {
                LoadDefaultConfig();
                config = Config.ReadObject<Configuration>();
                SaveConfig();
            }

            LoadData();
            permission.RegisterPermission("stattracker.admin", this);

            if (!string.IsNullOrEmpty(config.WebhookUrl))
            {
                webhookTimer = timer.Every(config.WebhookInterval * 60, () => SendTopKillsToDiscord()); // Convert minutes to seconds
            }
        }

        private void Unload()
        {
            SaveData();
            webhookTimer?.Destroy();
        }

        private void OnServerSave()
        {
            SaveData();
        }

        private void LoadData()
        {
            if (config.UseRemoteDatabase)
            {
                playerStats = LoadDataFromRemoteDatabase();
            }
            else
            {
                playerStats = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, PlayerData>>(Name) ?? new Dictionary<string, PlayerData>();
            }
        }

        private void SaveData()
        {
            if (config.UseRemoteDatabase)
            {
                SaveDataToRemoteDatabase();
            }
            else
            {
                Interface.Oxide.DataFileSystem.WriteObject(Name, playerStats);
            }
        }

        private Dictionary<string, PlayerData> LoadDataFromRemoteDatabase()
        {
            var data = new Dictionary<string, PlayerData>();
            string connectionString = $"Server={config.Host};Port={config.Port};Database={config.Database};User={config.User};Password={config.Password};";
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                string query = $"SELECT * FROM {config.TableName}";
                using (var cmd = new MySqlCommand(query, connection))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var playerId = reader.GetString("PlayerId");
                            data[playerId] = new PlayerData
                            {
                                PVPKills = reader.GetInt32("PVPKills"),
                                PVPDistance = reader.GetFloat("PVPDistance"),
                                PVEKills = reader.GetInt32("PVEKills"),
                                SleepersKilled = reader.GetInt32("SleepersKilled"),
                                HeadShots = reader.GetInt32("HeadShots"),
                                Deaths = reader.GetInt32("Deaths"),
                                Suicides = reader.GetInt32("Suicides"),
                                KDR = reader.GetFloat("KDR"),
                                HeliKills = reader.GetInt32("HeliKills"),
                                APCKills = reader.GetInt32("APCKills"),
                                RocketsLaunched = reader.GetInt32("RocketsLaunched"),
                                TimePlayed = reader.GetDouble("TimePlayed")
                            };
                        }
                    }
                }
            }
            return data;
        }

        private void SaveDataToRemoteDatabase()
        {
            string connectionString = $"Server={config.Host};Port={config.Port};Database={config.Database};User={config.User};Password={config.Password};";
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                foreach (var entry in playerStats)
                {
                    var playerId = entry.Key;
                    var data = entry.Value;
                    string query = $"REPLACE INTO {config.TableName} (PlayerId, PVPKills, PVPDistance, PVEKills, SleepersKilled, HeadShots, Deaths, Suicides, KDR, HeliKills, APCKills, RocketsLaunched, TimePlayed) " +
                                   $"VALUES ('{playerId}', {data.PVPKills}, {data.PVPDistance}, {data.PVEKills}, {data.SleepersKilled}, {data.HeadShots}, {data.Deaths}, {data.Suicides}, {data.KDR}, {data.HeliKills}, {data.APCKills}, {data.RocketsLaunched}, {data.TimePlayed})";
                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        private void OnPlayerInit(IPlayer player)
        {
            if (!playerStats.ContainsKey(player.Id))
            {
                playerStats[player.Id] = new PlayerData();
            }
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null) return;

            var data = GetPlayerData(player.UserIDString);

            if (info.Initiator != null && info.Initiator.ToPlayer() != null)
            {
                var killer = info.Initiator.ToPlayer();
                if (killer != player)
                {
                    data.PVPKills++;
                    data.PVPDistance += Vector3.Distance(player.transform.position, killer.transform.position);
                }
            }

            if (info.Initiator != null && info.Initiator is BaseHelicopter)
            {
                data.HeliKills++;
            }

            if (info.Initiator != null && info.Initiator is BradleyAPC)
            {
                data.APCKills++;
            }

            if (player.IsSleeping())
            {
                data.SleepersKilled++;
            }

            if (info.isHeadshot)
            {
                data.HeadShots++;
            }

            data.Deaths++;
            data.KDR = data.Deaths > 0 ? (float)data.PVPKills / data.Deaths : data.PVPKills;
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (player == null) return;

            var data = GetPlayerData(player.UserIDString);
            data.TimePlayed += player.lifeStory.secondsAlive;
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return;

            if (entity is BasePlayer player)
            {
                if (info.Initiator != null && info.Initiator.ToPlayer() != null)
                {
                    var killer = info.Initiator.ToPlayer();
                    var data = GetPlayerData(killer.UserIDString);
                    data.PVEKills++;
                }
            }
        }

        private void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {
            if (player == null) return;

            var data = GetPlayerData(player.UserIDString);
            data.RocketsLaunched++;
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null) return;

            if (player.lastDamage == DamageType.Suicide)
            {
                var data = GetPlayerData(player.UserIDString);
                data.Suicides++;
            }
        }

        private PlayerData GetPlayerData(string playerId)
        {
            if (!playerStats.TryGetValue(playerId, out var data))
            {
                data = new PlayerData();
                playerStats[playerId] = data;
            }
            return data;
        }

        [Command("stats")]
        private void StatsCommand(IPlayer player, string command, string[] args)
        {
            if (!playerStats.ContainsKey(player.Id))
            {
                player.Reply("No stats available for you yet.");
                return;
            }

            var data = GetPlayerData(player.Id);
            player.Reply($"Stats for {player.Name}:\n" +
                         $"PVPKills: {data.PVPKills}\n" +
                         $"PVPDistance: {data.PVPDistance}\n" +
                         $"PVEKills: {data.PVEKills}\n" +
                         $"SleepersKilled: {data.SleepersKilled}\n" +
                         $"HeadShots: {data.HeadShots}\n" +
                         $"Deaths: {data.Deaths}\n" +
                         $"Suicides: {data.Suicides}\n" +
                         $"KDR: {data.KDR}\n" +
                         $"HeliKills: {data.HeliKills}\n" +
                         $"APCKills: {data.APCKills}\n" +
                         $"RocketsLaunched: {data.RocketsLaunched}\n" +
                         $"TimePlayed: {data.TimePlayed} seconds");
        }

        [Command("lookup")]
        private void LookupCommand(IPlayer player, string command, string[] args)
        {
            if (args.Length != 1)
            {
                player.Reply("Usage: /lookup <playerName|steamID>");
                return;
            }

            var targetId = args[0];
            IPlayer targetPlayer = covalence.Players.FindPlayer(targetId);

            if (targetPlayer == null)
            {
                player.Reply($"Player '{targetId}' not found.");
                return;
            }

            var data = GetPlayerData(targetPlayer.Id);
            player.Reply($"Stats for {targetPlayer.Name} ({targetPlayer.Id}):\n" +
                         $"PVPKills: {data.PVPKills}\n" +
                         $"PVPDistance: {data.PVPDistance}\n" +
                         $"PVEKills: {data.PVEKills}\n" +
                         $"SleepersKilled: {data.SleepersKilled}\n" +
                         $"HeadShots: {data.HeadShots}\n" +
                         $"Deaths: {data.Deaths}\n" +
                         $"Suicides: {data.Suicides}\n" +
                         $"KDR: {data.KDR}\n" +
                         $"HeliKills: {data.HeliKills}\n" +
                         $"APCKills: {data.APCKills}\n" +
                         $"RocketsLaunched: {data.RocketsLaunched}\n" +
                         $"TimePlayed: {data.TimePlayed} seconds");
        }

        [Command("resetstats")]
        private void ResetStatsCommand(IPlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.Id, "stattracker.admin"))
            {
                player.Reply("You do not have permission to use this command.");
                return;
            }

            if (args.Length != 1)
            {
                player.Reply("Usage: /resetstats <steam64ID>");
                return;
            }

            string targetId = args[0];
            if (!playerStats.ContainsKey(targetId))
            {
                player.Reply($"No stats found for player with ID {targetId}.");
                return;
            }

            playerStats.Remove(targetId);
            if (config.UseRemoteDatabase)
            {
                DeleteStatsFromRemoteDatabase(targetId);
            }
            SaveData();
            player.Reply($"Stats for player with ID {targetId} have been reset.");
        }

        [Command("pushstats")]
        private void PushStatsCommand(IPlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.Id, "stattracker.admin"))
            {
                player.Reply("You do not have permission to use this command.");
                return;
            }

            SendTopKillsToDiscord();
            player.Reply("Top kills stats have been pushed to the Discord webhook.");
        }

        private void DeleteStatsFromRemoteDatabase(string playerId)
        {
            string connectionString = $"Server={config.Host};Port={config.Port};Database={config.Database};User={config.User};Password={config.Password};";
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                string query = $"DELETE FROM {config.TableName} WHERE PlayerId = '{playerId}'";
                using (var cmd = new MySqlCommand(query, connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void SendTopKillsToDiscord()
        {
            var topPlayers = playerStats.OrderByDescending(p => p.Value.PVPKills).Take(5);
            var message = new StringBuilder();
            int rank = 1;
            foreach (var player in topPlayers)
            {
                var steamId = player.Key;
                var playerName = covalence.Players.FindPlayerById(player.Key)?.Name ?? "Unknown";
                message.AppendLine($"{rank}. {playerName} ({steamId}) - {player.Value.PVPKills} kills");
                rank++;
            }
            PostToDiscord(message.ToString());
        }

        private void PostToDiscord(string message)
        {
            if (string.IsNullOrEmpty(config.WebhookUrl)) return;

            using (var webClient = new WebClient())
            {
                var embed = new
                {
                    author = new
                    {
                        name = "Stat Tracker"
                    },
                    description = message,
                    footer = new
                    {
                        text = "developed by herbs.acab"
                    }
                };

                var payload = new
                {
                    embeds = new[] { embed }
                };

                var jsonPayload = JsonConvert.SerializeObject(payload);
                webClient.Headers[HttpRequestHeader.ContentType] = "application/json";
                webClient.UploadString(config.WebhookUrl, jsonPayload);
            }
        }
    }
}
