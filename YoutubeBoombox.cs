﻿using BepInEx;
using HarmonyLib;
using YoutubeDLSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using LC_API.ServerAPI;
using GameNetcodeStuff;
using Unity.Netcode;
using System.Reflection.Emit;
using UnityEngine.Pool;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.ComponentModel;
using Newtonsoft.Json.Linq;
using BepInEx.Configuration;
using YoutubeDLSharp.Metadata;
using System.Security.Policy;
using Newtonsoft.Json;

namespace YoutubeBoombox
{
    public class InfoCache : IProgress<string>
    {
        public class Info
        {
            public string id { get; set; }
            public float duration { get; set; }
        }

        public string Id { get; set; }

        public InfoCache(string id)
        {
            Id = id;

            PlaylistCache.Add(id, new List<string>());
        }

        public static Dictionary<string, float> DurationCache = new Dictionary<string, float>();
        public static Dictionary<string, List<string>> PlaylistCache = new Dictionary<string, List<string>>();

        public void Report(string value)
        {
            try
            {
                Info json = JsonConvert.DeserializeObject<Info>(value);

                if (!DurationCache.ContainsKey(json.id))
                {
                    DurationCache.Add(json.id, json.duration);
                }

                PlaylistCache[Id].Add(json.id);

            } catch { }
        }
    }

    [BepInPlugin("steven4547466.YoutubeBoombox", "Youtube Boombox", "1.2.0")]
    [BepInDependency("LC_API")]
    public class YoutubeBoombox : BaseUnityPlugin
    {
        private static Harmony Harmony { get; set; }

        internal static string DirectoryPath { get; private set; }

        internal static string DownloadsPath { get; private set; }

        internal static YoutubeBoombox Singleton { get; private set; }

        public static YoutubeDL YoutubeDL { get; private set; } = new YoutubeDL();

        #region Config
        internal static ConfigEntry<int> MaxCachedDownloads { get; private set; }

        internal static ConfigEntry<bool> DeleteDownloadsOnRestart { get; private set; }

        internal static ConfigEntry<float> MaxSongDuration { get; private set; }

        internal static ConfigEntry<bool> EnableDebugLogs { get; private set; }
        #endregion

        internal static List<string> PathsThisSession { get; private set; } = new List<string>();

        public static void Log(object data, BepInEx.Logging.LogLevel level = BepInEx.Logging.LogLevel.Info)
        {
            Singleton.Logger.Log(level, data);
        }

        public static void DebugLog(object data, bool shouldLog = true)
        {
            if (shouldLog)
            {
                Singleton.Logger.LogDebug(data);
            }
        }

        async void Awake()
        {
            MaxCachedDownloads = Config.Bind(new ConfigDefinition("General", "Max Cached Downloads"), 10, new ConfigDescription("The maximum number of downloaded songs that can be saved before deleting.", new ConfigNumberClamper(1, 100)));
            DeleteDownloadsOnRestart = Config.Bind("General", "Delete Downloads On Restart", true, "Whether or not to delete downloads when your game starts again.");
            MaxSongDuration = Config.Bind("General", "Max Song Duration", 600f, "Maximum song duration in seconds. Any video longer than this will not be downloaded.");

            EnableDebugLogs = Config.Bind("Debugging", "Enable Debug Logs", false, "Whether or not to enable debug logs.");

            string oldDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), "Youtube-Boombox");

            if (Directory.Exists(oldDirectoryPath))
            {
                Directory.Delete(oldDirectoryPath, true);
            }

            DirectoryPath = Path.Combine(Paths.PluginPath, "steven4547466-YoutubeBoombox", "data");
            DownloadsPath = Path.Combine(DirectoryPath, "Downloads");
            Singleton = this;

            if (!Directory.Exists(DirectoryPath)) Directory.CreateDirectory(DirectoryPath);
            if (!Directory.Exists(DownloadsPath)) Directory.CreateDirectory(DownloadsPath);

            if (DeleteDownloadsOnRestart.Value)
            {
                foreach (string file in Directory.GetFiles(DownloadsPath))
                {
                    File.Delete(file);
                }
            }

            if (!Directory.GetFiles(DirectoryPath).Any(file => file.Contains("yt-dl"))) await Utils.DownloadYtDlp(DirectoryPath);
            if (!Directory.GetFiles(DirectoryPath).Any(file => file.Contains("ffmpeg"))) await Utils.DownloadFFmpeg(DirectoryPath);

            YoutubeDL.YoutubeDLPath = Directory.GetFiles(DirectoryPath).First(file => file.Contains("yt-dl"));
            YoutubeDL.FFmpegPath = Directory.GetFiles(DirectoryPath).First(file => file.Contains("ffmpeg"));

            YoutubeDL.OutputFolder = DownloadsPath;

            Harmony = new Harmony($"steven4547466.YoutubeBoombox-{DateTime.Now.Ticks}");

            Harmony.PatchAll();

