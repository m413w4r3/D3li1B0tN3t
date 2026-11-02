namespace D3B.Data.Packs;

/// <summary>The exception thrown when a file is not a pack this version of D3B reads, or a part of it is damaged.</summary>
public sealed class PackFormatException : IOException
{
    /// <summary>Initializes a new instance of the <see cref="PackFormatException"/> class.</summary>
    public PackFormatException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PackFormatException"/> class.</summary>
    /// <param name="message">What is wrong with the pack.</param>
    public PackFormatException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PackFormatException"/> class.</summary>
    /// <param name="message">What is wrong with the pack.</param>
    /// <param name="innerException">The error met while reading it.</param>
    public PackFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
