﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ConfiguredYoutubeBoombox.Providers;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using YoutubeDLSharp.Options;
using static ConfiguredYoutubeBoombox.Plugin;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ConfiguredYoutubeBoombox
{
    public class BoomboxController : NetworkBehaviour
    {
        private static readonly GUIStyle style = new GUIStyle
            { alignment = TextAnchor.MiddleCenter, normal = new GUIStyleState { textColor = Color.white } };

        private BoomboxItem Boombox { get; set; }

        private YoutubeBoomboxGUI GUI { get; set; }

        private ParsedUri CurrentUri { get; set; }

        private string CurrentId { get; set; }

        private string CurrentUrl { get; set; }

        private bool IsPlaylist { get; set; }

        private int PlaylistCurrentIndex { get; set; }

        private List<ulong> ReadyClients { get; } = new List<ulong>();

        private NetworkList<ulong> ClientsNeededToBeReady { get; } = new NetworkList<ulong>();

        public void Awake()
        {
            Boombox = GetComponent<BoomboxItem>();
        }

        public void Start()
        {
            DebugLog($"Boombox started client: {IsClient} host: {IsHost} server: {IsServer}", EnableDebugLogs.Value);
            IHaveTheModServerRpc();
        }

        public void Update()
        {
            if (StartOfRound.Instance != null
                && StartOfRound.Instance.localPlayerController.currentlyHeldObjectServer == Boombox
                && Keyboard.current[CustomBoomboxButton.Value].wasPressedThisFrame && !IsGUIShowing())
            {
                DebugLog("Boombox button pressed!", EnableDebugLogs.Value);

                GUI = gameObject.AddComponent<YoutubeBoomboxGUI>();

                DisableControls();
            }
        }

        public void OnGUI()
        {
            if (StartOfRound.Instance != null && StartOfRound.Instance.localPlayerController != null
                                              && StartOfRound.Instance.localPlayerController
                                                  .currentlyHeldObjectServer == Boombox)
            {
                const int width = 115;
                const int height = 20;
                UnityEngine.GUI.Box(new Rect(Screen.width - width, Screen.height - height, width, height),
                    string.Empty);
                UnityEngine.GUI.Label(new Rect(Screen.width - width, Screen.height - height, width, height),
                    $"Open YT GUI: [{CustomBoomboxButton.Value}]", style);
            }
        }

        private void DisableControls()
        {
            StartOfRound.Instance.localPlayerController.playerActions.Disable();
        }

        private void EnableControls()
        {
            StartOfRound.Instance.localPlayerController.playerActions.Enable();
        }

        [ServerRpc(RequireOwnership = false)]
        public void IHaveTheModServerRpc(ServerRpcParams serverRpcParams = default)
        {
            DebugLog("Regsitering mod server rpc called", EnableDebugLogs.Value);

            if (!IsServer) return;

            var sender = serverRpcParams.Receive.SenderClientId;

            if (!ClientsNeededToBeReady.Contains(sender))
            {
                DebugLog($"{sender} has registered having this mod", EnableDebugLogs.Value);

                ClientsNeededToBeReady.Add(sender);
            }
        }

        public void ClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;

            ClientsNeededToBeReady.Remove(clientId);
        }

        [ServerRpc(RequireOwnership = false)]
        public void DownloadServerRpc(string originalUrl, string id, string downloadUrl, UriType uriType)
        {
            DebugLog("Download server rpc received, sending to all", EnableDebugLogs.Value);
            DownloadClientRpc(originalUrl, id, downloadUrl, uriType);
        }

        [ClientRpc]
        public void DownloadClientRpc(string originalUrl, string id, string downloadUrl, UriType uriType)
        {
            DebugLog("Download request received on client, processing.", EnableDebugLogs.Value);
            ProcessRequest(new ParsedUri(new Uri(originalUrl), id, downloadUrl, uriType));
        }

        public void Download(ParsedUri parsedUri)
        {
            DebugLog("Download called, calling everywhere", EnableDebugLogs.Value);
            DownloadServerRpc(parsedUri.Uri.OriginalString, parsedUri.Id, parsedUri.DownloadUrl, parsedUri.UriType);
        }

        [ServerRpc(RequireOwnership = false)]
        public void IAmReadyServerRpc(ServerRpcParams serverRpcParams = default)
        {
            if (!IsServer) return;

            var sender = serverRpcParams.Receive.SenderClientId;

            DebugLog($"Ready called from {sender}", EnableDebugLogs.Value);

            //ClientRpcParams clientRpcParams = new ClientRpcParams
            //{
            //    Send = new ClientRpcSendParams
            //    {
            //        TargetClientIds = NetworkManager.Singleton.ConnectedClientsIds.Where(id => id != sender).ToArray()
            //    }
            //};

            AddReadyClientRpc(sender);
        }

        [ClientRpc]
        public void AddReadyClientRpc(ulong readyId)
        {
            DebugLog($"READY CLIENT CALLED already ready?: {ReadyClients.Contains(readyId)}", EnableDebugLogs.Value);
            if (ReadyClients.Contains(readyId)) return;

            ReadyClients.Add(readyId);

            DebugLog($"READY CLIENT {ReadyClients.Count}/{ClientsNeededToBeReady.Count}", EnableDebugLogs.Value);

            if (ReadyClients.Count >= ClientsNeededToBeReady.Count)
            {
                DebugLog("Everyone ready, starting tunes!", EnableDebugLogs.Value);
                ReadyClients.Clear();
                Boombox.boomboxAudio.loop = true;
                Boombox.boomboxAudio.pitch = 1;
                Boombox.isBeingUsed = true;
                Boombox.isPlayingMusic = true;
                Boombox.boomboxAudio.Play();

                if (IsPlaylist)
                {
                    DebugLog("Currently playing playlist, starting playlist routine.", EnableDebugLogs.Value);
                    Boombox.boomboxAudio.loop = false;
                    Boombox.StartCoroutine(PlaylistCoroutine());
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void StopMusicServerRpc(bool pitchDown)
        {
            StopMusicClientRpc(pitchDown);
        }

        [ClientRpc]
        public void StopMusicClientRpc(bool pitchDown)
        {
            if (pitchDown)
            {
                Boombox.StartCoroutine(Boombox.musicPitchDown());
            }
            else
            {
                Boombox.boomboxAudio.Stop();
                Boombox.boomboxAudio.PlayOneShot(Boombox.stopAudios[Random.Range(0, Boombox.stopAudios.Length)]);
            }

            Boombox.timesPlayedWithoutTurningOff = 0;

            Boombox.isBeingUsed = false;
            Boombox.isPlayingMusic = false;
            ResetReadyClients();
        }

        public void ResetReadyClients()
        {
            ReadyClients.Clear();
        }

        public void PlaySong(string url)
        {
            DebugLog($"Trying to play {url}", EnableDebugLogs.Value);

            DebugLog("Boombox found", EnableDebugLogs.Value);

            var uri = new Uri(url);

            var parsedUri = Plugin.Providers.First(p => p.Hosts.Contains(uri.Host)).ParseUri(uri);

            Download(parsedUri);
        }

        // Doesn't really destroy the GUI, the GUI destroys itself, just gotta set it to null.
        public void DestroyGUI()
        {
            GUI = null;

            EnableControls();
        }

        public bool IsGUIShowing()
        {
            return GUI != null;
        }

        public IEnumerator LoadSongCoroutine(string path)
        {
            DebugLog($"Loading song at {path}.", EnableDebugLogs.Value);

            if (PathsThisSession.Contains(path)) PathsThisSession.Remove(path);

            PathsThisSession.Insert(0, path);

            if (PathsThisSession.Count > MaxCachedDownloads.Value)
            {
                File.Delete(PathsThisSession[PathsThisSession.Count - 1]);
                PathsThisSession.RemoveAt(PathsThisSession.Count - 1);
            }

            var url = string.Format("file://{0}", path);
            var www = new WWW(url);
            yield return www;

            DebugLog($"Successfully loaded song at {path}.", EnableDebugLogs.Value);

            Boombox.boomboxAudio.clip = www.GetAudioClip(false, false);

            DebugLog("BOOMBOX READY!", EnableDebugLogs.Value);

            IAmReadyServerRpc();
        }

        public void IncrementPlaylistIndex()
        {
            DebugLog("Incrementing playlist index.", EnableDebugLogs.Value);

            Boombox.boomboxAudio.Stop();

            PlaylistCurrentIndex++;

            ReadyClients.Clear();

            if (InfoCache.PlaylistCache.TryGetValue(CurrentId, out var videoIds))
            {
                if (PlaylistCurrentIndex < videoIds.Count)
                {
                    var id = videoIds[PlaylistCurrentIndex];
                    var url = $"https://youtube.com/watch?v={id}";

                    DebugLog("Downloading next playlist song.", EnableDebugLogs.Value);

                    DownloadSong(id, url);
                }
                else
                {
                    DebugLog("Playlist complete!", EnableDebugLogs.Value);
                }
            }
            else
            {
                DebugLog("Playlist video ids not found! Cannot proceed!", EnableDebugLogs.Value);
                IAmReadyServerRpc();
            }
        }

        public IEnumerator PlaylistCoroutine()
        {
            PrepareNextSongInPlaylist();
            while (Boombox.boomboxAudio.isPlaying) yield return new WaitForSeconds(1);
            IncrementPlaylistIndex();
        }

        public void DownloadCurrentVideo()
        {
            DebugLog($"Downloading {CurrentUrl} ({CurrentId})", EnableDebugLogs.Value);
            DownloadSong(CurrentId, CurrentUrl);
        }

        public async void DownloadSong(string id, string url)
        {
            DebugLog($"Downloading song {url} ({id})", EnableDebugLogs.Value);

            var newPath = Path.Combine(DownloadsPath, $"{id}.mp3");

            if (id == null || url == null || newPath == null)
            {
                DebugLog($"Something is null. {id == null} {url == null} {newPath == null}", EnableDebugLogs.Value);

                return;
            }

            if (File.Exists(newPath))
            {
                DebugLog("File exists. Reusing.", EnableDebugLogs.Value);
                Boombox.StartCoroutine(LoadSongCoroutine(newPath));

                return;
            }

            if (InfoCache.DurationCache.TryGetValue(id, out var duration))
            {
                if (duration > MaxSongDuration.Value)
                {
                    DebugLog("Song too long. Preventing download.", EnableDebugLogs.Value);
                    IAmReadyServerRpc();

                    return;
                }
            }
            else
            {
                try
                {
                    DebugLog("Downloading song duration data.", EnableDebugLogs.Value);
                    var videoDataResult = await YoutubeDL.RunVideoDataFetch(url);
                    DebugLog("Downloaded song duration data.", EnableDebugLogs.Value);

                    if (videoDataResult.Success && videoDataResult.Data.Duration != null)
                    {
                        InfoCache.DurationCache.Add(id, (float)videoDataResult.Data.Duration);
                        // Skip downloading videos that are too long
                        if (videoDataResult.Data.Duration > MaxSongDuration.Value)
                        {
                            DebugLog("Song too long. Preventing download.", EnableDebugLogs.Value);
                            IAmReadyServerRpc();

                            return;
                        }
                    }
                    else
                    {
                        DebugLog("Couldn't get song data, skipping.", EnableDebugLogs.Value);
                        IAmReadyServerRpc();

                        return;
                    }
                }
                catch (Exception e)
                {
                    DebugLog("Error while downloading song data.", EnableDebugLogs.Value);
                    DebugLog(e, EnableDebugLogs.Value);
                    IAmReadyServerRpc();

                    return;
                }
            }

            DebugLog($"Trying to download {url}.", EnableDebugLogs.Value);

            var res = await YoutubeDL.RunAudioDownload(url, AudioConversionFormat.Mp3);

            DebugLog("Downloaded.", EnableDebugLogs.Value);

            if (res.Success)
            {
                File.Move(res.Data, newPath);

                DebugLog($"Song {id} downloaded successfully.", EnableDebugLogs.Value);
                Boombox.StartCoroutine(LoadSongCoroutine(newPath));
            }
            else
            {
                DebugLog($"Failed to download song {id}.", EnableDebugLogs.Value);
                IAmReadyServerRpc();
            }
        }

        public void PrepareNextSongInPlaylist()
        {
            DebugLog("Preparing next song in playlist", EnableDebugLogs.Value);
            if (InfoCache.PlaylistCache.TryGetValue(CurrentId, out var videoIds))
            {
                if (PlaylistCurrentIndex + 1 < videoIds.Count)
                {
                    var id = videoIds[PlaylistCurrentIndex + 1];
                    var url = $"https://youtube.com/watch?v={id}";

                    DebugLog($"Preparing {url} ({id})", EnableDebugLogs.Value);

                    PrepareSong(id, url);
                }
                else
                {
                    DebugLog("Playlist complete.", EnableDebugLogs.Value);
                }
            }
            else
            {
                DebugLog("Couldn't find playlist ids!", EnableDebugLogs.Value);
            }
        }

        public async void PrepareSong(string id, string url)
        {
            DebugLog($"Preparing next song {id}", EnableDebugLogs.Value);

            var newPath = Path.Combine(DownloadsPath, $"{id}.mp3");

            if (File.Exists(newPath))
            {
                DebugLog("Already exists, reusing.", EnableDebugLogs.Value);
                return;
            }

            if (InfoCache.DurationCache.TryGetValue(id, out var duration))
            {
                if (duration > MaxSongDuration.Value)
                {
                    DebugLog("Song too long. Preventing download.", EnableDebugLogs.Value);
                    return;
                }
            }
            else
            {
                var videoDataResult = await YoutubeDL.RunVideoDataFetch(url);

                if (videoDataResult.Success && videoDataResult.Data.Duration != null)
                {
                    InfoCache.DurationCache.Add(id, (float)videoDataResult.Data.Duration);
                    // Skip preparing videos that are too long
                    if (videoDataResult.Data.Duration > MaxSongDuration.Value)
                    {
                        DebugLog("Song too long. Preventing download.", EnableDebugLogs.Value);
                        return;
                    }
                }
                else
                {
                    DebugLog("Couldn't get song length. Skipping.", EnableDebugLogs.Value);
                    return;
                }
            }

            var res = await YoutubeDL.RunAudioDownload(url, AudioConversionFormat.Mp3);

            if (res.Success)
            {
                File.Move(res.Data, newPath);

                DebugLog($"Prepared {id} successfully", EnableDebugLogs.Value);
            }
            else
            {
                DebugLog($"Downloading {id} failed!", EnableDebugLogs.Value);
            }
        }

        public async void DownloadCurrentPlaylist()
        {
            DebugLog($"Downloading playlist from {CurrentUrl} ({CurrentId})", EnableDebugLogs.Value);

            PlaylistCurrentIndex = 0;
            if (!InfoCache.PlaylistCache.TryGetValue(CurrentId, out var videoIds))
            {
                DebugLog("Playlist not found in cache, downloading all ids.", EnableDebugLogs.Value);

                var playlistResult = await YoutubeDL.RunVideoPlaylistDownload(CurrentUrl, 1, null, null,
                    "bestvideo+bestaudio/best",
                    VideoRecodeFormat.None, default, null, new InfoCache(CurrentId),
                    new OptionSet
                    {
                        FlatPlaylist = true,
                        DumpJson = true
                    });

                if (!playlistResult.Success)
                {
                    DebugLog("Failed to download playlist ids. Unable to proceed.", EnableDebugLogs.Value);
                    IAmReadyServerRpc();

                    return;
                }

                videoIds = InfoCache.PlaylistCache[CurrentId];
            }

            if (videoIds.Count == 0)
            {
                DebugLog("Playlist video ids empty...", EnableDebugLogs.Value);
                IAmReadyServerRpc();

                return;
            }

            var id = videoIds[0];
            var url = $"https://youtube.com/watch?v={id}";

            DebugLog($"First playlist song found: {url} ({id})... Downloading.", EnableDebugLogs.Value);

            DownloadSong(id, url);
        }

        public void ProcessRequest(ParsedUri parsedUri)
        {
            var url = parsedUri.DownloadUrl;

            CurrentUri = parsedUri;
            CurrentUrl = url;
            IsPlaylist = parsedUri.UriType == UriType.Playlist;
            CurrentId = parsedUri.Id;

            DebugLog($"Processing request for {CurrentId} isPlaylist?: {IsPlaylist}", EnableDebugLogs.Value);

            if (!IsPlaylist)
                DownloadCurrentVideo();
            else
                DownloadCurrentPlaylist();
        }
    }

    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnClientDisconnect))]
    internal static class Left
    {
        private static void Prefix(StartOfRound __instance, ulong clientId)
        {
            if (!__instance.ClientPlayerList.ContainsKey(clientId)) return;

            foreach (var controller in Object.FindObjectsOfType<BoomboxController>())
                controller.ClientDisconnected(clientId);
        }
    }
}