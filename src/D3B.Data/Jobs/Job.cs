using System.Runtime.InteropServices;

namespace D3B.Data.Jobs;

/// <summary>A profession, such as lumberjack or fisherman, from the client's <c>JobsDataRoot</c>.</summary>
/// <remarks>Unmanaged and packed: MemoryPack copies an array of it as raw bytes, so its layout is part of the pack format.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Job
{
    /// <summary>The job of the skills that belong to no profession, such as using a zaap or opening a door: the client names it Base.</summary>
    public const int Base = 1;

    /// <summary>The icon of a job the client draws none for: <see cref="Base"/>'s.</summary>
    public const int NoIcon = -1;

    /// <summary>Gets the job's identifier.</summary>
    public int Id { get; }

    /// <summary>Gets the text naming it, by identifier.</summary>
    public int NameId { get; }

    /// <summary>Gets its icon, in <see cref="Icons.IconCatalog.Jobs"/>, or <see cref="NoIcon"/>.</summary>
    public int IconId { get; }

    /// <summary>Initializes a new instance of the <see cref="Job"/> struct.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="nameId">The text naming it.</param>
    /// <param name="iconId">Its icon, or <see cref="NoIcon"/>.</param>
    public Job(int id, int nameId, int iconId)
    {
        Id = id;
        NameId = nameId;
        IconId = iconId;
    }
}
