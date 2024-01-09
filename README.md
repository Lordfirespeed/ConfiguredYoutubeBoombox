# Lordfirespeed-Configured_Youtube_Boombox (CYTBB)

[![GitHub Build Status](https://img.shields.io/github/actions/workflow/status/lordfirespeed/configuredyoutubeboombox/build.yml?style=for-the-badge&logo=github)](https://github.com/Lordfirespeed/ConfiguredYoutubeBoombox/actions/workflows/build.yml)
[![Thunderstore Version](https://img.shields.io/thunderstore/v/Lordfirespeed/Configured_Youtube_Boombox?style=for-the-badge&logo=thunderstore&logoColor=white)](https://thunderstore.io/c/lethal-company/p/Lordfirespeed/Configured_Youtube_Boombox/)
[![Thunderstore Downloads](https://img.shields.io/thunderstore/dt/Lordfirespeed/Configured_Youtube_Boombox?style=for-the-badge&logo=thunderstore&logoColor=white)](https://thunderstore.io/c/lethal-company/p/Lordfirespeed/Configured_Youtube_Boombox/)

A BepInEx plugin for Lethal Company that downloads audio from YouTube for use with 
[Steven's Custom Boombox Music](https://thunderstore.io/c/lethal-company/p/Steven/Custom_Boombox_Music/).

## Configuration

1. Create a subdirectory in your `BepInEx/plugins` directory, e.g. `myTracksMod`. 
2. Create a new JSON file called `configured-youtube-boombox-tracks.json` in the `BepInEx/plugins/myTracksMod` folder.
   Paste this template into the file:
   ```json
   {
     "$schema": "https://github.com/Lordfirespeed/ConfiguredYoutubeBoombox/raw/main/assets/configured-youtube-boombox-tracks.schema.json",
     "tracks": [
       {
         "youtubeVideoId": "yourVideoIdHere",
         "trackName": "yourTrackName"
       }
     ]
   }
   ```
3. Replace the `youtubeVideoId` and `trackName` accordingly.
4. Specify extra options such as `startTimestamp`, `endTimestamp`, `volumeScalar` (See 
   [here](https://github.com/Lordfirespeed/RefugeCompany/blob/1d0b80eaaaef9ca11c3aa018a73323ca7ab26178/assets/configured-youtube-boombox-tracks.json) for an example)
5. Add additional tracks by adding more items to the array
6. That's it! CYTBB will download the listed tracks from Youtube into the `BepInEx/Custom Songs/Boombox Tracks` folder
   when the game is opened.

## Tracklist Reference

### `youtubeVideoId`
Required `string`. Determines the source YouTube video. See [this gist](https://gist.github.com/jakebellacera/d81bbf12b99448188f183141e6696817)
for how to get YouTube video IDs.

### `trackName`
Required `string`. Will be formatted into the `.mp3` track's filename once downloaded, `cytbb-[trackname]-[youtubeId].mp3`

### `startTimestamp`
Optional `string` of form `h:m:s`. The downloaded audio will be cropped to start at this timestamp.
Seconds can be a decimal quantity and leading zeroes can be omitted. For example:

- ✅ `128.95` (128.95 seconds)
- ✅ `2:08.95` (2 minute 8.95 seconds)
- ✅ `1:35:20.12` (1 hour, 35 minutes and 20.12 seconds)
- ❌ `35:94` (can't have >=60 seconds when specifying minutes)
- ❌ `1:62:30` (can't have >=60 minutes when specifying hours)

### `endTimestamp`
Optional `string` of same form as `startTimestamp`. The downloaded audio will be cropped to start at this timestamp.

## FAQ / Common Issues

### Other players are hearing different songs to me!

Desync will occur if players have different tracklists. Make sure all players have the same custom boombox tracks
to ensure synchronicity.

### I've made `configured-youtube-boombox-tracks.json` but nothing is being downloaded!

CYTBB will **not** search for tracklists recursively. Your tracklist **must** be inside a folder in `BepInEx/plugins`.
For example:

- ✅ `BepInEx/plugins/portalRadio/configured-youtube-boombox-tracks.json`
- ❌ `BepInEx/plugins/portalRadio/youtube-boombox-tracks.json` (wrong filename)
- ✅ `BepInEx/plugins/pirateSongs/configured-youtube-boombox-tracks.json` 
- ❌ `BepInEx/plugins/pirateSongs/tracks/configured-youtube-boombox-tracks.json` (extra `tracks` folder)
- ❌ `BepInEx/plugins/configured-youtube-boombox-tracks.json` (missing subfolder)

### What is that `$schema` property in the template for?

The `$schema` property is a link to a [JSON Schema](https://json-schema.org/understanding-json-schema) which
provides helpful validation and autocomplete features in supported text editors. 

## Contributing

You will need to create a `ConfiguredYoutubeBoombox/ConfiguredYoutubeBoombox.csproj.user`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="Current" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
    <PropertyGroup>
        <LETHAL_COMPANY_DIR>F:/SteamLibrary/steamapps/common/Lethal Company</LETHAL_COMPANY_DIR>
        <TEST_PROFILE_DIR>$(APPDATA)/r2modmanPlus-local/LethalCompany/profiles/Test Configured YT Boombox</TEST_PROFILE_DIR>
        <NETCODE_PATCHER_DIR>$(SolutionDir)NetcodeWeaver</NETCODE_PATCHER_DIR>
    </PropertyGroup>

    <!-- Create your 'Test Profile' using your modman of choice before enabling this. 
    Enable by setting the Condition attribute to "true". *nix users should switch out `copy` for `cp`. -->
    <Target Name="CopyToTestProfile" AfterTargets="PostBuildEvent;NetcodeWeave" Condition="false">
        <MakeDir
                Directories="$(TEST_PROFILE_DIR)/BepInEx/plugins/Lordfirespeed-Configured_Youtube_Boombox"
                Condition="Exists('$(TEST_PROFILE_DIR)') And !Exists('$(TEST_PROFILE_DIR)/BepInEx/plugins/Lordfirespeed-Configured_Youtube_Boombox')"
        />
        <Exec Command="copy &quot;$(TargetPath)&quot; &quot;$(TEST_PROFILE_DIR)/BepInEx/plugins/Lordfirespeed-Configured_Youtube_Boombox/&quot;" />
    </Target>
</Project>
```
