> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Jukebox_tracks)

# Modding:Jukebox tracks (`Jukebox_tracks`)

← Index

This page describes how the game stores and parses jukebox tracks. This is an advanced guide for mod developers.

## Format
Jukebox audio tracks can be changed by editing the `Data/JukeboxTracks` data asset.

This consists of a string → model lookup, where...
- The key is the audio cue ID to play (case-sensitive).
- The value is a model with the fields listed below.

If the player has heard a music track not listed in `Data/JukeboxTracks`, it's automatically available with the title set to the cue name. To disable a track, add an entry with `"Available": false`.

| field | effect |
| --- | --- |
| `Name` | *(Optional)* A tokenizable string for the music track's in-game title. Defaults to the ID. |
| `Available` | *(Optional)* Whether the track should be shown in the jukebox menu. This can be `true` (always shown), `false` (never shown), or `null` (show if the player has heard it before). Default `null`. Tracks with `"Available": true` are listed first in the jukebox menu. |
| `AlternativeTrackIds` | *(Optional)* A list of other cue names for this audio track (not case-sensitive). If the player has heard any of these track IDs, this entry is available in the menu. Default none. For example, this can be used when renaming a track to keep it unlocked for existing players: 
```json
"{{ModId}}_TrackName": {
    "Name": "{{i18n: track-name}}",
    "AlternativeTrackIds": [ "OldTrackName" ]
}
```
 |

ru:Модификации:Музыкальный автомат
zh:模组:点唱机音乐
