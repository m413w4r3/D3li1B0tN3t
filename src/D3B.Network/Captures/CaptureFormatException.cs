namespace D3B.Network.Captures;

/// <summary>The exception thrown when a file is not a capture this version of D3B reads, or its records are damaged.</summary>
public sealed class CaptureFormatException : IOException
{
    /// <summary>Initializes a new instance of the <see cref="CaptureFormatException"/> class.</summary>
    public CaptureFormatException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CaptureFormatException"/> class.</summary>
    /// <param name="message">What is wrong with the capture.</param>
    public CaptureFormatException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CaptureFormatException"/> class.</summary>
    /// <param name="message">What is wrong with the capture.</param>
    /// <param name="innerException">The error met while reading it.</param>
    public CaptureFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
