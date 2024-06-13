# StatTracker

StatTracker is a Rust plugin that tracks various player statistics and stores them in a local or remote database. This allows server admins and players to monitor their performance over time.

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

## Permissions

The following permission is used by StatTracker:

`stattracker.admin` - Required to use the /resetstats command.

## Configuration

The plugin can be configured to use either a local file or a remote MySQL database to store player statistics. The default configuration uses a local file.

Configuration Options

UseRemoteDatabase: `True or False`

Host: `The MySQL server host.`

Port: `The MySQL server port.`

Database: `The MySQL database name.`

User: `The MySQL database user.`

Password: `The MySQL database password.`

## Default Configuration
```json
{
  "UseRemoteDatabase": false,
  "Host": "localhost",
  "Port": 3306,
  "Database": "rust",
  "User": "root",
  "Password": "password"
}
```


