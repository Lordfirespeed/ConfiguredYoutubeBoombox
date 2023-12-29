using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ConfiguredYoutubeBoombox.Providers;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using YoutubeDLSharp.Options;
using static ConfiguredYoutubeBoombox.Plugin;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ConfiguredYoutubeBoombox;

public class BoomboxController : NetworkBehaviour
{
    private static readonly GUIStyle style = new()
        { alignment = TextAnchor.MiddleCenter, normal = new GUIStyleState { textColor = Color.white } };

    private BoomboxItem Boombox { get; set; }

    private ParsedUri CurrentUri { get; set; }

    private string CurrentId { get; set; }

    private string CurrentUrl { get; set; }

    private bool IsPlaylist { get; set; }

    private int PlaylistCurrentIndex { get; set; }

    private List<ulong> ReadyClients { get; } = new();

    private NetworkList<ulong> ClientsNeededToBeReady { get; } = new();

    public void Awake()
    {
        Boombox = GetComponent<BoomboxItem>();
    }

    public void Start()
    {
        LogDebug($"Boombox started client: {IsClient} host: {IsHost} server: {IsServer}");
        IHaveTheModServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    public void IHaveTheModServerRpc(ServerRpcParams serverRpcParams = default)
    {
        LogDebug("Registering mod server rpc called");

        if (!IsServer) return;

        var sender = serverRpcParams.Receive.SenderClientId;

        if (ClientsNeededToBeReady.Contains(sender)) return;

        LogDebug($"{sender} has registered having this mod");
        ClientsNeededToBeReady.Add(sender);
    }

    public void ClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        ClientsNeededToBeReady.Remove(clientId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void DownloadServerRpc(string originalUrl, string id, string downloadUrl, UriType uriType)
    {
        LogDebug("Download server rpc received, sending to all");
        DownloadClientRpc(originalUrl, id, downloadUrl, uriType);
    }

    [ClientRpc]
    public void DownloadClientRpc(string originalUrl, string id, string downloadUrl, UriType uriType)
    {
        LogDebug("Download request received on client, processing.");
        ProcessRequest(new ParsedUri(new Uri(originalUrl), id, downloadUrl, uriType));
    }

    public void Download(ParsedUri parsedUri)
    {
        LogDebug("Download called, calling everywhere");
        DownloadServerRpc(parsedUri.Uri.OriginalString, parsedUri.Id, parsedUri.DownloadUrl, parsedUri.UriType);
    }

    [ServerRpc(RequireOwnership = false)]
    public void IAmReadyServerRpc(ServerRpcParams serverRpcParams = default)
    {
        if (!IsServer) return;

        var sender = serverRpcParams.Receive.SenderClientId;

        LogDebug($"Ready called from {sender}");
        AddReadyClientRpc(sender);
    }

    [ClientRpc]
    public void AddReadyClientRpc(ulong readyId)
    {
        LogDebug($"READY CLIENT CALLED already ready?: {ReadyClients.Contains(readyId)}");
        if (ReadyClients.Contains(readyId)) return;

        ReadyClients.Add(readyId);

        LogDebug($"READY CLIENT {ReadyClients.Count}/{ClientsNeededToBeReady.Count}");

        if (ReadyClients.Count >= ClientsNeededToBeReady.Count)
        {
            LogDebug("Everyone ready, starting tunes!");
            ReadyClients.Clear();
            Boombox.boomboxAudio.loop = true;
            Boombox.boomboxAudio.pitch = 1;
            Boombox.isBeingUsed = true;
            Boombox.isPlayingMusic = true;
            Boombox.boomboxAudio.Play();

            if (IsPlaylist)
            {
                LogDebug("Currently playing playlist, starting playlist routine.");
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
        LogDebug($"Trying to play {url}");

        LogDebug("Boombox found");

        var uri = new Uri(url);

        var parsedUri = Plugin.Providers.First(p => p.Hosts.Contains(uri.Host)).ParseUri(uri);

        Download(parsedUri);
    }

    public IEnumerator LoadSongCoroutine(string path)
    {
        LogDebug($"Loading song at {path}.");

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

        LogDebug($"Successfully loaded song at {path}.");

        Boombox.boomboxAudio.clip = www.GetAudioClip(false, false);

        LogDebug("BOOMBOX READY!");

        IAmReadyServerRpc();
    }

    public void IncrementPlaylistIndex()
    {
        LogDebug("Incrementing playlist index.");

        Boombox.boomboxAudio.Stop();

        PlaylistCurrentIndex++;

        ReadyClients.Clear();

        if (InfoCache.PlaylistCache.TryGetValue(CurrentId, out var videoIds))
        {
            if (PlaylistCurrentIndex < videoIds.Count)
            {
                var id = videoIds[PlaylistCurrentIndex];
                var url = $"https://youtube.com/watch?v={id}";

                LogDebug("Downloading next playlist song.");

                DownloadSong(id, url);
            }
            else
            {
                LogDebug("Playlist complete!");
            }
        }
        else
        {
            LogDebug("Playlist video ids not found! Cannot proceed!");
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
        LogDebug($"Downloading {CurrentUrl} ({CurrentId})");
        DownloadSong(CurrentId, CurrentUrl);
    }

    public async void DownloadSong(string id, string url)
    {
        LogDebug($"Downloading song {url} ({id})");

        var newPath = Path.Combine(DownloadsPath, $"{id}.mp3");

        if (id == null || url == null || newPath == null)
        {
            LogDebug($"Something is null. {id == null} {url == null} {newPath == null}");

            return;
        }

        if (File.Exists(newPath))
        {
            LogDebug("File exists. Reusing.");
            Boombox.StartCoroutine(LoadSongCoroutine(newPath));

            return;
        }

        if (InfoCache.DurationCache.TryGetValue(id, out var duration))
        {
            if (duration > MaxSongDuration.Value)
            {
                LogDebug("Song too long. Preventing download.");
                IAmReadyServerRpc();

                return;
            }
        }
        else
        {
            try
            {
                LogDebug("Downloading song duration data.");
                var videoDataResult = await YoutubeDL.RunVideoDataFetch(url);
                LogDebug("Downloaded song duration data.");

                if (videoDataResult.Success && videoDataResult.Data.Duration != null)
                {
                    InfoCache.DurationCache.Add(id, (float)videoDataResult.Data.Duration);
                    // Skip downloading videos that are too long
                    if (videoDataResult.Data.Duration > MaxSongDuration.Value)
                    {
                        LogDebug("Song too long. Preventing download.");
                        IAmReadyServerRpc();

                        return;
                    }
                }
                else
                {
                    LogDebug("Couldn't get song data, skipping.");
                    IAmReadyServerRpc();

                    return;
                }
            }
            catch (Exception e)
            {
                LogDebug("Error while downloading song data.");
                LogDebug(e);
                IAmReadyServerRpc();

                return;
            }
        }

        LogDebug($"Trying to download {url}.");

        var res = await YoutubeDL.RunAudioDownload(url, AudioConversionFormat.Mp3);

        LogDebug("Downloaded.");

        if (res.Success)
        {
            File.Move(res.Data, newPath);

            LogDebug($"Song {id} downloaded successfully.");
            Boombox.StartCoroutine(LoadSongCoroutine(newPath));
        }
        else
        {
            LogDebug($"Failed to download song {id}.");
            IAmReadyServerRpc();
        }
    }

    public void PrepareNextSongInPlaylist()
    {
        LogDebug("Preparing next song in playlist");
        if (InfoCache.PlaylistCache.TryGetValue(CurrentId, out var videoIds))
        {
            if (PlaylistCurrentIndex + 1 < videoIds.Count)
            {
                var id = videoIds[PlaylistCurrentIndex + 1];
                var url = $"https://youtube.com/watch?v={id}";

                LogDebug($"Preparing {url} ({id})");

                PrepareSong(id, url);
            }
            else
            {
                LogDebug("Playlist complete.");
            }
        }
        else
        {
            LogDebug("Couldn't find playlist ids!");
        }
    }

    public async void PrepareSong(string id, string url)
    {
        LogDebug($"Preparing next song {id}");

        var newPath = Path.Combine(DownloadsPath, $"{id}.mp3");

        if (File.Exists(newPath))
        {
            LogDebug("Already exists, reusing.");
            return;
        }

        if (InfoCache.DurationCache.TryGetValue(id, out var duration))
        {
            if (duration > MaxSongDuration.Value)
            {
                LogDebug("Song too long. Preventing download.");
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
                    LogDebug("Song too long. Preventing download.");
                    return;
                }
            }
            else
            {
                LogDebug("Couldn't get song length. Skipping.");
                return;
            }
        }

        var res = await YoutubeDL.RunAudioDownload(url, AudioConversionFormat.Mp3);

        if (res.Success)
        {
            File.Move(res.Data, newPath);

            LogDebug($"Prepared {id} successfully");
        }
        else
        {
            LogDebug($"Downloading {id} failed!");
        }
    }

    public async void DownloadCurrentPlaylist()
    {
        LogDebug($"Downloading playlist from {CurrentUrl} ({CurrentId})");

        PlaylistCurrentIndex = 0;
        if (!InfoCache.PlaylistCache.TryGetValue(CurrentId, out var videoIds))
        {
            LogDebug("Playlist not found in cache, downloading all ids.");

            var playlistResult = await YoutubeDL.RunVideoPlaylistDownload(CurrentUrl, 1, null, null,
                "bestvideo+bestaudio/best",
                VideoRecodeFormat.None, default, null, new InfoCache(CurrentId),
                new OptionSet {
                    FlatPlaylist = true,
                    DumpJson = true
                });

            if (!playlistResult.Success)
            {
                LogDebug("Failed to download playlist ids. Unable to proceed.");
                IAmReadyServerRpc();

                return;
            }

            videoIds = InfoCache.PlaylistCache[CurrentId];
        }

        if (videoIds.Count == 0)
        {
            LogDebug("Playlist video ids empty...");
            IAmReadyServerRpc();

            return;
        }

        var id = videoIds[0];
        var url = $"https://youtube.com/watch?v={id}";

        LogDebug($"First playlist song found: {url} ({id})... Downloading.");

        DownloadSong(id, url);
    }

    public void ProcessRequest(ParsedUri parsedUri)
    {
        var url = parsedUri.DownloadUrl;

        CurrentUri = parsedUri;
        CurrentUrl = url;
        IsPlaylist = parsedUri.UriType == UriType.Playlist;
        CurrentId = parsedUri.Id;

        LogDebug($"Processing request for {CurrentId} isPlaylist?: {IsPlaylist}");

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