using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using MySql.Data.MySqlClient;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

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
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration
            {
                UseRemoteDatabase = false,
                Host = "localhost",
                Port = 3306,
                Database = "rust",
                User = "root",
                Password = "password",
                WebhookUrl = ""
            };
            SaveConfig();
        }

        private void Init()
        {
            config = Config.ReadObject<Configuration>();
            LoadData();
            permission.RegisterPermission("stattracker.admin", this);

            if (!string.IsNullOrEmpty(config.WebhookUrl))
            {
                webhookTimer = timer.Every(43200, () => SendTopKillsToDiscord()); // 43200 seconds = 12 hours
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
                string query = "SELECT * FROM player_stats";
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
                    string query = $"REPLACE INTO player_stats (PlayerId, PVPKills, PVPDistance, PVEKills, SleepersKilled, HeadShots, Deaths, Suicides, KDR, HeliKills, APCKills, RocketsLaunched, TimePlayed) " +
                                   $"VALUES ('{playerId}', {data.PVPKills}, {data.PVPDistance}, {data.PVEKills}, {data.SleepersKilled}, {data.HeadShots}, {data.Deaths}, {data.Suicides}, {data.KDR}, {data.HeliKills}, {data.APCKills}, {data.RocketsLaunched}, {data.TimePlayed})";
                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
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

        private void OnPlayerInit(IPlayer player)
        {
            if (!playerStats.ContainsKey(player.Id))
            {
                playerStats[player.Id] = new PlayerData();
            }
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            var data = GetPlayerData(player);

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
            var data = GetPlayerData(player);
            data.TimePlayed += player.lifeStory.secondsAlive;
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BasePlayer player)
            {
                if (info.Initiator != null && info.Initiator.ToPlayer() != null)
                {
                    var killer = info.Initiator.ToPlayer();
                    var data = GetPlayerData(killer);
                    data.PVEKills++;
                }
            }
        }

        private void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {
            var data = GetPlayerData(player);
            data.RocketsLaunched++;
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (player.lastDeathReason == BaseNetworkable.DeathReason.Suicide)
            {
                var data = GetPlayerData(player);
                data.Suicides++;
            }
        }

        private PlayerData GetPlayerData(IPlayer player)
        {
            return playerStats[player.Id];
        }

        [Command("stats")]
        private void StatsCommand(IPlayer player, string command, string[] args)
        {
            if (!playerStats.ContainsKey(player.Id))
            {
                player.Reply("No stats available for you yet.");
                return;
            }

            var data = GetPlayerData(player);
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

        [Command("resetstats", "stattracker.admin")]
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

        private void DeleteStatsFromRemoteDatabase(string playerId)
        {
            string connectionString = $"Server={config.Host};Port={config.Port};Database={config.Database};User={config.User};Password={config.Password};";
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                string query = $"DELETE FROM player_stats WHERE PlayerId = '{playerId}'";
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
            message.AppendLine("Top 5 Players by Kills:");
            int rank = 1;
            foreach (var player in topPlayers)
            {
                var playerName = covalence.Players.FindPlayerById(player.Key)?.Name ?? player.Key;
                message.AppendLine($"{rank}. {playerName} - {player.Value.PVPKills} kills");
                rank++;
            }
            PostToDiscord(message.ToString());
        }

        private async void PostToDiscord(string message)
        {
            if (string.IsNullOrEmpty(config.WebhookUrl)) return;

            using (var httpClient = new HttpClient())
            {
                var payload = new
                {
                    content = message
                };

                var jsonPayload = JsonConvert.SerializeObject(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                await httpClient.PostAsync(config.WebhookUrl, content);
            }
        }
    }
}
