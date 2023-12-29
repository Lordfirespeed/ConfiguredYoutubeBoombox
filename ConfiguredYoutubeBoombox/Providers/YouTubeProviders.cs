using System;

namespace ConfiguredYoutubeBoombox.Providers;

public class YouTubeProvider : Provider
{
    public override string[] Hosts => ["youtube.com", "www.youtube.com"];

    public override ParsedUri ParseUri(Uri uri)
    {
        var id = string.Empty;
        var uriType = UriType.Video;

        var collection = HttpUtility.ParseQueryString(uri.Query);
        id = collection.Get("v");

        if (id == null)
        {
            id = collection.Get("list");
            uriType = UriType.Playlist;
        }

        if (string.IsNullOrEmpty(id)) return null;

        return new ParsedUri(uri, id, uri.Host + uri.PathAndQuery, uriType);
    }
}

public class YouTuDotBeProvider : Provider
{
    public override string[] Hosts => ["youtu.be", "www.youtu.be"];

    public override ParsedUri ParseUri(Uri uri)
    {
        return new ParsedUri(uri, uri.AbsolutePath.Substring(1), uri.Host + uri.AbsolutePath, UriType.Video);
    }
}