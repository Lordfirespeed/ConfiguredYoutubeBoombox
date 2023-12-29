﻿using System;

namespace ConfiguredYoutubeBoombox.Providers
{
    public class YouTubeProvider : Provider
    {
        public override string[] Hosts => new[] { "youtube.com", "www.youtube.com" };

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

            if (id == null || id == string.Empty) return null;

            return new ParsedUri(uri, id, uri.Host + uri.PathAndQuery, uriType);
        }
    }

    public class YouTuBeProvider : Provider
    {
        public override string[] Hosts => new[] { "youtu.be", "www.youtu.be" };

        public override ParsedUri ParseUri(Uri uri)
        {
            return new ParsedUri(uri, uri.AbsolutePath.Substring(1), uri.Host + uri.AbsolutePath, UriType.Video);
        }
    }
}