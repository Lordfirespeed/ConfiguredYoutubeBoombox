using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ConfiguredYoutubeBoombox.util;
using YoutubeDLSharp.Options;
using static ConfiguredYoutubeBoombox.Plugin;

namespace ConfiguredYoutubeBoombox;

public class TrackDownloader
{
    protected static async Task<float> FetchSongDuration(string id)
    {
        Logger?.LogDebug("Fetching video metadata.");
        var videoDataResult = await YoutubeDL.RunVideoDataFetch(id);

        if (videoDataResult is { Success: true, Data.Duration: not null })
        {
            return (float)videoDataResult.Data.Duration;
        }

        throw new Exception("Failed to fetch video duration.");
    }

    public static async Task DownloadTrack(ConfiguredTrack track)
    {
        if (track.VideoId == null) 
            throw new NullReferenceException("Track VideoId must not be null.");
        if (track.TrackName == null)
            throw new NullReferenceException("Track TrackName must not be null.");
        
        Logger?.LogDebug($"Downloading '{track.TrackName}' ({track.VideoId})");

        var newPath = Path.Combine(DownloadsPath, $"cytbb.{track.TrackName}-{track.VideoId}.mp3");

        if (File.Exists(newPath))
        {
            Logger?.LogDebug("Track already downloaded, nothing to do.");
            return;
        }
        
        var duration = await InfoCache.DurationCache.ComputeIfAbsentAsync(track.VideoId, FetchSongDuration);
        if (duration > MaxSongDuration.Value)
        {
            throw new VideoTooLongException("Track too long, skipping.");
        }
        
        string?[] downloaderArgs = [
            track.StartTimestamp != null ? $"-ss {track.StartTimestamp}" : null,
            track.EndTimestamp != null ? $"-to {track.EndTimestamp}" : null
        ];

        Logger?.LogDebug($"Starting download ({track.TrackName}).");
        var res = await YoutubeDL.RunAudioDownload(
            track.VideoId,
            AudioConversionFormat.Mp3,
            overrideOptions: new()
            {
                Downloader = "ffmpeg",
                DownloaderArgs = String.Join(' ', downloaderArgs.Where(x => x != null))
            });
        Logger?.LogDebug($"Download complete ({track.TrackName}).");

        if (!res.Success)
        {
            throw new YoutubeDLProcessFailed($"Failed to download '{track.TrackName}' ({track.VideoId}).");
        }
        
        File.Move(res.Data, newPath);
        Logger?.LogDebug($"'{track.TrackName}' ({track.VideoId}) downloaded successfully.");
    }
}

public static class InfoCache
{
    public static readonly Dictionary<string, float> DurationCache = new();
}