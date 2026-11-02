namespace D3B.Network.Framing;

/// <summary>The exception thrown when bytes cannot be frames: a length too long to be one, or longer than allowed.</summary>
public sealed class FrameFormatException : IOException
{
    /// <summary>Initializes a new instance of the <see cref="FrameFormatException"/> class.</summary>
    public FrameFormatException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FrameFormatException"/> class.</summary>
    /// <param name="message">What is wrong with the bytes.</param>
    public FrameFormatException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FrameFormatException"/> class.</summary>
    /// <param name="message">What is wrong with the bytes.</param>
    /// <param name="innerException">The error met while reading them.</param>
    public FrameFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
