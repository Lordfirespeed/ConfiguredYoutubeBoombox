using Newtonsoft.Json;

namespace ConfiguredYoutubeBoombox;

public class ConfiguredTrackListFile
{
    [JsonProperty("tracks")]
    public ConfiguredTrack[]? Tracks { get; set; }
}

public class ConfiguredTrack
{
    [JsonProperty("youtubeVideoId")] 
    public string? VideoId { get; set; }
    
    [JsonProperty("trackName")]
    public string? TrackName { get; set; }
    
    [JsonProperty("startTimestamp")]
    public string? StartTimestamp { get; set; }
    
    [JsonProperty("endTimestamp")]
    public string? EndTimestamp { get; set; }
    
    [JsonProperty("volumeScalar")]
    public float? VolumeScalar { get; set; }
}