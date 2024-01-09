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
    private static readonly int[] timestampPartMagnitudes = [1, 60, 3600];
    
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
    
    /// <summary>
    /// Parse an ffmpeg timestamp string to a number of seconds.
    /// </summary>
    /// <param name="timestamp">The timestamp to parse</param>
    /// <returns>The timestamp's time in seconds</returns>
    protected static float ParseTimestamp(string timestamp)
    {
        if (String.IsNullOrWhiteSpace(timestamp))
            throw new ArgumentException("Timestamp cannot be null or whitespace");
        
        if (float.TryParse(timestamp, out var parsedFloat)) return parsedFloat;

        var parts = timestamp.Split(":")
            .Select(part => part.Trim())
            .Reverse()
            .Select((part, index) => (part, index))
            .ToArray();

        if (parts.Length > 3)
            throw new NotImplementedException("Timestamp has more than 3 parts, support is not implemented");

        try
        {
            var seconds = float.Parse(parts[0].part);
            
            foreach (var (part, index) in parts[1..])
            {
                if (part is null) continue;
                seconds += int.Parse(part) * timestampPartMagnitudes[index];
            }

            return seconds;
        }
        catch (Exception error)
        {
            throw new ArgumentException($"Failed to parse the timestamp '{timestamp}'", error);
        }
    }

    protected static async Task<float> GetEffectiveDuration(ConfiguredTrack track)
    {
        if (track.VideoId == null) 
            throw new NullReferenceException("Track VideoId must not be null.");
        
        var duration = await InfoCache.DurationCache.ComputeIfAbsentAsync(track.VideoId, FetchSongDuration);
        var effectiveDuration = duration;

        if (track.StartTimestamp is not null)
        {
            var startTime = ParseTimestamp(track.StartTimestamp);
            if (duration <= startTime) return 0;

            effectiveDuration -= startTime;
        }

        if (track.EndTimestamp is not null)
        {
            var endTime = ParseTimestamp(track.EndTimestamp);
            if (duration <= endTime) return effectiveDuration;

            effectiveDuration -= duration - endTime;
        }

        return effectiveDuration;
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

        var duration = await GetEffectiveDuration(track);
        if (duration > MaxSongDuration.Value)
            throw new VideoTooLongException("Track too long, skipping.");
        
        string?[] downloaderArgs = [
            "ffmpeg:-nostats",
            "ffmpeg:-loglevel 0",
            track.StartTimestamp != null ? $"ffmpeg:-ss {track.StartTimestamp}" : null,
            track.EndTimestamp != null ? $"ffmpeg:-to {track.EndTimestamp}" : null,
        ];

        string?[] extractAudioFilterArgs = [
            track.VolumeScalar != null ? $"volume={track.VolumeScalar}" : null,
        ];
        extractAudioFilterArgs = extractAudioFilterArgs
            .Where(x => x is not null)
            .Cast<string>().ToArray();
        
        string?[] postProcessorArgs = [
          extractAudioFilterArgs.Length > 0 ? $"ExtractAudio:-filter:a {String.Join(":", extractAudioFilterArgs)}" : null,
        ];

        Logger?.LogDebug($"Starting download ({track.TrackName}).");
        var res = await YoutubeDL.RunAudioDownload(
            track.VideoId,
            AudioConversionFormat.Mp3,
            overrideOptions: new()
            {
                DownloaderArgs = downloaderArgs.Where(x => x != null).Cast<string>().ToArray(),
                PostprocessorArgs = postProcessorArgs.Where(x => x != null).Cast<string>().ToArray(),
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