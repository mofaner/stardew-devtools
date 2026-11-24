> **文档来源**: Stardew Valley Official Wiki (https://stardewvalleywiki.com/Modding:Audio)

# Modding:Audio (`Audio`)

← Index

This page explains how to use and edit the game's music tracks and sound effects (called *cues*). This is an advanced guide for modders.

## Overview
'Audio' covers both sound effects (like the *boop* sound when clicking a button) and music tracks. An audio track or sound effect is called a *cue*, which may have multiple audio files to randomize which one plays each time the cue starts.

The game loads its initial cues from the `Content/XACT` folder (which can't be edited directly), and then adds/edits cues based on the audio data (which can be edited through Content Patcher or SMAPI).

Sound effects are played by audio cue ID, while music is selected using this priority list:
# specific audio cue IDs in some cases (e.g. `MainTheme` on the title screen);
# music data in `Data/Locations`;
# the `Music` map property (deprecated);
# music data in `Data/LocationContexts`.

## Audio data
You can add or edit cues by editing the `Data/AudioChanges` asset. New cues are added to the game's soundbank, so they can be used anywhere normal audio can be used (e.g. the `Music` map property).

### Data format
The `Data/AudioChanges` asset consists of a string → model lookup, where the key matches the `ID`, and the value is a model with the fields below.

Entries in this asset describe an **override** applied to the soundbank. The override is applied permanently for the current game session, even if the asset is edited to remove it. Overriding a cue will reset all values to the ones specified.

| field | effect |
| --- | --- |
| `ID` | The unique string ID for the audio cue, used when playing the sound in-game. |
| `FilePaths` | A list of absolute file paths (not asset names) from which to load the audio. Each file can be `.ogg` or `.wav`. If you list multiple paths, a random one will be chosen each time it's played. |
| `Category` | The audio category, which determines which volume slider in the game options applies. This should be one of `Default`, `Music`, `Sound`, `Ambient`, or `Footsteps` (see a description of each category). Defaults to `Default`. |
| `StreamedVorbis` | Whether the audio should be streamed from disk when it's played, instead of being loaded into memory ahead of time. This is only possible for Ogg Vorbis (`.ogg`) files, which otherwise will be decompressed in-memory on load. Default false. This is a tradeoff between memory usage and performance, so you should consider which value is best for each audio cue: |
| value | effect |
| `true` | Reduces memory usage when the audio cue isn't active, but increases performance impact when it's played. Playing the audio multiple times will multiply the memory and performance impact while they're active, since each play will stream a new instance. Recommended for longer audio cues (like music or ambient noise), or cues that are rarely used in a specific scenario (e.g. a sound that only plays once in an event). |
| `false` | Increases memory usage (since it's fully loaded into memory), but reduces performance impact when it's played. It can be played any number of times without affecting memory or performance (it'll just play the cached audio). Recommended for sound effects, or short audio cues that are played occasionally. |

|-
| `Looped`
| Whether the audio cue loops continuously until stopped. Default false.
|-
| `UseReverb`
| Whether to apply a reverb effect to the audio. Default false.
|-
| `CustomFields`
| The custom fields for this entry.
|}

### Category list
Each audio cue is assigned to one of five categories, which affects which volume slider in the game options applies to it:

| internal ID | name | description |
| --- | --- | --- |
| 1 | `Default` | This is an unused category, and should generally be avoided. |
| 2 | `Music` | For music tracks, to be regulated with the in-game music volume option. |
| 3 | `Sound` | For sound effects, to be regulated with the in-game sound volume option. |
| 4 | `Ambient` | For ambient background sounds, like wind, rain or machine whirring, that can play in the background of a scene. |
| 5 | `Footsteps` | For step sounds, such as player or horse footsteps. |

### Audio file format
The game only supports `.ogg` and `.wav` formats. If you have `.mp3` files you'd like to use in-game, you can convert the audio file to `.wav` or `.ogg` with audio software such as [Audacity](https://www.audacityteam.org/). For smaller-sized sounds such as sound effects, you can use a `.wav` file, but for audio such as music that has a large file size, use a compressed `.ogg` file.

Make sure the file size of your music is relatively small (preferably around 2-4MB). The bigger the file, the longer the game will freeze for when loading your newly added song. To reduce the size of your song, you can convert it to `.ogg` and add compression to the sound file. The more compression your audio has, the smaller the file will be, but the quality will diminish noticeably if you compress the audio too much.

If you're patching many sounds at once, keep the file size of your mod in mind, because the more sounds you include, the longer it will take for people to download your mod.

If your audio is in mono and not stereo, it may sound quieter or muffled compared to other audio. You should convert your audio to stereo if you want to avoid this.

### Example
This content pack adds a new music cue to the game, and plays it when the player enters the bus stop:

    "Changes": [
        // add music cue
        {
            "Action": "EditData",
            "Target": "Data/AudioChanges",
            "Entries": {
                "{{ModId}}_Music": {
                    "ID": "{{ModId}}_Music",
                    "Category": "Music",
                    "FilePaths": [ "{{AbsoluteFilePath: assets/music.ogg}}" ],
                    "StreamedVorbis": true,
                    "Looped": true
                }
            }
        },

        // add to bus stop
        {
            "Action": "EditData",
            "Target": "Data/Locations",
            "Fields": {
                "BusStop": {
                    "MusicDefault": "{{ModId}}_Music"
                }
            }
        }
    ]

## Review audio
### Unpack audio files
The base game's audio is stored in the `Content/XACT` folder in the form of soundbanks, which store all of the game's sounds and music. The two soundbank files available are `Wave Bank(1.4).xwb` and `Wave Bank.xwb`.

Audio files can be extracted and browsed with an XWB extractor:

| tool name | platforms supported | notes |
| --- | --- | --- |
| mariodon/unxwb unxwb | Windows, macOS | Extracts an XWB soundbank into .wav files and hex codes. A straightforward open and extract utility. **use unwxnb on macOS (Sample)**: If you want to use unxwb on a Mac, you'll need to build it yourself. # Download the source code to unxwb from mariodon/unxwb unxwb (click the green Code button > select download zip) # Download [this make file from Github](https://gist.github.com/ssokolow/11149295) (download zip button in top right) # Unzip both if needed # Move the make file into the source code folder # Open up Terminal and type `cd Downloads/unxwb-master` and hit return if you just left everything in Downloads, or otherwise use `cd` to move into the folder with the source code # Type `make` into Terminal and hit return. If this gives you errors, try typing in `xcode-select --install` and hit return, which installs developer tools, and then try `make` again # You should now have `unxwb` as a Unix executable file in that folder # Copy `Wave Bank.xwb` from your game content folder into the folder # Type `./unxwb Wave\ Bank.xwb` and hit return to extract the .wavs from the .xwb # Move the .wav files into a separate folder (I named mine `Wave Bank`) # Copy `Wave Bank(1.4).xwb` from your game content folder into the folder # Rename `Wave Bank(1.4).xwb` to `WaveBank14.xwb` # Type `./unxwb WaveBank14.xwb` and hit return to extract the .wavs from the .xwb # Move the .wav files into a separate folder (I named mine `Wave Bank 1.4`) # Now you have all the .wavs into two folders! |
| [VGMStream](https://www.vgmstream.org/downloads) plugin for [foobar2000](https://www.foobar2000.org/) sound player | Windows | Views XWB soundbanks with integer codes and many in-game codes included in filenames, and has a very convenient file browser. (For some reason, it increments audio IDs by 1, and each audio cue is looped.) |
| [XACTTool](https://shrinefox.com/browse?post=xacttool) | Windows | Extracts an XWB soundbank into .wav files and integer codes. Can also manipulate soundbanks. Command-line only. |

See the full track list below.

### Identify audio in-game
- For music tracks, you can install the 19441 Now Playing mod (see the mod description for info on adding the music ID to the message). A HUD message will be shown in-game when music starts.
- For sound effects, you can enter `debug logSounds` in the SMAPI console window. This will log info for each sound played in-game, including positional data if applicable. See console commands for more info.

## Track list
These are the raw soundbank IDs for music and sounds exported from the game data (see the talk page for the export code).

A few notes about the table columns:
- The name is what you'd use in-game (*e.g.,* with the `Music` map property or the `Game1.changeMusicTrack` method). When a name is repeated with different soundbank IDs, the game will choose a random sound each time you play it.
- The wavebank indicates whether the audio is from `Content/XACT/Wave Bank.xwb` or `Content/XACT/Wave Bank(1.4).xwb`. Each wavebank has its own set of soundbank IDs, but names don't overlap.
- The soundtrack index is the sound's position in the soundbank. The hexadecimal version matches the track's filename if you unpack the wavebank using unxwb.
- The description column is filled in manually for the wiki.

See also an [older spreadsheet with more comprehensive descriptions](https://docs.google.com/spreadsheets/d/1CpDrw23peQiq-C7F2FjYOMePaYe0Rc9BwQsj3h6sjyo). (Don't copy text from that spreadsheet into this page, since it's not licensed!)

### Footsteps
| cue ID | wavebank | soundbank index | description |  |
| --- | --- | --- | --- | --- |
| dec | hex |  |  |  |
| `Cowboy_Footstep` | `Wavebank` | `269` | data-sort-value="269"\| `0000010d` | Mainly used as the footstep sound for pets, in Prairie King, and as a hover sound in various menus (including the title screen buttons). |
| `grassyStep` | `Wavebank` | `22` | data-sort-value="22"\| `00000016` | Mainly used as the player footstep sound on grass tiles, when adding hay to a silo, when changing a player's hat or hairstyle, and for rustling bushes in Lewis and Marnie's six-heart event. |
| `jingleBell` | `Wavebank` | `433` | data-sort-value="433"\| `000001b1` | A sleigh bell jingling once. Mainly used as the player's footstep sound when they wear Cinderclown Shoes. |
| `434` | data-sort-value="434"\| `000001b2` |  |  |  |
| `435` | data-sort-value="435"\| `000001b3` |  |  |  |
| `sandyStep` | `Wavebank` | `16` | data-sort-value="16"\| `00000010` | Mainly used as the player footstep sound on dirt tiles. Also plays when new clothes are put on the farmer. |
| `snowyStep` | `Wavebank` | `340` | data-sort-value="340"\| `00000154` | Mainly used as the player footstep sound on grass tiles in winter. |
| `stoneStep` | `Wavebank` | `23` | data-sort-value="23"\| `00000017` | Mainly used as the player footstep sound on stone tiles. Also plays when the player selects an inventory slot or places an item in their inventory. |
| `thudStep` | `Wavebank` | `24` | data-sort-value="24"\| `00000018` | Mainly used as a fallback player footstep sound when other sounds do not apply, particularly in indoors locations. |
| `woodyStep` | `Wavebank` | `29` | data-sort-value="29"\| `0000001d` | Mainly used as the player footstep sound on wood tiles. |

### Music
| cue ID | wavebank | soundbank index | title in jukebox and soundtrack | description |  |
| --- | --- | --- | --- | --- | --- |
| dec | hex |  |  |  |  |
| `50s` | `Wavebank` | `45` | data-sort-value="45"\| `0000002d` | *Pleasant Memory (Penny's Theme)* | A peaceful clarinet and marimba track in 3. |
| `AbigailFlute` | `Wavebank` | `279` | data-sort-value="279"\| `00000117` | *A Stillness in the Rain Solo* (jukebox);; *A Stillness in the Rain (Abigail's Melody)* (soundtrack) | A solo flute melody. |
| `AbigailFluteDuet` | `Wavebank` | `280` | data-sort-value="280"\| `00000118` | *A Stillness in the Rain Duet* (jukebox);; *A Stillness in the Rain* (soundtrack) | The same melody as in `AbigailFlute`, but accompanied by a guitar. |
| `aerobics` | `Wavebank` | `55` | data-sort-value="55"\| `00000037` | *Aerobics Class* (jukebox only) | Dance aerobics music that abruptly cuts off. Used in Harvey's 6-heart event. |
| `archaeo` | `Wavebank` | `0` | data-sort-value="0"\| `00000000` |  | An upbeat banjo tune with heavy bass and a higher-pitched electric melody. Appears unused. |
| `bigDrums` | `Wavebank` | `172` | data-sort-value="172"\| `000000ac` |  | Mysterious tambourine, bass drum, and bongos. Used in Sebastian's 6-heart event. |
| `breezy` | `Wavebank` | `281` | data-sort-value="281"\| `00000119` | *Land of Green and Gold* (jukebox);; *Land of Green and Gold (Leah's Theme)* (soundtrack) | A happy marimba and harmonica tune in 3. |
| `caldera` | `Wavebank` | `383` | data-sort-value="383"\| `0000017f` | *Mystery of the Caldera* | A mysterious synth track backed by lava bubbling. |
| `Cavern` | `Wavebank` | `65` | data-sort-value="65"\| `00000041` | *Mines (a Flicker in the Deep)* | A soft electric piano track. Plays in The Mines in levels 1-39. |
| `christmasTheme` | `Wavebank` | `305` | data-sort-value="305"\| `00000131` | *Winter Festival* | A joyful holiday tune with jingling bells. Background music for the Feast of the Winter Star. |
| `Cloth` | `Wavebank` | `67` | data-sort-value="67"\| `00000043` | *Mines (Cloth)* | Wistful piano chords with synths in a minor key. Plays in The Mines in levels 40-79. |
| `CloudCountry` | `Wavebank` | `190` | data-sort-value="190"\| `000000be` | *Cloud Country* | A happy banjo tune with flute. Used in the character creation menus. |
| `clubloop` | `Wavebank` | `102` | data-sort-value="102"\| `00000066` |  | Quiet ambient hum interrupted by the occasional sci-fi esque sound. Background ambiance for the Casino and Qi's Walnut Room. |
| `cowboy_boss` | `Wavebank` | `267` | data-sort-value="267"\| `0000010b` | *Journey Of The Prairie King (Boss)* (jukebox);; *Journey of the Prairie King (Final Boss & Ending)* (soundtrack) | A fast and bass-heavy 8-bit boss fight soundtrack. Used as background music when the player fights Fector in Journey of the Prairie King. |
| `cowboy_outlawsong` | `Wavebank` | `277` | data-sort-value="277"\| `00000115` | *Journey Of The Prairie King (Outlaw)* (jukebox);; *Journey of the Prairie King (The Outlaw)* (soundtrack) | An 8-bit "wild west"-themed track with whistling. Plays when the Cowboy boss spawns in Journey of the Prairie King. |
| `Cowboy_OVERWORLD` | `Wavebank` | `261` | data-sort-value="261"\| `00000105` | *Journey of the Prairie King (Overworld)* | A medium-tempo 8-bit game theme. Used as the default theme for Journey of the Prairie King. |
| `Cowboy_singing` | `Wavebank` | `262` | data-sort-value="262"\| `00000106` | *Journey Of The Prairie King (Ending)* (jukebox);; *Journey of the Prairie King (Final Boss & Ending)* (soundtrack) | A slower 8-bit track with a higher-pitched melody on top of the bass. Plays when the player completes Journey of the Prairie King. |
| `Cowboy_undead` | `Wavebank` | `270` | data-sort-value="270"\| `0000010e` | *Journey Of The Prairie King (Undead)* (jukebox only) | A short and fast 8-bit game track in a minor key. Plays when the player picks up a tombstone power-up in Journey of the Prairie King. |
| `crane_game` | `Wavebank(1.4)` | `12` | data-sort-value="12"\| `0000000c` | *Crane Game* | A slow jazz string bass solo. Used in the Crane Game. |
| `crane_game_fast` | `Wavebank(1.4)` | `13` | data-sort-value="13"\| `0000000d` | *Crane Game (It's A Catch!)* (jukebox);; *Crane Game* (soundtrack) | An upbeat version of `crane_game` that includes piano. Used in the Crane Game. |
| `Crystal Bells` | `Wavebank` | `64` | data-sort-value="64"\| `00000040` | *Mines (Crystal Bells)* | A slow piano melody with electric bass and synths. Plays in The Mines in levels 1-39. |
| `Cyclops` | `Wavebank` | `124` | data-sort-value="124"\| `0000007c` |  | Seems to match `winter2`. |
| `desolate` | `Wavebank` | `40` | data-sort-value="40"\| `00000028` | *A Sad Story* (jukebox);; *A Sad Song (Alex's Theme)* (soundtrack) | A sad mallet percussion tune. |
| `distantBanjo` | `Wavebank` | `347` | data-sort-value="347"\| `0000015b` | *Distant Banjo* | A happy harmonica and banjo tune. |
| `EarthMine` | `Wavebank` | `64` | data-sort-value="64"\| `00000040` |  | Plays one of either `Crystal Bells`, `Cavern`, or `Secret Gnomes`. |
| `65` | data-sort-value="65"\| `00000041` |  |  |  |  |
| `66` | data-sort-value="66"\| `00000042` |  |  |  |  |
| `echos` | `Wavebank` | `49` | data-sort-value="49"\| `00000031` | *Echos* (jukebox);; *Echos (Sebastian's Theme)* (soundtrack) | A pensive and slow xylophone solo. |
| `elliottPiano` | `Wavebank` | `295` | data-sort-value="295"\| `00000127` | *Piano Solo* (jukebox);; *Piano Solo (Elliott's Theme)* (soundtrack) | A slow, short, and simple piano solo. |
| `EmilyDance` | `Wavebank` | `357` | data-sort-value="357"\| `00000165` | *Emily's Dance* | A cassette tape being put into a player and then upbeat dance music with a strong beat and flute/piano melody. Used in Emily's 6-heart event. |
| `EmilyDream` | `Wavebank` | `358` | data-sort-value="358"\| `00000166` | *Dreamscape* | Ambiance of the wind blowing, a stringed instrument playing, and "ooo"s. Includes lots of pitch bending. Used in Emily's 2-heart event. |
| `EmilyTheme` | `Wavebank` | `359` | data-sort-value="359"\| `00000167` | *Song Of Feathers* (jukebox);; *Song of Feathers (Emily's Theme)* (soundtrack) | A medium-tempo happy synth track with a xylophone feature. Used in several of Emily's heart events. |
| `end_credits` | `Wavebank` | `403` | data-sort-value="403"\| `00000193` | *Stardew Valley Forever* (jukebox);; *Summit Celebration* (soundtrack) | A happy track with bass and synth melodies. Plays during the Summit event. |
| `event1` | `Wavebank` | `299` | data-sort-value="299"\| `0000012b` | *Fun Festival* | An upbeat banjo, piano, and bass track. Plays in several cutscenes, such as the judging at the Egg Festival. |
| `event2` | `Wavebank` | `302` | data-sort-value="302"\| `0000012e` | *Luau Festival* | An upbeat yet mysterious xylophone and flute melody. Used during the Luau. |
| `fall1` | `Wavebank` | `121` | data-sort-value="121"\| `00000079` | *Fall (The Smell of Mushroom)* | A soft trumpet, clarinet, harp, and xylophone track. Plays in various outdoor locations in fall. |
| `fall2` | `Wavebank` | `119` | data-sort-value="119"\| `00000077` | *Fall (Ghost Synth)* | A soft synth and bass track with light drums. Plays in various outdoor locations in fall. |
| `fall3` | `Wavebank` | `120` | data-sort-value="120"\| `00000078` | *Fall (Raven's Descent)* | A soft flute and xylophone track. Plays in various outdoor locations in fall. |
| `fallFest` | `Wavebank` | `304` | data-sort-value="304"\| `00000130` | *Stardew Valley Fair Theme* | A medium-tempo banjo and clarinet duet. Used in various festivals, including the Stardew Valley Fair. |
| `fieldofficeTentMusic` | `Wavebank` | `375` | data-sort-value="375"\| `00000177` | *Professor Snail's Radio* | A laid-back electric guitar track with light percussion and some xylophone fills. Plays in the Island Field Office. |
| `FlowerDance` | `Wavebank` | `301` | data-sort-value="301"\| `0000012d` | *Flower Dance* | A waltz featuring lots of string pizzicato, flute, and bells. Plays during the dance scene in the Flower Dance. |
| `FrogCave` | `Wavebank` | `387` | data-sort-value="387"\| `00000183` | *The Gourmand's Cave* | A medium-tempo percussive track with drums, clave, and the occasional frog croak. Plays in Gourmand's Cave. |
| `FrostMine` | `Wavebank` | `67` | data-sort-value="67"\| `00000043` |  | Plays one of either `Cloth`, `Icicles`, or `XOR`. |
| `68` | data-sort-value="68"\| `00000044` |  |  |  |  |
| `69` | data-sort-value="69"\| `00000045` |  |  |  |  |
| `Ghost Synth` | `Wavebank` | `119` | data-sort-value="119"\| `00000077` |  | Alias for `fall2` that is unused by Vanilla. |
| `grandpas_theme` | `Wavebank` | `336` | data-sort-value="336"\| `00000150` | *Grandpa's Theme* | A soft music box lullaby. Plays in the intro cutscene. |
| `gusviolin` | `Wavebank` | `297` | data-sort-value="297"\| `00000129` | *Violin Solo* | A short, sentimental violin solo. Plays in Alex's 10-heart event. |
| `harveys_theme_jazz` | `Wavebank(1.4)` | `4` | data-sort-value="4"\| `00000004` | *Grapefruit Sky (Pasta Primavera Mix)* | A medium-tempo swing track with piano, drums, and mallet percussion. Used in Harvey's 14-heart event. |
| `heavy` | `Wavebank` | `51` | data-sort-value="51"\| `00000033` | *Sam's Band (heavy)* (jukebox);; *Sam's Band (Heavy Version)* (soundtrack) | Loud drums and bass. Used by Sam's 8-heart event. |
| `honkytonky` | `Wavebank` | `52` | data-sort-value="52"\| `00000034` | *Sam's Band (Bluegrass)* (jukebox); *Sam's Band (Bluegrass Version)* (soundtrack) | An uptempo fiddle and banjo tune. Used by Sam's 8-heart event. |
| `Icicles` | `Wavebank` | `68` | data-sort-value="68"\| `00000044` | *Mines (Icicles)* | A slower track with synths and some drums. Plays in The Mines in levels 40-79. |
| `IslandMusic` | `Wavebank` | `374` | data-sort-value="374"\| `00000176` | *Ginger Island* | A tropical tune featuring steel drums, castanets, güiro, and flute. Plays on Ginger Island. |
| `jaunty` | `Wavebank` | `41` | data-sort-value="41"\| `00000029` | *Jaunty* | A happy flute and harpsichord duet in 3. Used in various heart events. |
| `junimoKart` | `Wavebank(1.4)` | `20` | data-sort-value="20"\| `00000014` | *Junimo Kart (Title Theme)* (jukebox);; *JunimoKart (Title Theme)* (soundtrack) | An upbeat synth track. Plays in the title menus of Junimo Kart. |
| `junimoKart_ghostMusic` | `Wavebank(1.4)` | `0` | data-sort-value="0"\| `00000000` | *Junimo Kart (Ghastly Galleon)* (jukebox);; *JunimoKart (Ghastly Galleon)* (soundtrack) | A slow, spooky track with ghostly "ooo"s. Plays in the Ghastly Galleon levels of JunimoKart. |
| `junimoKart_mushroomMusic` | `Wavebank(1.4)` | `21` | data-sort-value="21"\| `00000015` | *Junimo Kart (Glowshroom Grotto)* (jukebox);; *JunimoKart (Glowshroom Grotto)* (soundtrack) | A slower synth track with a hi-hat beat. Plays in the Glowshroom Grotto levels of JunimoKart. |
| `junimoKart_slimeMusic` | `Wavebank(1.4)` | `22` | data-sort-value="22"\| `00000016` | *Junimo Kart (Slomp's Stomp)* (soundtrack);; *JunimoKart (Slomp's Stomp)* (soundtrack) | An uptempo track in harmonic minor featuring oboe, a stringed instrument, güiro, and the occasional slime splat noise. Plays in the Slomp's Stomp levels of JunimoKart. |
| `junimoKart_whaleMusic` | `Wavebank(1.4)` | `1` | data-sort-value="1"\| `00000001` | *Junimo Kart (The Gem Sea Giant)* (jukebox);; *JunimoKart (The Gem Sea Giant)* (soundtrack) | A slow, mystical synth track with some pitch bending. Plays in the Gem Sea Giant levels of JunimoKart. |
| `junimoStarSong` | `Wavebank` | `308` | data-sort-value="308"\| `00000134` | *A Golden Star Is Born* | A hopeful synth track. Plays in the Community Center after various completion milestones. |
| `kindadumbautumn` | `Wavebank` | `282` | data-sort-value="282"\| `0000011a` | *Grapefruit Sky* (jukebox);; *Grapefruit Sky (Dr. Harvey's Theme)* (soundtrack) | A peaceful guitar and flute duet. Used in several of Harvey and Maru's heart events. |
| `LavaMine` | `Wavebank` | `72` | data-sort-value="72"\| `00000048` |  | Plays one of either `Of Dwarves`, `Near The Planet Core`, `Overcast`, or `tribal`. |
| `73` | data-sort-value="73"\| `00000049` |  |  |  |  |
| `198` | data-sort-value="198"\| `000000c6` |  |  |  |  |
| `215` | data-sort-value="215"\| `000000d7` |  |  |  |  |
| `libraryTheme` | `Wavebank` | `341` | data-sort-value="341"\| `00000155` | *Library and Museum* (jukebox);; *The Library and Museum* (soundtrack) | A cheerful banjo and flute duet. Plays as background music in the Museum. |
| `MainTheme` | `Wavebank` | `335` | data-sort-value="335"\| `0000014f` | *Stardew Valley Overture* | A peaceful guitar and flute theme. Plays in the title menus for the game. |
| `Majestic` | `Wavebank` | `121` | data-sort-value="121"\| `00000079` |  | Alias for `fall1` that is unused by Vanilla. |
| `MarlonsTheme` | `Wavebank` | `349` | data-sort-value="349"\| `0000015d` | *Marlon's Theme* (jukebox);; *The Adventure Guild* (soundtrack) | A banjo melody accompanied by flute and string bass. Used as background music for the Adventurer's Guild. |
| `marnieShop` | `Wavebank` | `180` | data-sort-value="180"\| `000000b4` | *Country Shop* | A happy banjo, violin, and flute melody accompanied by tambourine. Plays at Marnie's Ranch and the Carpenter's Shop. |
| `mermaidSong` | `Wavebank` | `362` | data-sort-value="362"\| `0000016a` | *Mermaid* (jukebox);; *Mermaid Song* (soundtrack) | A slow, mystical synth track with vocals over top. Plays during the Night Market's mermaid show. |
| `moonlightJellies` | `Wavebank` | `303` | data-sort-value="303"\| `0000012f` | *Dance of the Moonlight Jellies* | A peaceful synth track with the sound of ocean waves in the background. Plays during the Dance of the Moonlight Jellies. |
| `movie_classic` | `Wavebank(1.4)` | `7` | data-sort-value="7"\| `00000007` | *The Zuzu City Express Theme* (jukebox);; *The Zuzu City Express (Movie Theme)* (soundtrack) | A peaceful string ballad with quiet mallet percussion. Used in The Zuzu City Express movie. |
| `movie_nature` | `Wavebank(1.4)` | `8` | data-sort-value="8"\| `00000008` | Exploring Our Vibrant World* (jukebox);; *Exploring Our Vibrant World (Movie Theme)* | A mysterious track with string, marimba, and slow drums. A flute melody enters midway. Used in the Natural Wonders: Exploring Our Vibrant World movie. |
| `movie_wumbus` | `Wavebank(1.4)` | `9` | data-sort-value="9"\| `00000009` | *Wumbus' Theme* (jukebox);; *Wumbus (Movie Theme)* (soundtrack) | A midtempo electric piano track with prominent percussion features. Used the in the Wumbus movie. |
| `movieTheater` | `Wavebank(1.4)` | `10` | data-sort-value="10"\| `0000000a` | *Movie Theater* | An uptempo swing track with electric piano, vibraphone, and drums. Default music in the Movie Theater. |
| `movieTheaterAfter` | `Wavebank(1.4)` | `11` | data-sort-value="11"\| `0000000b` | *Movie Theater (Closing Time)* | A slower rendititon of `movieTheater` with piano and clarinet. Plays after a movie ends at the Movie Theater. |
| `musicboxsong` | `Wavebank` | `44` | data-sort-value="44"\| `0000002c` | *Music Box Song* | A slow music box and synth melody. Plays in various cutscenes, such as the Community Center Reopening Ceremony (Pierre's memories). |
| `Near The Planet Core` | `Wavebank` | `72` | data-sort-value="72"\| `00000048` | *Mines (a Visitor to the Unknown)* (jukebox);; *Mines (Visitor to the Unknown)* (soundtrack) | An uptempo synth, bass, flute, and glockenspiel track with the occasional wind gust. Plays in The Mines in levels 80-120. |
| `New Snow` | `Wavebank` | `126` | data-sort-value="126"\| `0000007e` |  | Alias for `winter1` that is unused by Vanilla. |
| `night_market` | `Wavebank` | `364` | data-sort-value="364"\| `0000016c` | *Night Market* | A slow track with mallet percussion, strings, flute, and the sound of ocean waves. Plays during the Night Market. |
| `Of Dwarves` | `Wavebank` | `73` | data-sort-value="73"\| `00000049` | *Mines (The Lava Dwellers)* | A miidtempo track with synths, lots of hi-hat, and whooshing sounds. Plays in The Mines in levels 80-120. |
| `Orange` | `Wavebank` | `122` | data-sort-value="122"\| `0000007a` |  | Alias for `summer1` that is unused by Vanilla. |
| `Overcast` | `Wavebank` | `215` | data-sort-value="215"\| `000000d7` | *Mines (Magical Shoes)* | A mysterious synth track backed by drums. Plays in The Mines in levels 80-120. |
| `Pink Petals` | `Wavebank` | `93` | data-sort-value="93"\| `0000005d` |  | Alias for `spring1` that is unused by Vanilla. |
| `PIRATE_THEME` | `Wavebank` | `390` | data-sort-value="390"\| `00000186` | *Pirate Theme* | A bouncy shanty in a minor key. Plays in Ginger Island's Pirate Cove when Pirates are visiting. |
| `PIRATE_THEME(muffled)` | `Wavebank` | `390` | data-sort-value="390"\| `00000186` | *Pirate Theme (Muffled)* (jukebox only) | A muffled version of `PIRATE_THEME`. Plays in Ginger Island Southeast when Pirates are visiting. |
| `playful` | `Wavebank` | `278` | data-sort-value="278"\| `00000116` | *Playful* | A happy clarinet and xylophone duet with background strings. Used in various heart events. |
| `Plums` | `Wavebank` | `120` | data-sort-value="120"\| `00000078` |  | Alias for `fall3` that is unused by Vanilla. |
| `poppy` | `Wavebank` | `53` | data-sort-value="53"\| `00000035` | *Sam's Band (poppy)* (jukebox);; *Sam's Band (Pop Version)* (soundtrack) | A happy pop song with strong electric guitar, bass, and claps. Used by Sam's 8-heart event. |
| `raccoonSong` | `Wavebank` | `436` | data-sort-value="436"\| `000001b4` | *Raccoon Song* (jukebox only) | An eclectic track with a melody full of chirping. Plays after the player completes all of the requests for the Raccoon. |
| `ragtime` | `Wavebank` | `46` | data-sort-value="46"\| `0000002e` | *Pickle Jar Rag* (Jukebox);; *Pickle Jar Rag (Haley's Theme)* (soundtrack) | A piano ragtime track. Used in various heart events such as Haley's 4-heart event. |
| `sad_kid` | `Wavebank` | `389` | data-sort-value="389"\| `00000185` | *Leo's Song* | A pensieve flute and marimba duet. Plays in various heart events involving Leo. |
| `sadpiano` | `Wavebank` | `47` | data-sort-value="47"\| `0000002f` | *A Dark Corner of the Past* | A wistful piano solo. Used in various heart events. |
| `Saloon1` | `Wavebank` | `350` | data-sort-value="350"\| `0000015e` | *The Stardrop Saloon* | An old-timey ragtime piano solo with the occasional sound of clinking plates and glasses. Plays in the Saloon. |
| `sam_acoustic1` | `Wavebank(1.4)` | `2` | data-sort-value="2"\| `00000002` | *The Happy Junimo Show Theme* | A harmonica song with guitar and tambourine accompaniment. Ends with a guitar solo. Used by Sam's 14-heart event. |
| `sam_acoustic2` | `Wavebank(1.4)` | `3` | data-sort-value="3"\| `00000003` | *Sam's Acoustic Noodling* (jukebox only) | A short acoustic guitar rendition of `sam_acoustic1`. Used by Sam's 14-heart event. |
| `sampractice` | `Wavebank` | `50` | data-sort-value="50"\| `00000032` | *Band Practice* | A short clip of an electric guitar practicing over a loop of drums and claps. Used by Sam's 2-heart event. |
| `sappypiano` | `Wavebank` | `43` | data-sort-value="43"\| `0000002b` |  | A midtempo track featuring sparkling harp notes, synths, flute, and drums. It is unknown why the track is titled this way. Unused by Vanilla. |
| `Secret Gnomes` | `Wavebank` | `66` | data-sort-value="66"\| `00000042` | *Mines (Star Lumpy)* | A mysterious mallet percussion track. Plays in The Mines in levels 1-39. |
| `SettlingIn` | `Wavebank` | `192` | data-sort-value="192"\| `000000c0` | *Settling In* | A happy banjo and flute duet with bass. Plays in the event where the player meets Robin and Lewis after just moving in. |
| `shaneTheme` | `Wavebank` | `361` | data-sort-value="361"\| `00000169` | *Shane's Theme* (jukebox);; *Frozen Pizza and Eggs (Shane's Theme)* (soundtrack) | A sad xylophone and electric guitar track. Used in various cutscenes such as Shane's 4-heart event. |
| `shimmeringbastion` | `Wavebank` | `54` | data-sort-value="54"\| `00000036` | *Sam's Band (Electronic)* (jukebox);; *Sam's Band (Electronic Version)* (soundtrack) | An upbeat synth track with drums. Used by Sam's 8-heart event. |
| `spaceMusic` | `Wavebank` | `285` | data-sort-value="285"\| `0000011d` | *Starwatcher* (jukebox);; *Starwatcher (Maru's Theme)* (soundtrack) | A slow, peaceful synth melody over crickets chirping. Used in various cutscenes such as Maru's 14-heart event. |
| `spirits_eve` | `Wavebank` | `334` | data-sort-value="334"\| `0000014e` | *Spirit's Eve Festival* | A spooky theme. Plays during Spirit's Eve. |
| `spring1` | `Wavebank` | `93` | data-sort-value="93"\| `0000005d` | *Spring (It's a Big World Outside)* | A hopeful track featuring strings, clarinet, and flute. Plays in various outdoor locations in spring. |
| `spring2` | `Wavebank` | `91` | data-sort-value="91"\| `0000005b` | *Spring (The Valley Comes Alive)* |  |
| `spring3` | `Wavebank` | `92` | data-sort-value="92"\| `0000005c` | *Spring (Wild Horseradish Jam)* |  |
| `springsongs` | `Wavebank` | `91` | data-sort-value="91"\| `0000005b` |  |  |
| `92` | data-sort-value="92"\| `0000005c` |  |  |  |  |
| `93` | data-sort-value="93"\| `0000005d` |  |  |  |  |
| `springtown` | `Wavebank` | `94` | data-sort-value="94"\| `0000005e` | *Pelican Town* |  |
| `Stadium_ambient` | `Wavebank` | `356` | data-sort-value="356"\| `00000164` |  |  |
| `starshoot` | `Wavebank` | `42` | data-sort-value="42"\| `0000002a` | *Starshoot* (jukebox only) |  |
| `submarine_song` | `Wavebank` | `366` | data-sort-value="366"\| `0000016e` | *Submarine* (jukebox);; *Submarine Theme* (soundtrack) |  |
| `summer1` | `Wavebank` | `122` | data-sort-value="122"\| `0000007a` | *Summer (Nature's Crescendo)* | A happy track featuring castanets, flute, and bass. Plays in various outdoor locations in summer. |
| `summer2` | `Wavebank` | `123` | data-sort-value="123"\| `0000007b` | *Summer (The Sun Can Bend An Orange Sky)* |  |
| `summer3` | `Wavebank` | `115` | data-sort-value="115"\| `00000073` | *Summer (Tropicala)* |  |
| `SunRoom` | `Wavebank(1.4)` | `17` | data-sort-value="17"\| `00000011` | *Alone With Relaxing Tea* (jukebox);; *Sun Room (Alone with Relaxing Tea)* (soundtrack) |  |
| `sweet` | `Wavebank` | `144` | data-sort-value="144"\| `00000090` | *Buttercup Melody* |  |
| `tickTock` | `Wavebank` | `300` | data-sort-value="300"\| `0000012c` | *Festival Game* |  |
| `tinymusicbox` | `Wavebank` | `296` | data-sort-value="296"\| `00000128` | *Music Box Song* (jukebox only) | The soundtrack's *Music Box Song* is `musicboxsong`, not this one. |
| `title_night` | `Wavebank` | `127` | data-sort-value="127"\| `0000007f` | *Load Game Theme* (jukebox);; *Load Game* (soundtrack) |  |
| `tribal` | `Wavebank` | `198` | data-sort-value="198"\| `000000c6` | *Mines (Danger!)* |  |
| `Tropical Jam` | `Wavebank` | `115` | data-sort-value="115"\| `00000073` |  |  |
| `VolcanoMines` | `Wavebank` | `382` | data-sort-value="382"\| `0000017e` |  |  |
| `384` | data-sort-value="384"\| `00000180` |  |  |  |  |
| `VolcanoMines1` | `Wavebank` | `382` | data-sort-value="382"\| `0000017e` | *Volcano Mines (Molten Jelly)* |  |
| `VolcanoMines2` | `Wavebank` | `384` | data-sort-value="384"\| `00000180` | *Volcano Mines (Forgotten World)* |  |
| `wavy` | `Wavebank` | `95` | data-sort-value="95"\| `0000005f` | *Calico Desert* |  |
| `wedding` | `Wavebank` | `104` | data-sort-value="104"\| `00000068` | *Wedding Theme* (jukebox);; *Wedding Celebration* (soundtrack) |  |
| `winter1` | `Wavebank` | `126` | data-sort-value="126"\| `0000007e` | *Winter (Nocturne of Ice)* | A slow, peaceful track with xylophone and synths. Plays in various outdoor locations in winter. |
| `winter2` | `Wavebank` | `124` | data-sort-value="124"\| `0000007c` | *Winter (The Wind Can Be Still)* | A slow and peaceful synth track. Plays in various outdoor locations in winter. |
| `winter3` | `Wavebank` | `125` | data-sort-value="125"\| `0000007d` | *Winter (Ancient)* |  |
| `WizardSong` | `Wavebank` | `321` | data-sort-value="321"\| `00000141` | *A Glimpse Of The Other World* (jukebox);; *A Glimpse of the Other World (Wizard's Theme)* (soundtrack) |  |
| `woodsTheme` | `Wavebank` | `216` | data-sort-value="216"\| `000000d8` | *In the Deep Woods* |  |
| `XOR` | `Wavebank` | `69` | data-sort-value="69"\| `00000045` | *Mines (Marimba Of Frozen Bone)* (jukebox);; *Mines (Marimba Of Frozen Bones)* (soundtrack) |  |

### Music (ambient)
| cue ID | wavebank | soundbank index | description |  |
| --- | --- | --- | --- | --- |
| dec | hex |  |  |  |
| `babblingBrook` | `Wavebank` | `343` | data-sort-value="343"\| `00000157` | A loop of a stream flowing. Plays when near rivers. |
| `bugLevelLoop` | `Wavebank` | `169` | data-sort-value="169"\| `000000a9` | A loop of bug wings periodically clicking and buzzing. Plays when a swarm happens in the mines. |
| `communityCenter` | `Wavebank` | `307` | data-sort-value="307"\| `00000133` | A loop of the wind blowing, wood beams creaking, and the occasional wind chime. Used as the background audio in the Community Center when it is not yet completed. |
| `cracklingFire` | `Wavebank` | `342` | data-sort-value="342"\| `00000156` | A loop of the sound of fire crackling. Played by fireplace furniture and the campfire near Linus' tent. |
| `darkCaveLoop` | `Wavebank` | `168` | data-sort-value="168"\| `000000a8` | A constant, fairly quiet low loop of air flowing eerily. Appears to be unused in vanilla. |
| `fall_day_ambient` | `Wavebank` | `338` | data-sort-value="338"\| `00000152` | A loop of leaves rustling in the wind, ravens cawing, and the occasional soft wind chime. Used as outdoor ambiance in the fall. |
| `Frost_Ambient` | `Wavebank` | `200` | data-sort-value="200"\| `000000c8` | A loop of wind gusting and a quiet tinkling noise in the background. Used as ambiance in the frozen levels of the mines. |
| `heavyEngine` | `Wavebank` | `344` | data-sort-value="344"\| `00000158` | A loop of an engine rumbling. The sound of the machine outside the Blacksmith. |
| `Hospital_Ambient` | `Wavebank` | `283` | data-sort-value="283"\| `0000011b` | A loop of fans whirring and a machine beeping. Used as background music for many events that take place at Harvey's Clinic. |
| `jojaOfficeSoundscape` | `Wavebank` | `337` | data-sort-value="337"\| `00000151` | A loop of fans whirring and keyboards typing. Used in the intro cutscene for the game. |
| `jungle_ambience` | `Wavebank` | `371` | data-sort-value="371"\| `00000173` | A loop of birds twittering and other rainforest creatures calling. Used as ambiance for Ginger Island East. |
| `Lava_Ambient` | `Wavebank` | `201` | data-sort-value="201"\| `000000c9` | A loop of the wind blowing, and the occasional low rumble. Used as ambiance in the lava levels of the mines. |
| `movieScreenAmbience` | `Wavebank(1.4)` | `6` | data-sort-value="6"\| `00000006` | Begins with people chattering and snacking, which fade out to a projector starting up. This track does not loop. Used when the player goes to see a movie. |
| `nightTime` | `Wavebank` | `224` | data-sort-value="224"\| `000000e0` | A loop of crickets chirping. Used during several overnight events such as the crop fairy. |
| `ocean` | `Wavebank` | `175` | data-sort-value="175"\| `000000af` | A loop of the sound of ocean waves. Plays at the Beach. |
| `pool_ambient` | `Wavebank` | `288` | data-sort-value="288"\| `00000120` | A loop that sounds similar to a faucet running. Used as ambiance in the Spa pool. |
| `rain` | `Wavebank` | `116` | data-sort-value="116"\| `00000074` | A loop of rain pouring. Used on rainy days. |
| `roadnoise` | `Wavebank` | `189` | data-sort-value="189"\| `000000bd` | Used in the intro cutscene. |
| `spring_day_ambient` | `Wavebank` | `179` | data-sort-value="179"\| `000000b3` | A loop of birds chirping. Used as outdoor ambiance in the spring during the day. |
| `spring_night_ambient` | `Wavebank` | `345` | data-sort-value="345"\| `00000159` | A loop of crickets chirping. Used as outdoor ambiance at night in all seasons but winter. |
| `summer_day_ambient` | `Wavebank` | `339` | data-sort-value="339"\| `00000153` | A loop of flies buzzing and birds chirping. Used as outdoor ambiance in the summer during the day. |
| `tropical_island_day_ambient` | `Wavebank` | `376` | data-sort-value="376"\| `00000178` | A loop of leaves rustling in the wind and rainforest creatures calling. Used as outdoor ambiance at Ginger Island during the day. |
| `Upper_Ambient` | `Wavebank` | `199` | data-sort-value="199"\| `000000c7` | A loop of the wind blowing and the occasional sound of water dripping. Used as ambiance in the non-frost/lava levels of the mines |
| `Volcano_Ambient` | `Wavebank` | `377` | data-sort-value="377"\| `00000179` | A loop of fire crackling, lava bubbling, and the wind occasionally gusting. Used as ambiance in the Volcano Dungeon. |
| `waterfall` | `Wavebank` | `427` | data-sort-value="427"\| `000001ab` | A loop of a waterfall rushing. Plays near waterfalls. |
| `waterfall_big` | `Wavebank` | `428` | data-sort-value="428"\| `000001ac` | A loop of a large waterfall rushing. Plays near waterfalls. |
| `wind` | `Wavebank` | `85` | data-sort-value="85"\| `00000055` | A loop of the wind blowing. |
| `winter_day_ambient` | `Wavebank` | `354` | data-sort-value="354"\| `00000162` | A loop of the wind howling and gusting. Used as outdoor ambiance in the winter during the day. |

### Sound
| cue ID | wavebank | soundbank index | description |  |
| --- | --- | --- | --- | --- |
| dec | hex |  |  |  |
| `achievement` | `Wavebank` | `103` | data-sort-value="103"\| `00000067` | A bright "success" synth chime with reverb. Plays when the player gets an achievement. |
| `axchop` | `Wavebank` | `141` | data-sort-value="141"\| `0000008d` | The hollow sound of an axe hitting wood. Used when various wooden things are hit with the Axe. |
| `axe` | `Wavebank` | `1` | data-sort-value="1"\| `00000001` | A higher-pitched version of `axchop`. Plays when various wooden items such as Tappers are placed. |
| `backpackIN` | `Wavebank` | `133` | data-sort-value="133"\| `00000085` | A low-pitched, descending "whoop" noise. Plays when the player holds an item in their hand and deposits it in the Shipping Bin without opening the menu to do so. |
| `barrelBreak` | `Wavebank` | `310` | data-sort-value="310"\| `00000136` | The sound of a wooden crate smashing. Plays when Crates and Barrels are broken in the Mines. |
| `batFlap` | `Wavebank` | `170` | data-sort-value="170"\| `000000aa` | Wings flapping. Used by bats, birds, and when weapons block attacks. |
| `batScreech` | `Wavebank` | `171` | data-sort-value="171"\| `000000ab` | A bat screeching. |
| `bigDeSelect` | `Wavebank` | `2` | data-sort-value="2"\| `00000002` | A short, descending click. Plays when menus are closed. |
| `bigSelect` | `Wavebank` | `3` | data-sort-value="3"\| `00000003` | Variation on the `bigDeSelect` sound cue. |
| `bob` | `Wavebank` | `30` | data-sort-value="30"\| `0000001e` | A low "bloop". Plays while Fishing when the rod has been cast but no fish are on the line. |
| `book_read` | `Wavebank` | `423` | data-sort-value="423"\| `000001a7` | The sound of book pages flipping rapidly. Plays when the player reads a Book. |
| `boop` | `Wavebank` | `98` | data-sort-value="98"\| `00000062` | Various electric sci-fi zaps. Plays randomly as ambiance in the Casino. |
| `99` | data-sort-value="99"\| `00000063` |  |  |  |
| `100` | data-sort-value="100"\| `00000064` |  |  |  |
| `101` | data-sort-value="101"\| `00000065` |  |  |  |
| `boulderBreak` | `Wavebank` | `238` | data-sort-value="238"\| `000000ee` | The sound of a rock shattering. Plays when boulders break. |
| `boulderCrack` | `Wavebank` | `4` | data-sort-value="4"\| `00000004` | A high-pitched "dink" sound. Plays when tools and weapons are forged. |
| `breakingGlass` | `Wavebank` | `284` | data-sort-value="284"\| `0000011c` | The sound of glass shattering. Plays when crystals in the frozen levels of the mines shatter. |
| `breathin` | `Wavebank` | `84` | data-sort-value="84"\| `00000054` | An ascending "whoop" noise. Plays when dialogue boxes are opened. |
| `breathout` | `Wavebank` | `83` | data-sort-value="83"\| `00000053` | A descending "whoop" noise. Plays when dialogue boxes are closed. |
| `bubbles` | `Wavebank` | `235` | data-sort-value="235"\| `000000eb` | A bubbling sound. Played by Kegs, Casks, Oil Makers, the Slime Egg-Press, and the Cauldron furniture when it is turned on. |
| `236` | data-sort-value="236"\| `000000ec` |  |  |  |
| `busDoorOpen` | `Wavebank` | `191` | data-sort-value="191"\| `000000bf` | A hissing noise followed by an ascending "whoop". Used in various cutscenes. |
| `busDriveOff` | `Wavebank` | `309` | data-sort-value="309"\| `00000135` | The sound of a bus engine rumbling that fades out into the distance. Plays when the Bus drives away (such as when the player visits The Desert). |
| `button_press` | `Wavebank` | `411` | data-sort-value="411"\| `0000019b` | A mechanical button click. Used by the Prize Ticket machine menu. |
| `button_tap` | `Wavebank` | `412` | data-sort-value="412"\| `0000019c` | A quiet mechanical button click. Used by the Prize Ticket machine menu. |
| `button1` | `Wavebank` | `250` | data-sort-value="250"\| `000000fa` | A high-pitched "click-click" noise. Plays when bobbers are attached to fishing rods. |
| `cacklingWitch` | `Wavebank` | `323` | data-sort-value="323"\| `00000143` | An cartoony evil witch laugh. Used in the Witch nighttime event. |
| `camel` | `Wavebank(1.4)` | `23` | data-sort-value="23"\| `00000017` | A camel bleating. Plays when the player interacts with the Desert Trader's camel. |
| `cameraNoise` | `Wavebank` | `292` | data-sort-value="292"\| `00000124` | A camera shutter clicking. Plays when screenshots are taken and when Haley takes photos in several events. |
| `cancel` | `Wavebank` | `353` | data-sort-value="353"\| `00000161` | A short shaker noise. Used for various cancellation actions or when the player has insufficient resources to perform an action. |
| `cast` | `Wavebank` | `246` | data-sort-value="246"\| `000000f6` | A short "shwhip" noise. Used by the Fishing Rod when it is cast. |
| `cat` | `Wavebank` | `332` | data-sort-value="332"\| `0000014c` | A cat meowing. |
| `333` | data-sort-value="333"\| `0000014d` |  |  |  |
| `cavedrip` | `Wavebank` | `129` | data-sort-value="129"\| `00000081` | A wet 'dwoop' sound. Used as random background noise in the mines and sewers, and the dripping sound for the volcano dungeon entrance pipe. |
| `clam_tone` | `Wavebank` | `363` | data-sort-value="363"\| `0000016b` | A kalimba-esque tone. Used in the "uh" multiplayer emote and the Mermaid Boat. Flute Blocks play this sound as one of their special sounds. |
| `clank` | `Wavebank` | `173` | data-sort-value="173"\| `000000ad` | A metal-on-metal clanking sound. Plays when Metal Heads take damage, as ambient noise when Robin is constructing a farm building, and when a Pickaxe is used on boulders that it is not strong enough to break. |
| `203` | data-sort-value="203"\| `000000cb` |  |  |  |
| `203` | data-sort-value="203"\| `000000cb` |  |  |  |
| `204` | data-sort-value="204"\| `000000cc` |  |  |  |
| `205` | data-sort-value="205"\| `000000cd` |  |  |  |
| `205` | data-sort-value="205"\| `000000cd` |  |  |  |
| `clubhit` | `Wavebank` | `159` | data-sort-value="159"\| `0000009f` | A low-pitched thud. Used by Clubs. |
| `clubSmash` | `Wavebank` | `174` | data-sort-value="174"\| `000000ae` | A more aggressive and low-pitched thud. Used by Clubs when the special attack is triggered. |
| `clubswipe` | `Wavebank` | `160` | data-sort-value="160"\| `000000a0` | A low-pitched "swish" noise. Plays when Clubs swing. |
| `cluck` | `Wavebank` | `31` | data-sort-value="31"\| `0000001f` | A chicken clucking. |
| `32` | data-sort-value="32"\| `00000020` |  |  |  |
| `33` | data-sort-value="33"\| `00000021` |  |  |  |
| `coin` | `Wavebank` | `5` | data-sort-value="5"\| `00000005` | A "pop" sound. Plays in a variety of situations, including when the player picks up objects. |
| `coldSpell` | `Wavebank` | `197` | data-sort-value="197"\| `000000c5` | A high-pitched and rattly "woosh". Unused in vanilla. |
| `cow` | `Wavebank` | `80` | data-sort-value="80"\| `00000050` | A cow mooing. |
| `81` | data-sort-value="81"\| `00000051` |  |  |  |
| `82` | data-sort-value="82"\| `00000052` |  |  |  |
| `cowboy_dead` | `Wavebank` | `268` | data-sort-value="268"\| `0000010c` | An 8-bit zap noise followed by a disappointed-sounding descending riff. Plays when the player dies in Journey of the Prairie King. |
| `cowboy_explosion` | `Wavebank` | `276` | data-sort-value="276"\| `00000114` | An 8-bit explosion sound. Used by Journey of the Prairie King. |
| `cowboy_gopher` | `Wavebank` | `275` | data-sort-value="275"\| `00000113` | A quick, high-pitched zap. Used by the gopher in Journey of the Prairie King. |
| `cowboy_gunload` | `Wavebank` | `272` | data-sort-value="272"\| `00000110` | An 8-bit version of a gun loading. Used by Journey of the Prairie King. |
| `Cowboy_gunshot` | `Wavebank` | `266` | data-sort-value="266"\| `0000010a` | A short, hollow 'tock' sound. Used for woodpeckers pecks, Journey of the Prairie King guns, darts impact, casino slots, and a hover sound in many in-game menus. |
| `Cowboy_monsterDie` | `Wavebank` | `264` | data-sort-value="264"\| `00000108` | An 8-bit death noise. Plays when monsters die in Journey of the Prairie King. |
| `265` | data-sort-value="265"\| `00000109` |  |  |  |
| `cowboy_monsterhit` | `Wavebank` | `274` | data-sort-value="274"\| `00000112` | A short, hollow "tock" sound. Plays when monsters are damaged in Journey of the Prairie King. |
| `cowboy_powerup` | `Wavebank` | `271` | data-sort-value="271"\| `0000010f` | An ascending, 8-bit zap. Plays when the player gets a powerup in Journey of the Prairie King. |
| `Cowboy_Secret` | `Wavebank` | `263` | data-sort-value="263"\| `00000107` | An ascending, 8-bit jingle. Plays when the player holds up powerups in Journey of the Prairie King. |
| `crafting` | `Wavebank` | `36` | data-sort-value="36"\| `00000024` | A hammer clanking on a nail. Plays when the player tries to damage a hiding Rock Crab. |
| `crane` | `Wavebank(1.4)` | `14` | data-sort-value="14"\| `0000000e` | A looping buzzing sound. Used by the Crane Game. |
| `crickets` | `Wavebank` | `117` | data-sort-value="117"\| `00000075` | A single chirp. Used as random ambient nighttime noise. |
| `cricketsAmbient` | `Wavebank` | `346` | data-sort-value="346"\| `0000015a` | A loop of crickets chirping. Plays as ambient nighttime noise. |
| `crit` | `Wavebank` | `352` | data-sort-value="352"\| `00000160` | A high-pitched "zing". Plays when the player lands a critical hit with their weapon. |
| `croak` | `Wavebank` | `138` | data-sort-value="138"\| `0000008a` | A frog croaking once. Used as ambient noise in the rain and in various cutscenes involving frogs. |
| `crow` | `Wavebank` | `324` | data-sort-value="324"\| `00000144` | A singular crow caw. |
| `crystal` | `Wavebank` | `143` | data-sort-value="143"\| `0000008f` | A high-pitched 'ding!' sound that resonates for a few seconds. Used for the mine elevator ding, island crystal puzzle, in dialogue for the `<` character, Singing Stone (with a random pitch), and mine crystals when broken. |
| `cursed_mannequin` | `Wavebank` | `431` | data-sort-value="431"\| `000001af` | A spooky breathing noise. Plays when the Cursed Mannequin "comes to life". |
| `cut` | `Wavebank` | `6` | data-sort-value="6"\| `00000006` | The sound of foliage swiping. Plays when Weeds are cut. |
| `daggerswipe` | `Wavebank` | `163` | data-sort-value="163"\| `000000a3` | A short swiping noise. Plays when the Dagger is used for a normal attack. |
| `death` | `Wavebank` | `70` | data-sort-value="70"\| `00000046` | A descending marimba riff. Plays when the player is knocked to 0 health. |
| `debuffHit` | `Wavebank` | `151` | data-sort-value="151"\| `00000097` | A "zap" noise. Plays when debuffing projectiles hit the player. |
| `debuffSpell` | `Wavebank` | `152` | data-sort-value="152"\| `00000098` | A similar "zap" sound to `debuffHit`. Plays when debuffing projectiles are fired at the player. |
| `detector` | `Wavebank` | `37` | data-sort-value="37"\| `00000025` | A high-pitched sci-fi "boing". Unused by Vanilla. |
| `dialogueCharacter` | `Wavebank` | `7` | data-sort-value="7"\| `00000007` | A short tone. Plays when the dialogue typing animation is running for dialogue boxes. |
| `dialogueCharacterClose` | `Wavebank` | `8` | data-sort-value="8"\| `00000008` | A short ascending tone. Plays when the last character of the text in a dialogue box is rendered in the typing animation. |
| `dirtyHit` | `Wavebank` | `243` | data-sort-value="243"\| `000000f3` | The sound of digging in the dirt. Plays when crops are planted. |
| `244` | data-sort-value="244"\| `000000f4` |  |  |  |
| `discoverMineral` | `Wavebank` | `208` | data-sort-value="208"\| `000000d0` | A sparkling noise. Plays when the player gets rarer drops from cracking Geodes. |
| `distantTrain` | `Wavebank` | `220` | data-sort-value="220"\| `000000dc` | A train whistle in the distance. Plays when a Train comes but the player is not at the Railroad. |
| `221` | data-sort-value="221"\| `000000dd` |  |  |  |
| `dog_bark` | `Wavebank` | `331` | data-sort-value="331"\| `0000014b` | A dog barking. |
| `dog_pant` | `Wavebank` | `330` | data-sort-value="330"\| `0000014a` | A dog panting. |
| `dogs` | `Wavebank` | `228` | data-sort-value="228"\| `000000e4` | Dogs howling and barking. Plays during the Wild animal attack nighttime event. |
| `dogWhining` | `Wavebank` | `316` | data-sort-value="316"\| `0000013c` | A dog whining and panting. Plays in several events involving Dusty. |
| `doorClose` | `Wavebank` | `9` | data-sort-value="9"\| `00000009` | A door closing. Used by exterior doors throughout the game. |
| `doorCreak` | `Wavebank` | `319` | data-sort-value="319"\| `0000013f` | The sound of creaky door hinges. Plays when a Chest, Shipping Bin, or fridge opens. Also plays when the door of a Coop or Barn closes. |
| `doorCreakReverse` | `Wavebank` | `322` | data-sort-value="322"\| `00000142` | The sound of creaky door hinges. Plays when a Chest, Shipping Bin, or fridge closes. Also plays when the door of a Coop or Barn opens. |
| `doorOpen` | `Wavebank` | `320` | data-sort-value="320"\| `00000140` | The sound of a door opening. Plays when interior doors are opened. |
| `dropItemInWater` | `Wavebank` | `10` | data-sort-value="10"\| `0000000a` | A "sploosh" noise. Plays when items are dropped in water, farm animals swim, and fish splash. |
| `drumkit0` | `Wavebank` | `110` | data-sort-value="110"\| `0000006e` | A low-pitched drum hit. |
| `drumkit1` | `Wavebank` | `111` | data-sort-value="111"\| `0000006f` | A snare drum hit. |
| `drumkit2` | `Wavebank` | `108` | data-sort-value="108"\| `0000006c` | A hit on a clamped hi-hat. |
| `drumkit3` | `Wavebank` | `107` | data-sort-value="107"\| `0000006b` | A hit on an open hi-hat. |
| `drumkit4` | `Wavebank` | `109` | data-sort-value="109"\| `0000006d` | A low-pitched kick drum hit. |
| `drumkit5` | `Wavebank` | `106` | data-sort-value="106"\| `0000006a` | A higher-pitched bongo drum hit. |
| `drumkit6` | `Wavebank` | `105` | data-sort-value="105"\| `00000069` | The highest-pitched drumkit sound. Reminiscent of a digital metronome. Plays when checkboxes in the settings menu are toggled. |
| `Duck` | `Wavebank` | `231` | data-sort-value="231"\| `000000e7` | A duck quacking. |
| `Duggy` | `Wavebank` | `60` | data-sort-value="60"\| `0000003c` | Digging sounds. Plays when a Duggy digs up underneath the player. |
| `dustMeep` | `Wavebank` | `186` | data-sort-value="186"\| `000000ba` | A high-pitched chirp. Used as an alternate Junimo sound. Also used as ambient sounds for Dust Sprites and plays when they die. |
| `DwarvishSentry` | `Wavebank` | `386` | data-sort-value="386"\| `00000182` | A sci-fi "beep-boop" chirp. Plays when a Dwarvish Sentry spawns. |
| `dwoop` | `Wavebank` | `34` | data-sort-value="34"\| `00000022` | A sound that inflects upwards in pitch, but is a lower pitch than `dwop`. Plays when farm animals go inside and when furniture is rotated. |
| `dwop` | `Wavebank` | `234` | data-sort-value="234"\| `000000ea` | A sound that inflects upwards in pitch. Plays when the user clicks on an item in their inventory to lift it up and freely drag it around, when the trash bear throws an item up into the air, when bobbers are removed from fishing rods, etc. |
| `eat` | `Wavebank` | `25` | data-sort-value="25"\| `00000019` | A chomping noise. Plays when the player eats food. |
| `explosion` | `Wavebank` | `35` | data-sort-value="35"\| `00000023` | A loud "boom". Plays when bombs explode. |
| `fairy_heal` | `Wavebank` | `421` | data-sort-value="421"\| `000001a5` | A twinkling noise. Plays when the Fairy Box trinket heals the player. |
| `fallDown` | `Wavebank` | `318` | data-sort-value="318"\| `0000013e` | A descending tone. Plays when the player jumps in a shaft in the Skull Cavern. |
| `fastReel` | `Wavebank` | `248` | data-sort-value="248"\| `000000f8` | The sound of the fishing rod reeling. Plays when the fishing bar is behind the fish during the fishing minigame. |
| `fireball` | `Wavebank` | `71` | data-sort-value="71"\| `00000047` | The sound of fire roaring. Plays when interior Fireplaces are lit, when monsters fire flaming projectiles, and when Butterfly Powder is used. |
| `firework` | `Wavebank` | `429` | data-sort-value="429"\| `000001ad` | A firework taking off and then exploding after a few seconds. Plays when Fireworks are used. |
| `fishBite` | `Wavebank` | `26` | data-sort-value="26"\| `0000001a` | A warbling noise. This is the default sound that plays when a fish bites. Players may select this sound as their fish bite sound in the options menu. |
| `fishBite_alternate_0` | `Wavebank` | `398` | data-sort-value="398"\| `0000018e` | A higher-pitched version of `fishBite`. Players may select this sound as their fish bite sound in the options menu. |
| `fishBite_alternate_1` | `Wavebank` | `397` | data-sort-value="397"\| `0000018d` | A lower-pitched version of `fishBite`. Players may select this sound as their fish bite sound in the options menu. |
| `fishBite_alternate_2` | `Wavebank` | `396` | data-sort-value="396"\| `0000018c` | A more watery version of `fishBite`. Players may select this sound as their fish bite sound in the options menu. |
| `fishEscape` | `Wavebank` | `253` | data-sort-value="253"\| `000000fd` | A disappointing echo-y sound. Plays when fish escape during the fishing minigame. |
| `FishHit` | `Wavebank` | `251` | data-sort-value="251"\| `000000fb` | A "ding". Plays when the player successfully hooks a fish and the fishing minigame starts. |
| `fishingRodBend` | `Wavebank` | `254` | data-sort-value="254"\| `000000fe` | A creaking noise. Plays during the fishing minigame when the player clicks. |
| `255` | data-sort-value="255"\| `000000ff` |  |  |  |
| `256` | data-sort-value="256"\| `00000100` |  |  |  |
| `fishSlap` | `Wavebank` | `260` | data-sort-value="260"\| `00000104` | A wet squelching noise. Plays when Fish Ponds are emptied, after the player catches a fish, and when the Bait Maker is processing. |
| `flameSpell` | `Wavebank` | `150` | data-sort-value="150"\| `00000096` | The sound of fire roaring and a spell casting. Used in several events, including Sebastian's 6-heart event. |
| `flameSpellHit` | `Wavebank` | `149` | data-sort-value="149"\| `00000095` | The sound of a fireball colliding with something. Used in several events, including Sebastian's 6-heart event. |
| `flute` | `Wavebank` | `112` | data-sort-value="112"\| `00000070` | A flute playing a single tone. Used by Flute Blocks. |
| `flybuzzing` | `Wavebank` | `164` | data-sort-value="164"\| `000000a4` | The sound of insect wings buzzing. Used by flying insects such as the Cave Fly. |
| `frog_slap` | `Wavebank` | `422` | data-sort-value="422"\| `000001a6` | A slimey splat. Plays when the frog from the Frog Egg trinket hops. |
| `frozen` | `Wavebank` | `394` | data-sort-value="394"\| `0000018a` | The sound of ice quickly forming. Plays when projectiles from the Ice Rod hit enemies. |
| `furnace` | `Wavebank` | `38` | data-sort-value="38"\| `00000026` | The sound of fire roaring. Plays when a Furnace is processing and a Pepper Rex breathes fire. |
| `fuse` | `Wavebank` | `48` | data-sort-value="48"\| `00000030` | A fuse hissing. Plays when bombs are placed down before they explode. |
| `getNewSpecialItem` | `Wavebank` | `223` | data-sort-value="223"\| `000000df` | A sparkling jingle. Often used in events when the player learns a new crafting recipe or receives items. |
| `ghost` | `Wavebank` | `11` | data-sort-value="11"\| `0000000b` | A low moan. Plays when Ghosts and Mummies die. |
| `give_gift` | `Wavebank` | `348` | data-sort-value="348"\| `0000015c` | A short "confirmation" jingle. Plays when the player gives a gift to an NPC. |
| `glug` | `Wavebank` | `325` | data-sort-value="325"\| `00000145` | The sound of liquid glugging in a bottle. Used when Watering Cans are filled, in several heart events, and in a farmer drinking animation. |
| `goat` | `Wavebank` | `78` | data-sort-value="78"\| `0000004e` | A goat bleating. |
| `79` | data-sort-value="79"\| `0000004f` |  |  |  |
| `goldenWalnut` | `Wavebank` | `372` | data-sort-value="372"\| `00000174` | A high-pitched wood block jingle. Plays when the player collects a Golden Walnut. |
| `gorilla_intro` | `Wavebank` | `388` | data-sort-value="388"\| `00000184` | An ~10 second song of jungle drums, a gorilla snorting, and various animal noises. Used when the player offers a banana to the Gorilla on Ginger Island. |
| `grunt` | `Wavebank` | `12` | data-sort-value="12"\| `0000000c` | A low-pitched grunt. Used as death sounds for several monsters, when the player offers a banana to the Gorilla on Ginger Island, and in Emily's 10-heart event. |
| `gulp` | `Wavebank` | `239` | data-sort-value="239"\| `000000ef` | Loud drinking sounds. Plays when the player drinks. |
| `240` | data-sort-value="240"\| `000000f0` |  |  |  |
| `hammer` | `Wavebank` | `134` | data-sort-value="134"\| `00000086` | The dull thud of metal on rock. Plays when various items are broken by tools in the game. |
| `harvest` | `Wavebank` | `326` | data-sort-value="326"\| `00000146` | A "pop" noise. Plays when crops are harvested, either by the player or by Junimos. |
| `healSound` | `Wavebank` | `196` | data-sort-value="196"\| `000000c4` | An ascending regeneration sound. Used by several events, when the player regenerates health from the Vampiric enchantment, and when a Shadow Shaman heals nearby monsters. |
| `hitEnemy` | `Wavebank` | `56` | data-sort-value="56"\| `00000038` | The sound of a weapon swinging combined with a "hit" sound. Used in a wide variety of situations, such as when the player deals damage to an enemy. |
| `hoeHit` | `Wavebank` | `13` | data-sort-value="13"\| `0000000d` | The sound of digging in the dirt. Plays when dirt is tilled with a Hoe. |
| `horse_flute` | `Wavebank` | `395` | data-sort-value="395"\| `0000018b` | A flute playing a short melody. Plays when the Horse Flute is used. |
| `401` | data-sort-value="401"\| `00000191` |  |  |  |
| `402` | data-sort-value="402"\| `00000192` |  |  |  |
| `jingle1` | `Wavebank` | `252` | data-sort-value="252"\| `000000fc` | A high-pitched "ding". Used when a fish is caught and when journal or fish pond quests are completed. |
| `junimoKart_coin` | `Wavebank(1.4)` | `16` | data-sort-value="16"\| `00000010` | A high-pitched, metallic "ding". Plays when the player collects a coin in Junimo Kart. |
| `junimoMeep1` | `Wavebank` | `306` | data-sort-value="306"\| `00000132` | A high-pitched chirp. Used by Junimos. |
| `keyboardTyping` | `Wavebank` | `293` | data-sort-value="293"\| `00000125` | An ~8 second loop of a mechanical keyboard clacking. Used in Sebastian's 2-heart event. |
| `killAnimal` | `Wavebank` | `233` | data-sort-value="233"\| `000000e9` | The gruesome sound of a whoosh and bones crushing. Used only by Sebastian's 6-heart event. |
| `leafrustle` | `Wavebank` | `142` | data-sort-value="142"\| `0000008e` | Leaves rustling. Plays when trees are shaken, critters jump into bushes, bushes are shaken, and more. |
| `machine_bell` | `Wavebank` | `413` | data-sort-value="413"\| `0000019d` | A bell dinging once. Plays when the player claims a prize in the Prize Machine. |
| `magic_arrow` | `Wavebank` | `420` | data-sort-value="420"\| `000001a4` | A quick, quiet "whoosh". Plays when the Magic Quiver fires an arrow. |
| `magic_arrow_hit` | `Wavebank` | `419` | data-sort-value="419"\| `000001a3` | A high-pitched damage sound. Plays when arrows from the Magic Quiver hit an enemy. |
| `magma_sprite_die` | `Wavebank` | `380` | data-sort-value="380"\| `0000017c` | A screech combined with fire roaring. Plays when a Magma Sprite dies. |
| `magma_sprite_hit` | `Wavebank` | `379` | data-sort-value="379"\| `0000017b` | The sound of fire whooshing. Plays when a Magma Sprite takes damage. |
| `magma_sprite_spot` | `Wavebank` | `381` | data-sort-value="381"\| `0000017d` | An airy screech. Plays when a Magma Sprite spots the player and begins to attack them. |
| `metal_tap` | `Wavebank` | `203` | data-sort-value="203"\| `000000cb` | A high-pitched metal clank. Plays when the player uses an Anvil. |
| `Meteorite` | `Wavebank` | `225` | data-sort-value="225"\| `000000e1` | A whooshing noise. Plays during the Meteorite nighttime event. |
| `Milking` | `Wavebank` | `230` | data-sort-value="230"\| `000000e6` | The sound of liquid filling up a pail. Plays when the Milk Pail is used. |
| `minecartLoop` | `Wavebank` | `188` | data-sort-value="188"\| `000000bc` | A high-pitched loop of a minecart on tracks. Used as a sound effect in Junimo Kart. |
| `miniharp_note` | `Wavebank(1.4)` | `5` | data-sort-value="5"\| `00000005` | A harpsichord tone. Used by the "music" multiplayer emote and by Flute Blocks when the player is holding an Amethyst. |
| `money` | `Wavebank` | `61` | data-sort-value="61"\| `0000003d` | A single "ding". Plays in various situations, such as when the game saves. |
| `moneyDial` | `Wavebank` | `237` | data-sort-value="237"\| `000000ed` | A high-pitched sound of coins falling. Plays repeatedly on a new day when the money from the previous day's shipping is added to the player's farm. |
| `monkey1` | `Wavebank` | `385` | data-sort-value="385"\| `00000181` | A monkey chattering. Used by the monkeys in the Volcano Caldera. |
| `monsterdead` | `Wavebank` | `158` | data-sort-value="158"\| `0000009e` | A "splat" noise. Used as a death sound for various monsters. |
| `moss_cut` | `Wavebank` | `409` | data-sort-value="409"\| `00000199` | A rustling noise. Plays when Moss is harvested from a tree. |
| `mouseClick` | `Wavebank` | `294` | data-sort-value="294"\| `00000126` | A computer mouse clicking once. Used in Sebastian's 2-heart event. |
| `newArtifact` | `Wavebank` | `211` | data-sort-value="211"\| `000000d3` | A low-pitched "ding". Plays when UI messages such as skill mastery level-ups appear, items are donated to bundles or the museum, the Night Market submarine reaches the ocean floor, breaking open a geode containing common items at the Blacksmith, and in other menus throughout the game. |
| `newRecipe` | `Wavebank` | `214` | data-sort-value="214"\| `000000d6` | The sound of a page turning. Plays when the player learns a new recipe. |
| `newRecord` | `Wavebank` | `213` | data-sort-value="213"\| `000000d5` | A sparkling jingle. Plays when the player catches a fish of record size and at night when the player levels up in a skill. |
| `objectiveComplete` | `Wavebank` | `132` | data-sort-value="132"\| `00000084` | A rebounding jingly noise. Plays when a weapon's secondary attack cooldown is over. |
| `openBox` | `Wavebank` | `14` | data-sort-value="14"\| `0000000e` | The low-pitched sound of a latch opening. Plays in a variety of situations, including when Pressure Pads are activated in the Volcano Dungeon, when the player interacts with the Telephone, and when Calico Statues are activated. |
| `openChest` | `Wavebank` | `161` | data-sort-value="161"\| `000000a1` | A click and then the creak of hinges. Plays when a Chest opens. |
| `Ostrich` | `Wavebank` | `367` | data-sort-value="367"\| `0000016f` | An ostrich cooing. |
| `ow` | `Wavebank` | `63` | data-sort-value="63"\| `0000003f` | Plays when the player takes damage. |
| `owl` | `Wavebank` | `227` | data-sort-value="227"\| `000000e3` | An owl hooting. Plays during the Stone Owl nighttime event. |
| `parachute` | `Wavebank` | `414` | data-sort-value="414"\| `0000019e` | The rustling sound of a parachute. Used in the plane fly-by cutscene that enables the Mystery Box mechanic. |
| `parrot` | `Wavebank` | `360` | data-sort-value="360"\| `00000168` | A parrot squawking. Used by the parrots on Ginger Island. |
| `parrot_flap` | `Wavebank` | `432` | data-sort-value="432"\| `000001b0` | The sound of parrot wings flapping. Used by the parrots on Ginger Island. |
| `parrot_squawk` | `Wavebank` | `373` | data-sort-value="373"\| `00000175` | A parrot squawking. Used by the parrots on Ginger Island. |
| `parry` | `Wavebank` | `173` | data-sort-value="173"\| `000000ad` | A "clink" noise. Plays when an attack is successfully parried. |
| `phone` | `Wavebank` | `113` | data-sort-value="113"\| `00000071` | A phone ringing. Plays when the Telephone rings. |
| `Pickup_Coin15` | `Wavebank` | `273` | data-sort-value="273"\| `00000111` | An electronic metallic "ding". Plays when the player collects coins in Journey of the Prairie King. |
| `pickUpItem` | `Wavebank` | `15` | data-sort-value="15"\| `0000000f` | A "pop" sound. Plays when forageables are picked up. |
| `pig` | `Wavebank` | `130` | data-sort-value="130"\| `00000082` | A pig oinking. |
| `131` | data-sort-value="131"\| `00000083` |  |  |  |
| `planeflyby` | `Wavebank` | `415` | data-sort-value="415"\| `0000019f` | The whirring of a propellor plane. Used in the plane fly-by cutscene that enables the Mystery Box mechanic. |
| `potterySmash` | `Wavebank` | `147` | data-sort-value="147"\| `00000093` | The sound of pottery shattering. Used in the slingshot minigame at the Stardew Valley Fair. |
| `powerup` | `Wavebank` | `39` | data-sort-value="39"\| `00000027` | An ascending sci-fi powerup noise. Plays when the player uses a powerup in Journey of the Prairie King. |
| `pullItemFromWater` | `Wavebank` | `28` | data-sort-value="28"\| `0000001c` | Water splashing. Plays in various situations, such as when the player starts/stops swimming, when fish are caught, and more. |
| `purchase` | `Wavebank` | `145` | data-sort-value="145"\| `00000091` | A few coins clinking together. Plays when items are sold to shops. |
| `146` | data-sort-value="146"\| `00000092` |  |  |  |
| `241` | data-sort-value="241"\| `000000f1` |  |  |  |
| `purchaseClick` | `Wavebank` | `146` | data-sort-value="146"\| `00000092` | A slightly higher-pitched version of `purchase`. Plays when items are bought from shops. |
| `purchaseRepeat` | `Wavebank` | `241` | data-sort-value="241"\| `000000f1` | A slightly higher-pitched version of `purchase`. Plays when the player buys multiple items from shops in quick succession. |
| `qi_shop` | `Wavebank` | `399` | data-sort-value="399"\| `0000018f` | A sci-fi jingle. Plays when the player opens the shop in Qi's Walnut Room. |
| `qi_shop_purchase` | `Wavebank` | `400` | data-sort-value="400"\| `00000190` | Gems clinking together. Plays when the player purchases from the shop in Qi's Walnut Room. |
| `questcomplete` | `Wavebank` | `128` | data-sort-value="128"\| `00000080` | An ascending "success" jingle. Plays when the player completes quests. |
| `quickSlosh` | `Wavebank` | `290` | data-sort-value="290"\| `00000122` | A more subtle variation of the `slosh` sound cue. |
| `291` | data-sort-value="291"\| `00000123` |  |  |  |
| `rabbit` | `Wavebank` | `74` | data-sort-value="74"\| `0000004a` | A rustling noise used when rabbits are pet. |
| `Raccoon` | `Wavebank` | `424` | data-sort-value="424"\| `000001a8` | The chittering of a raccoon. Plays when interacting with the raccoons in the repaired Giant Stump. |
| `425` | data-sort-value="425"\| `000001a9` |  |  |  |
| `rainsound` | `Wavebank` | `135` | data-sort-value="135"\| `00000087` | Various eerie ambient noises that play on rainy days. |
| `136` | data-sort-value="136"\| `00000088` |  |  |  |
| `137` | data-sort-value="137"\| `00000089` |  |  |  |
| `reward` | `Wavebank` | `212` | data-sort-value="212"\| `000000d4` | A sparkling "success" jingle. Plays in various situations, such as when the player finds a wallet item. |
| `robotBLASTOFF` | `Wavebank` | `287` | data-sort-value="287"\| `0000011f` | A whooshing rocket launch build-up sound. |
| `robotSoundEffects` | `Wavebank` | `286` | data-sort-value="286"\| `0000011e` | Robotic hissing followed by high-pitched beeps. |
| `rockGolemDie` | `Wavebank` | `166` | data-sort-value="166"\| `000000a6` | A high-pitched squeal. Plays when the player kills a Stone Golem. |
| `rockGolemHit` | `Wavebank` | `167` | data-sort-value="167"\| `000000a7` | A high-pitched grunt. Plays when the player hits a Stone Golem. |
| `rockGolemSpawn` | `Wavebank` | `165` | data-sort-value="165"\| `000000a5` | The sound of rocks tumbling. Plays when a Stone Golem appears. |
| `rooster` | `Wavebank` | `329` | data-sort-value="329"\| `00000149` | A rooster crowing. Plays at the start of each day. |
| `scissors` | `Wavebank` | `229` | data-sort-value="229"\| `000000e5` | Scissors snipping. Plays when Shears are used. |
| `seagulls` | `Wavebank` | `176` | data-sort-value="176"\| `000000b0` | A seagull squawking. |
| `177` | data-sort-value="177"\| `000000b1` |  |  |  |
| `178` | data-sort-value="178"\| `000000b2` |  |  |  |
| `secret1` | `Wavebank` | `218` | data-sort-value="218"\| `000000da` | A mystical chime. Plays when the player discovers various secrets, such as finding the sweetest taste for Old Master Cannoli. |
| `seeds` | `Wavebank` | `17` | data-sort-value="17"\| `00000011` | A hollow squelching noise. |
| `select` | `Wavebank` | `148` | data-sort-value="148"\| `00000094` | A high-pitched hollow rising 'pook!' sound. Used as a click/select sound in many menus (e.g. clicking a button on the title screen). |
| `sell` | `Wavebank` | `242` | data-sort-value="242"\| `000000f2` | The clink of a single coin. Plays when the player sells an item to a vendor. |
| `serpentDie` | `Wavebank` | `315` | data-sort-value="315"\| `0000013b` | A synthesized, echoing scream. Plays when a serpent dies. |
| `serpentHit` | `Wavebank` | `314` | data-sort-value="314"\| `0000013a` | A single synthy "thud". Plays when the player attacks a serpent. |
| `sewing_loop` | `Wavebank(1.4)` | `15` | data-sort-value="15"\| `0000000f` | A looping mechanical click-clack. Plays when the player processes items through the Sewing Machine. |
| `shadowDie` | `Wavebank` | `194` | data-sort-value="194"\| `000000c2` | An airy whooshing. Plays when a Shadow Brute or Shadow Shaman dies. |
| `shadowHit` | `Wavebank` | `195` | data-sort-value="195"\| `000000c3` | A single chewy "thwack". Plays when the player attacks a Shadow Brute or Shadow Shaman. |
| `shadowpeep` | `Wavebank` | `18` | data-sort-value="18"\| `00000012` | A surprised, whale-like mumble. Heard as intermittent ambient noise from Shadow Brutes and Shadow Shamans. |
| `sheep` | `Wavebank` | `232` | data-sort-value="232"\| `000000e8` | A sheep bleating. |
| `shiny4` | `Wavebank` | `19` | data-sort-value="19"\| `00000013` | A quiet "drrt" sound. Plays when using the scroll bar. |
| `Ship` | `Wavebank` | `96` | data-sort-value="96"\| `00000060` | The sound of items dropping into a wooden box. Used in a variety of situations, including when items are dropped into the Shipping Bin. |
| `97` | data-sort-value="97"\| `00000061` |  |  |  |
| `shwip` | `Wavebank` | `317` | data-sort-value="317"\| `0000013d` | A swishy sound. Plays when scrolling through certain menus, such as the Jukebox music selection screen. |
| `SinWave` | `Wavebank` | `245` | data-sort-value="245"\| `000000f5` |  |
| `sipTea` | `Wavebank` | `193` | data-sort-value="193"\| `000000c1` |  |
| `skeletonDie` | `Wavebank` | `183` | data-sort-value="183"\| `000000b7` | The Rattle of bones falling. Used when a skeleton dies. |
| `skeletonHit` | `Wavebank` | `184` | data-sort-value="184"\| `000000b8` | Bones clanking. Used when the player attacks a skeleton. |
| `skeletonStep` | `Wavebank` | `182` | data-sort-value="182"\| `000000b6` | A single soft bony step. Used when skeletons move. |
| `slime` | `Wavebank` | `57` | data-sort-value="57"\| `00000039` | A "boing" noise. Used when slimes lunge at the player. |
| `slimedead` | `Wavebank` | `59` | data-sort-value="59"\| `0000003b` | A "splat" sound. Used when slimes die. |
| `156` | data-sort-value="156"\| `0000009c` |  |  |  |
| `157` | data-sort-value="157"\| `0000009d` |  |  |  |
| `slimeHit` | `Wavebank` | `185` | data-sort-value="185"\| `000000b9` | A wet squelching sound. Used when the player attacks a slime. |
| `slingshot` | `Wavebank` | `207` | data-sort-value="207"\| `000000cf` | The sound of something being drawn tight. Plays when the Slingshot is drawn back. |
| `slosh` | `Wavebank` | `289` | data-sort-value="289"\| `00000121` | Water sloshing. Plays in a variety of situations, including when a Pan is used. |
| `290` | data-sort-value="290"\| `00000122` |  |  |  |
| `291` | data-sort-value="291"\| `00000123` |  |  |  |
| `slowReel` | `Wavebank` | `247` | data-sort-value="247"\| `000000f7` | The sound of a fishing rod reeling. Plays when the fishing bar is no longer behind the fish in the fishing minigame. |
| `smallSelect` | `Wavebank` | `20` | data-sort-value="20"\| `00000014` | A shorter variant of the `select` sound cue. Used as a click/select sound in many menus (e.g. clicking through to the next dialogue box). |
| `SpringBirds` | `Wavebank` | `86` | data-sort-value="86"\| `00000056` | A bird chirping. Used as ambient noise. |
| `87` | data-sort-value="87"\| `00000057` |  |  |  |
| `88` | data-sort-value="88"\| `00000058` |  |  |  |
| `89` | data-sort-value="89"\| `00000059` |  |  |  |
| `90` | data-sort-value="90"\| `0000005a` |  |  |  |
| `squid_bubble` | `Wavebank` | `392` | data-sort-value="392"\| `00000188` |  |
| `squid_hit` | `Wavebank` | `393` | data-sort-value="393"\| `00000189` |  |
| `squid_move` | `Wavebank` | `391` | data-sort-value="391"\| `00000187` |  |
| `Stadium_cheer` | `Wavebank` | `355` | data-sort-value="355"\| `00000163` |  |
| `stairsdown` | `Wavebank` | `313` | data-sort-value="313"\| `00000139` | The sound of footsteps descending. Plays when the player descends ladders. |
| `stardrop` | `Wavebank` | `351` | data-sort-value="351"\| `0000015f` |  |
| `statue_of_blessings` | `Wavebank` | `417` | data-sort-value="417"\| `000001a1` |  |
| `steam` | `Wavebank` | `378` | data-sort-value="378"\| `0000017a` |  |
| `stone_button` | `Wavebank` | `418` | data-sort-value="418"\| `000001a2` |  |
| `stoneCrack` | `Wavebank` | `75` | data-sort-value="75"\| `0000004b` |  |
| `76` | data-sort-value="76"\| `0000004c` |  |  |  |
| `stumpCrack` | `Wavebank` | `217` | data-sort-value="217"\| `000000d9` |  |
| `submarine_landing` | `Wavebank` | `365` | data-sort-value="365"\| `0000016d` |  |
| `swordswipe` | `Wavebank` | `58` | data-sort-value="58"\| `0000003a` |  |
| `162` | data-sort-value="162"\| `000000a2` |  |  |  |
| `telephone_buttonPush` | `Wavebank` | `369` | data-sort-value="369"\| `00000171` |  |
| `telephone_dialtone` | `Wavebank` | `370` | data-sort-value="370"\| `00000172` |  |
| `telephone_ringingInEar` | `Wavebank` | `368` | data-sort-value="368"\| `00000170` |  |
| `terraria_boneSerpent` | `Wavebank` | `407` | data-sort-value="407"\| `00000197` |  |
| `terraria_meowmere` | `Wavebank` | `405` | data-sort-value="405"\| `00000195` |  |
| `406` | data-sort-value="406"\| `00000196` |  |  |  |
| `terraria_warp` | `Wavebank` | `404` | data-sort-value="404"\| `00000194` |  |
| `throw` | `Wavebank` | `187` | data-sort-value="187"\| `000000bb` |  |
| `throwDownITem` | `Wavebank` | `21` | data-sort-value="21"\| `00000015` |  |
| `thunder` | `Wavebank` | `114` | data-sort-value="114"\| `00000072` | A loud thunderclap. |
| `thunder_small` | `Wavebank` | `327` | data-sort-value="327"\| `00000147` | Thunder rumbling. |
| `328` | data-sort-value="328"\| `00000148` |  |  |  |
| `ticket_machine_whir` | `Wavebank` | `410` | data-sort-value="410"\| `0000019a` |  |
| `tinyWhip` | `Wavebank` | `249` | data-sort-value="249"\| `000000f9` |  |
| `toolCharge` | `Wavebank` | `62` | data-sort-value="62"\| `0000003e` |  |
| `toolSwap` | `Wavebank` | `27` | data-sort-value="27"\| `0000001b` |  |
| `toyPiano` | `Wavebank` | `181` | data-sort-value="181"\| `000000b5` |  |
| `trainLoop` | `Wavebank` | `222` | data-sort-value="222"\| `000000de` | A loop of a train clacking on the tracks. |
| `trainWhistle` | `Wavebank` | `219` | data-sort-value="219"\| `000000db` | A short train whistle. |
| `trashbear` | `Wavebank(1.4)` | `25` | data-sort-value="25"\| `00000019` |  |
| `trashbear_flute` | `Wavebank(1.4)` | `24` | data-sort-value="24"\| `00000018` |  |
| `trashcan` | `Wavebank` | `209` | data-sort-value="209"\| `000000d1` | The sound of wrappers rustling. Plays when the player rustles through the Garbage Cans around town. |
| `trashcanlid` | `Wavebank` | `210` | data-sort-value="210"\| `000000d2` | The sound of metal hinges creaking. Plays when the player hovers the Trash Can in their inventory menu and when the Bus door closes before it drives off. |
| `treasure_totem` | `Wavebank` | `416` | data-sort-value="416"\| `000001a0` |  |
| `treecrack` | `Wavebank` | `140` | data-sort-value="140"\| `0000008c` | A cracking noise. Plays right as the animation of a tree falling starts. |
| `treethud` | `Wavebank` | `139` | data-sort-value="139"\| `0000008b` |  |
| `turtle_pet` | `Wavebank` | `430` | data-sort-value="430"\| `000001ae` | A low chirping noise. The sound of the Turtle pet. |
| `UFO` | `Wavebank` | `226` | data-sort-value="226"\| `000000e2` | A sci-fi humming sound. Plays during the Strange Capsule nighttime event. |
| `wand` | `Wavebank` | `118` | data-sort-value="118"\| `00000076` |  |
| `warrior` | `Wavebank` | `206` | data-sort-value="206"\| `000000ce` |  |
| `wateringCan` | `Wavebank` | `153` | data-sort-value="153"\| `00000099` | Water pouring. Plays when the Watering Can is used. |
| `154` | data-sort-value="154"\| `0000009a` |  |  |  |
| `155` | data-sort-value="155"\| `0000009b` |  |  |  |
| `waterSlosh` | `Wavebank` | `257` | data-sort-value="257"\| `00000101` | A sloshing sound. Plays when both water and lava slosh. |
| `258` | data-sort-value="258"\| `00000102` |  |  |  |
| `259` | data-sort-value="259"\| `00000103` |  |  |  |
| `weed_cut` | `Wavebank` | `408` | data-sort-value="408"\| `00000198` | A rustling noise. Plays when weeds are cut down. |
| `whistle` | `Wavebank` | `298` | data-sort-value="298"\| `0000012a` | A sports referee-style whistle. Plays at the end of the fishing and target minigames at the Stardew Valley Fair. |
| `windstorm` | `Wavebank` | `426` | data-sort-value="426"\| `000001aa` | The sound of the wind howling and then a tree cracking. Plays during the windstorm nighttime event. |
| `woodchipper` | `Wavebank(1.4)` | `18` | data-sort-value="18"\| `00000012` |  |
| `woodchipper_occasional` | `Wavebank(1.4)` | `19` | data-sort-value="19"\| `00000013` |  |
| `woodWhack` | `Wavebank` | `311` | data-sort-value="311"\| `00000137` |  |
| `312` | data-sort-value="312"\| `00000138` |  |  |  |
| `woodyHit` | `Wavebank` | `77` | data-sort-value="77"\| `0000004d` |  |
| `yoba` | `Wavebank` | `202` | data-sort-value="202"\| `000000ca` | A sparkling noise. Plays when the Prismatic Butterfly is found and when Fairy Dust is used. |

## Manage audio in C#
### Playing sounds
The game mostly handles sound effects through the `Game1.sounds` field. This has low-level methods like `PlayLocal`, `PlayAll`, `GetVolumeForDistance`, etc.

However, you should rarely call `Game1.sounds` directly. Instead the game has four main methods for playing a sound effect:

| method | usage |
| --- | --- |
| `Game1.playSound` | Play a sound for the current player only, which isn't synced in multiplayer and isn't affected by location or distance. This is mainly used for UI and menu sounds. For example: 
```c#
// for UI elements (e.g. crafting an item)
Game1.playSound("crafting");
```
 |
| `GameLocation.localSound`; `GameLocation.playSound` | Play a sound for the current player (`localSound`) or all players (`playSound`) if they're in this location. You can optionally specify... * a tile position (which attenuates volume for each player based on their distance from the sound's source); * and/or pitch (from 0 to 2400 with intervals of 100 between every half step, where 1200 is the default pitch). For example: 
```c#
// play sound for the current player if they're anywhere on the farm
Game1.getFarm().localSound("doorCreak");

// play sound for the current player if they're on the farm near the mailbox (fading with distance)
Farm farm = Game1.getFarm();
farm.localSound("doorCreak", farm.GetMainMailboxPosition());

// play sound for all players on the farm near the mailbox (fading with distance), with a -200 pitch shift
Farm farm = Game1.getFarm();
farm.playSound("doorCreak", farm.GetMainMailboxPosition(), 1000);
```
 |
| `DelayedAction.playSoundAfterDelay` | Play a sound for the current or all players after a specified delay in milliseconds. You can optionally specify a location, tile position, and pitch (which works the same way as the `GameLocation` methods). You can call this method repeatedly to play multiple sounds (e.g. for a sequence of sounds with different delays). For example: 
```c#
// play sound for the current player after 1.5 seconds, no matter where they are
DelayedAction.playSoundAfterDelay("thunder_small", 1500);

// play sound for all players on the farm after 1.5 seconds, fading with distance from the mailbox
Farm farm = Game1.getFarm();
DelayedAction.playSoundAfterDelay("doorCreak", farm, farm.GetMainMailboxPosition());

// play sound for the current player only if they'rte on the farm after 1.5 seconds, fading with distance from the mailbox
Farm farm = Game1.getFarm();
DelayedAction.playSoundAfterDelay("doorCreak", farm, farm.GetMainMailboxPosition(), local: true);
```
 |

### Add/Edit Audio
To add or edit audio data in C#, you'll need to use SMAPI Content Events to edit a game asset. The expected type for the dictionary should be `IDictionary<string, AudioCueData>`.  

You can then add or edit entries in the loaded content, referencing the data format above.  

File paths can be specified by using `Path.Combine`.

Example:

```c#
List<string> paths = [
    Path.Combine(helper.DirectoryPath, "assets", fileName)
];
```

## See also
- The Soundtrack page for additional information about where many music tracks are used in-game.
- From the modding tutorial wiki:
  - [Content Patcher Snippets for Music](https://stardewmodding.wiki.gg/wiki/Content_Patcher_Snippets_for_1.6#Music)
  - [Adding Custom Music](https://stardewmodding.wiki.gg/wiki/Tutorial:_Adding_Custom_Music)

ru:Модификации:Аудио
zh:模组:音频
