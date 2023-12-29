using System;

namespace ConfiguredYoutubeBoombox.Providers;

public enum UriType
{
    Video,
    Playlist
}

public class ParsedUri
{
    public ParsedUri(Uri uri, string id, string downloadUrl, UriType uriType)
    {
        Uri = uri;
        Id = id;
        DownloadUrl = downloadUrl;
        UriType = uriType;
    }

    public Uri Uri { get; }

    public string Id { get; }

    public string DownloadUrl { get; }

    public UriType UriType { get; }
}

public abstract class Provider
{
    public abstract string[] Hosts { get; }

    public abstract ParsedUri ParseUri(Uri uri);
}