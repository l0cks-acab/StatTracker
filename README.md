# StatTracker

StatTracker is a Rust plugin that tracks various player statistics and stores them in a local or remote database. This allows server admins and players to monitor their performance over time. The plugin also posts the players stats to discord via a webhook.

## Author

- Plugin developed by **herbs.acab**

## Version

- 1.1.9

## Description

StatTracker tracks and stores the following player statistics:
- PVPKills
- PVPDistance
- PVEKills
- SleepersKilled
- HeadShots
- Deaths
- Suicides
- KDR (Kill/Death Ratio)
- HeliKills
- APCKills
- RocketsLaunched
- TimePlayed

## Commands

`/stats` - Displays the player's statistics.

`/resetstats <steam64ID>` - Allows admins to delete a player's statistics using their Steam64ID.

`/pushstats` - Allows admins to forcefully push the stats to the webhook.

## Permissions

The following permission is used by StatTracker:

`stattracker.admin` - Required to use the /resetstats & /pushstats commands.

## Default Configuration
```json
{
  "Database": "rust",
  "Host": "localhost",
  "LBTableName": "leaderboard",
  "Password": "password",
  "Port": 3306,
  "TableName": "player_stats",
  "User": "root",
  "UseRemoteDatabase": false,
  "WebhookInterval": 720,
  "WebhookUrl": "https://discord.com/api/webhooks/.................."
}
```


