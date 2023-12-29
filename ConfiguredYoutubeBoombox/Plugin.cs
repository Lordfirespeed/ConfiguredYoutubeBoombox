using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using ConfiguredYoutubeBoombox.Providers;
using HarmonyLib;
using Newtonsoft.Json;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using YoutubeDLSharp;

namespace ConfiguredYoutubeBoombox;

public class InfoCache : IProgress<string>
{
    public static readonly Dictionary<string, float> DurationCache = new();
    public static readonly Dictionary<string, List<string>> PlaylistCache = new();

    public InfoCache(string id)
    {
        Id = id;

        PlaylistCache.Add(id, new List<string>());
    }

    public string Id { get; set; }

    public void Report(string value)
    {
        try
        {
            var json = JsonConvert.DeserializeObject<Info>(value);

            if (!DurationCache.ContainsKey(json.id)) DurationCache.Add(json.id, json.duration);

            PlaylistCache[Id].Add(json.id);
        }
        catch
        {
        }
    }

    public class Info
    {
        public string id { get; set; }
        public float duration { get; set; }
    }
}

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("LC_API")]
public class Plugin : BaseUnityPlugin
{
    private static Harmony Harmony { get; set; }

    internal static string PluginDataPath { get; private set; }

    internal static string DownloadsPath { get; private set; }

    internal static Plugin Singleton { get; private set; }

    public static YoutubeDL YoutubeDL { get; } = new();

    internal static List<string> PathsThisSession { get; private set; } = new();

    internal static List<Provider> Providers { get; } = new();

    public Plugin()
    {
        Singleton = this;
    }

    private async void Awake()
    {
        MaxCachedDownloads = Config.Bind(
            new ConfigDefinition("General", "Max Cached Downloads"),
            10,
            new ConfigDescription(
                "The maximum number of downloaded songs that can be saved before deleting.",
                new ConfigNumberClamper(1, 100)
            )
        );
        DeleteDownloadsOnRestart = Config.Bind(
            new ConfigDefinition("General", "Re-download on restart"),
            false,
            new ConfigDescription("Whether or not to delete downloads when your game starts again.")
        );
        MaxSongDuration = Config.Bind(
            new ConfigDefinition("General", "Max Song Duration"),
            600f,
             new ConfigDescription("Maximum song duration in seconds. Any video longer than this will not be downloaded.")
        );

        var oldDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), "Youtube-Boombox");

        if (Directory.Exists(oldDirectoryPath)) Directory.Delete(oldDirectoryPath, true);

        PluginDataPath = Path.Combine(Paths.PluginPath, "Lordfirespeed-Configured_Youtube_Boombox", "data");
        DownloadsPath = Path.Combine(Paths.BepInExRootPath, "Custom Songs", "Boombox Music");

        if (!Directory.Exists(PluginDataPath)) Directory.CreateDirectory(PluginDataPath);
        if (!Directory.Exists(DownloadsPath)) Directory.CreateDirectory(DownloadsPath);

        if (DeleteDownloadsOnRestart.Value)
            foreach (var file in Directory.GetFiles(DownloadsPath))
                File.Delete(file);

        if (!Directory.GetFiles(PluginDataPath).Any(file => file.Contains("yt-dl")))
            await Utils.DownloadYtDlp(PluginDataPath);
        if (!Directory.GetFiles(PluginDataPath).Any(file => file.Contains("ffmpeg")))
            await Utils.DownloadFFmpeg(PluginDataPath);

        YoutubeDL.YoutubeDLPath = Directory.GetFiles(PluginDataPath).First(file => file.Contains("yt-dl"));
        YoutubeDL.FFmpegPath = Directory.GetFiles(PluginDataPath).First(file => file.Contains("ffmpeg"));

        YoutubeDL.OutputFolder = DownloadsPath;

        YoutubeDL.OutputFileTemplate = "%(id)s.%(ext)s";

        Harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);

        Harmony.PatchAll();
        SetupNetworking();

        var method = new StackTrace().GetFrame(0).GetMethod();
        var assembly = method.ReflectedType!.Assembly;

        AccessTools.GetTypesFromAssembly(assembly)
            .Where(t => t.IsSubclassOf(typeof(Provider)))
            .Select(t => (Activator.CreateInstance(t) as Provider)!)
            .Do(provider => Providers.Add(provider));
    }

    public static void LogInfo(object data)
    {
        Singleton.Logger.LogInfo(data);
    }

    public static void LogError(object data)
    {
        Singleton.Logger.LogError(data);
    }

    public static void LogDebug(object data)
    {
        Singleton.Logger.LogDebug(data);
    }

    private void SetupNetworking()
    {
        var types = Assembly.GetExecutingAssembly().GetTypes();
        foreach (var type in types)
        {
            var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (var method in methods)
            {
                var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                if (attributes.Length > 0) method.Invoke(null, null);
            }
        }
    }

    [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.Start))]
    private class GameNetworkManagerPatch
    {
        public static void Postfix(GameNetworkManager __instance)
        {
            __instance.GetComponent<NetworkManager>().NetworkConfig.Prefabs.Prefabs
                .Where(networkPrefab => networkPrefab.Prefab.GetComponent<BoomboxItem>() != null)
                .Do(networkPrefab => { networkPrefab.Prefab.AddComponent<BoomboxController>(); });
        }
    }

    #region Config

    internal static ConfigEntry<int> MaxCachedDownloads { get; private set; }

    internal static ConfigEntry<bool> DeleteDownloadsOnRestart { get; private set; }

    internal static ConfigEntry<float> MaxSongDuration { get; private set; }

    internal static ConfigEntry<bool> EnableDebugLogs { get; private set; }

    internal static ConfigEntry<Key> CustomBoomboxButton { get; private set; }

    #endregion
}