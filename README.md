# StatTracker

StatTracker is a Rust plugin that tracks various player statistics and stores them in a local or remote database. This allows server admins and players to monitor their performance over time. The plugin also posts the players stats to discord via a webhook.

## Author

- Plugin developed by **locks**

## Version

- 1.1.8

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
  "UseRemoteDatabase": false,
  "Host": "localhost",
  "Port": 3306,
  "Database": "rust",
  "User": "root",
  "Password": "password",
  "WebhookUrl": ""
}
```