            SetupNetworking();

            CommandHandler.CommandHandler.RegisterCommand("bbv", new List<string>() { "boomboxvolume" }, (string[] args) =>
            {
                if (args.Length > 0 && float.TryParse(args[0], out float volume))
                {
                    PlayerControllerB localController = StartOfRound.Instance.localPlayerController;
                    if (localController.currentlyHeldObjectServer is BoomboxItem boombox)
                    {
                        boombox.boomboxAudio.volume = volume / 100;
                    }
                    else
                    {
                        BoomboxItem closestBoombox = null;
                        float distanceSqr = float.MaxValue;

                        foreach (BoomboxItem boomboxItem in FindObjectsOfType<BoomboxItem>())
                        {
                            float dist = (boomboxItem.transform.position - localController.transform.position).sqrMagnitude;
                            if (dist < distanceSqr)
                            {
                                closestBoombox = boomboxItem;
                                distanceSqr = dist;
                            }
                        }

                        if (distanceSqr <= 255)
                        {
                            closestBoombox.boomboxAudio.volume = volume / 100;
                        }
                    }
                }
            });
        }

        private void SetupNetworking()
        {
            Networking.GetString += GetNetworkStringBroadcast;
            Networking.GetInt += GetNetworkIntBroadcast;
        }

        private void GetNetworkStringBroadcast(string data, string signature)
        {
            DebugLog($"GOT STRING BROADCAST {data}|{signature}", EnableDebugLogs.Value);
            if (signature == NetworkingSignatures.BOOMBOX_SIG)
            {
                string[] split = data.Split('|');
                string id = split[0];
                string type = split[1];
                ulong netId;

                if (!ulong.TryParse(split[2], out netId))
                {
                    Logger.LogError("Unable to find boombox id in data");

                    return;
                }

                BoomboxItem boombox = FindObjectsOfType<BoomboxItem>().FirstOrDefault(b => b.NetworkObjectId == netId);

                if (!boombox)
                {
                    Logger.LogError($"Unable to find boombox with net id: {netId}");

                    return;
                }

                BoomboxPatch.CurrentBoombox = boombox;

                BoomboxController.Download(boombox, id, type);
            }
            else if (signature == NetworkingSignatures.BOOMBOX_OFF_SIG)
            {
                string[] split = data.Split('|');

                ulong netId;

                if (!ulong.TryParse(split[0], out netId))
                {
                    Logger.LogError("Unable to find boombox id in data");

                    return;
                }

                bool pitchDown;
                if (!bool.TryParse(split[1], out pitchDown))
                {
                    Logger.LogError("Unable to find pitchDown in data");

                    return;
                }

                BoomboxItem boombox = FindObjectsOfType<BoomboxItem>().FirstOrDefault(b => b.NetworkObjectId == netId);

                if (!boombox)
                {
                    Logger.LogError($"Unable to find boombox with net id: {netId}");

                    return;
                }

                if (pitchDown)
                {
                    boombox.StartCoroutine(boombox.musicPitchDown());
                }
                else
                {
                    boombox.boomboxAudio.Stop();
                    boombox.boomboxAudio.PlayOneShot(boombox.stopAudios[UnityEngine.Random.Range(0, boombox.stopAudios.Length)]);
                }

                boombox.timesPlayedWithoutTurningOff = 0;

                boombox.isBeingUsed = false;
                boombox.isPlayingMusic = false;
                BoomboxController.ResetReadyClients(boombox);
            }
        }

        private void GetNetworkIntBroadcast(int data, string signature)
        {
            DebugLog($"GOT INT BROADCAST {data}|{signature}", EnableDebugLogs.Value);
            if (signature == NetworkingSignatures.BOOMBOX_READY_CLIENT_SIG)
            {
                ulong netId = (ulong)data;
                BoomboxItem boombox = FindObjectsOfType<BoomboxItem>().FirstOrDefault(b => b.NetworkObjectId == netId);

                if (!boombox)
                {
                    Logger.LogError($"Unable to find boombox with net id: {netId}");

                    return;
                }

                BoomboxController.AddReadyClient(boombox);
            }
        }

        public static (string, string) GetIdAndTypeFromUrl(string url)
        {
            string id;
            string type = "video";

            if (url.Contains("?v="))
            {
                id = url.Split(new[] { "?v=" }, StringSplitOptions.None)[1];
            }
            else if (url.Contains("youtu.be"))
            {
                if (url.EndsWith("/")) url = url.Substring(0, url.Length - 1);

                string[] split = url.Split('/');

                id = split[split.Length - 1];
            } 
            else if (url.Contains("?list="))
            {
                id = url.Split(new[] { "?list=" }, StringSplitOptions.None)[1].Split('&')[0];
                type = "playlist";
            }
            else
            {
                Singleton.Logger.LogError("Couldn't resolve URL.");
                return (null, null);
            }

            return (id, type);
        }

        public static void PlaySong(string url)
        {
            DebugLog($"Trying to play {url}", EnableDebugLogs.Value);
            if (BoomboxPatch.CurrentBoombox == null) return;

            DebugLog("Boombox found", EnableDebugLogs.Value);

            (string id, string type) = GetIdAndTypeFromUrl(url);

            BoomboxController.Download(BoomboxPatch.CurrentBoombox, id, type, true);
        }

        [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.DiscardItemOnClient))]
        class DropItemPatch
        {
            public static void Prefix(GrabbableObject __instance)
            {
                if (!__instance.IsOwner)
                {
                    return;
                }

                if (BoomboxPatch.ShowingGUI)
                {
                    BoomboxPatch.ShowingGUI = false;
                }
            }
        }

        [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.UseItemOnClient))]
        class PreventRpc
        {
            public static bool IsBoombox(GrabbableObject obj)
            {
                return obj is BoomboxItem;
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                List<CodeInstruction> newInstructions = new List<CodeInstruction>(instructions);
                int index = newInstructions.FindLastIndex(i => i.opcode == OpCodes.Ldarg_0) - 1;

                System.Reflection.Emit.Label skipLabel = generator.DefineLabel();

                newInstructions[index].labels.Add(skipLabel);

                index = newInstructions.FindLastIndex(i => i.opcode == OpCodes.Brfalse_S) + 1;

                newInstructions.InsertRange(index, new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PreventRpc), nameof(PreventRpc.IsBoombox))),
                    new CodeInstruction(OpCodes.Brtrue_S, skipLabel)
                });

                for (int z = 0; z < newInstructions.Count; z++) yield return newInstructions[z];
            }
        }

        [HarmonyPatch(typeof(BoomboxItem), nameof(BoomboxItem.PocketItem))]
        public class BoomboxPocketPatch
        {
            internal static bool Debounce = false;

            public static void Prefix()
            {
                Debounce = true;
            }
        }

        [HarmonyPatch(typeof(BoomboxItem), nameof(BoomboxItem.StartMusic))]
        public class BoomboxPatch
        {
            internal static bool ShowingGUI { get; set; } = false;
            internal static YoutubeBoomboxGUI ShownGUI { get; set; }
            internal static BoomboxItem CurrentBoombox { get; set; }

            public static bool Prefix(BoomboxItem __instance, bool startMusic, bool pitchDown)
            {

                if (BoomboxPocketPatch.Debounce)
                {
                    BoomboxPocketPatch.Debounce = false;

                    Networking.Broadcast($"{__instance.NetworkObjectId}|{pitchDown}", NetworkingSignatures.BOOMBOX_OFF_SIG);
                    DebugLog("Stopping boombox", EnableDebugLogs.Value);

                    if (pitchDown)
                    {
                        __instance.StartCoroutine(__instance.musicPitchDown());
                    }
                    else
                    {
                        __instance.boomboxAudio.Stop();
                        __instance.boomboxAudio.PlayOneShot(__instance.stopAudios[UnityEngine.Random.Range(0, __instance.stopAudios.Length)]);
                    }

                    __instance.timesPlayedWithoutTurningOff = 0;

                    __instance.isBeingUsed = false;
                    __instance.isPlayingMusic = false;
                    BoomboxController.ResetReadyClients(__instance);

                    CurrentBoombox = null;

                    return false;
                }

                __instance.isBeingUsed = startMusic;

                if (!startMusic)
                {
                    if (ShowingGUI)
                    {
                        DebugLog("Prevent dual open", EnableDebugLogs.Value);
                        return false;
                    }

                    BoomboxController.ResetReadyClients(__instance);

                    DebugLog("Opening boombox gui", EnableDebugLogs.Value);

                    CurrentBoombox = __instance;

                    GameObject guiObj = new GameObject("YoutubeBoomboxInput");
                    guiObj.hideFlags = HideFlags.HideAndDontSave;
                    ShownGUI = guiObj.AddComponent<YoutubeBoomboxGUI>();

                    ShowingGUI = true;
                }
                else if (__instance.isPlayingMusic)
                {
                    Networking.Broadcast($"{__instance.NetworkObjectId}|{pitchDown}", NetworkingSignatures.BOOMBOX_OFF_SIG);
                    DebugLog("Stopping boombox", EnableDebugLogs.Value);

                    if (pitchDown)
                    {
                        __instance.StartCoroutine(__instance.musicPitchDown());
                    }
                    else
                    {
                        __instance.boomboxAudio.Stop();
                        __instance.boomboxAudio.PlayOneShot(__instance.stopAudios[UnityEngine.Random.Range(0, __instance.stopAudios.Length)]);
                    }

                    __instance.timesPlayedWithoutTurningOff = 0;

                    __instance.isBeingUsed = false;
                    __instance.isPlayingMusic = false;
                    BoomboxController.ResetReadyClients(__instance);

                    CurrentBoombox = null;
                }

                return false;
            }
        }
    }
}
