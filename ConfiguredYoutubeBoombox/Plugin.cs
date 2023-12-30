using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Newtonsoft.Json;
using YoutubeDLSharp;

namespace ConfiguredYoutubeBoombox;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("com.steven.lethalcompany.boomboxmusic", "1.4.0")]
public class Plugin : BaseUnityPlugin
{
    #region Config
    internal static ConfigEntry<float> MaxSongDuration { get; private set; } = null!;
    #endregion

    internal new static ManualLogSource? Logger;
    internal static string? PluginDataPath;
    internal static string? DownloadsPath;
    internal static YoutubeDL YoutubeDL { get; } = new();
    internal static JsonSerializer TrackListSerializer { get; } = new();

    public Plugin()
    {
        Logger = base.Logger;
    }

    private async void Awake()
    {
        MaxSongDuration = Config.Bind(
            new ConfigDefinition("General", "Max Song Duration"),
            600f,
             new ConfigDescription("Maximum song duration in seconds. Any video longer than this will not be downloaded.")
        );

        await InitializeToolsAndDirectories();
        await DownloadConfiguredTracks();
    }

    private async Task InitializeToolsAndDirectories()
    {
        PluginDataPath = Path.Combine(Path.GetDirectoryName(Info.Location)!, "configured-youtube-boombox-data");
        DownloadsPath = Path.Combine(Paths.BepInExRootPath, "Custom Songs", "Boombox Music");

        if (!Directory.Exists(PluginDataPath)) Directory.CreateDirectory(PluginDataPath);
        if (!Directory.Exists(DownloadsPath)) Directory.CreateDirectory(DownloadsPath);

        Func<Task> ensureYtDlp = async () =>
        {
            if (Directory.GetFiles(PluginDataPath).Any(file => file.Contains("yt-dl"))) return;
            await Utils.DownloadYtDlp(PluginDataPath);
        };
        Func<Task> ensureFfMpeg = async () =>
        {
            if (Directory.GetFiles(PluginDataPath).Any(file => file.Contains("ffmpeg"))) return;
            await Utils.DownloadFFmpeg(PluginDataPath);
        };

        await Task.WhenAll([
            ensureYtDlp(), 
            ensureFfMpeg()
        ]);

        YoutubeDL.YoutubeDLPath = Directory.GetFiles(PluginDataPath).First(file => file.Contains("yt-dl"));
        YoutubeDL.FFmpegPath = Directory.GetFiles(PluginDataPath).First(file => file.Contains("ffmpeg"));
        YoutubeDL.OutputFolder = DownloadsPath;
        YoutubeDL.OutputFileTemplate = "%(id)s.%(ext)s";
    }

    private IEnumerable<string> DiscoverConfiguredTrackListFiles()
    {
        return Directory.GetDirectories(Paths.PluginPath)
            .Select(pluginDirectory => Path.Join(pluginDirectory, "configured-youtube-boombox-tracks.json"))
            .Where(File.Exists);
    }

    private ConfiguredTrack[] DeserializeConfiguredTrackListFile(string trackListFilePath)
    {
        using var streamReader = new StreamReader(trackListFilePath);
        using var reader = new JsonTextReader(streamReader);
        var trackListFile = TrackListSerializer.Deserialize<ConfiguredTrackListFile>(reader);
        if (trackListFile?.Tracks != null) return trackListFile.Tracks;
        Logger?.LogWarning($"Failed to deserialize any tracks from {trackListFilePath}.");
        return Array.Empty<ConfiguredTrack>();
    } 

    private IEnumerable<ConfiguredTrack> DiscoverConfiguredTracks()
    {
        return DiscoverConfiguredTrackListFiles()
            .SelectMany(DeserializeConfiguredTrackListFile);
    }

    private async Task DownloadConfiguredTracks()
    {
        await Task.WhenAll(
            DiscoverConfiguredTracks().Select(TrackDownloader.DownloadTrack)
        );
    }
}