# Lordfirespeed-Configured_Youtube_Boombox

[![GitHub Build Status](https://img.shields.io/github/actions/workflow/status/lordfirespeed/configuredyoutubeboombox/build.yml?style=for-the-badge&logo=github)](https://github.com/Lordfirespeed/ConfiguredYoutubeBoombox/actions/workflows/build.yml)
[![Thunderstore Version](https://img.shields.io/thunderstore/v/Lordfirespeed/Configured_Youtube_Boombox?style=for-the-badge&logo=thunderstore&logoColor=white)](https://thunderstore.io/c/lethal-company/p/Lordfirespeed/Configured_Youtube_Boombox/)
[![Thunderstore Downloads](https://img.shields.io/thunderstore/dt/Lordfirespeed/Configured_Youtube_Boombox?style=for-the-badge&logo=thunderstore&logoColor=white)](https://thunderstore.io/c/lethal-company/p/Lordfirespeed/Configured_Youtube_Boombox/)

## Usage

## Features

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
