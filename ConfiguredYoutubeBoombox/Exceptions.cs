using System;

namespace ConfiguredYoutubeBoombox;

public class VideoTooLongException : Exception
{
    public VideoTooLongException()
    {
    }

    public VideoTooLongException(string message)
        : base(message)
    {
    }

    public VideoTooLongException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

public class VideoTooLongExceptiona : Exception
{
    public VideoTooLongExceptiona()
    {
    }

    public VideoTooLongExceptiona(string message)
        : base(message)
    {
    }

    public VideoTooLongExceptiona(string message, Exception inner)
        : base(message, inner)
    {
    }
}