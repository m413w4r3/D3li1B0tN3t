namespace D3B.Data.Spells;

/// <summary>The shape of a spell's zone, the client's <c>SpellZoneShape</c>: each value is the character the client's zones are written with.</summary>
/// <remarks>Named as the client's code names them; the cells each covers are <c>D3B.Engine</c>'s <c>SpellZones</c> (D187).</remarks>
public enum SpellZoneShape : byte
{
    /// <summary>No zone: 0, on a spell's preview zone when it has none.</summary>
    None = 0,

    /// <summary>No cell, the client's <c>Empty</c>: a space.</summary>
    Empty = (byte)' ',

    /// <summary>The client's <c>DiagonalCrossWithoutCenter</c>: <c>#</c>.</summary>
    DiagonalCrossWithoutCenter = (byte)'#',

    /// <summary>The client's <c>Star</c>: <c>*</c>.</summary>
    Star = (byte)'*',

    /// <summary>The client's <c>DiagonalCross</c>: <c>+</c>.</summary>
    DiagonalCross = (byte)'+',

    /// <summary>The client's <c>DiagonalPerpendicularLine</c>: <c>-</c>.</summary>
    DiagonalPerpendicularLine = (byte)'-',

    /// <summary>The client's <c>DiagonalLine</c>: <c>/</c>.</summary>
    DiagonalLine = (byte)'/',

    /// <summary>The cells the zone lists, the client's <c>Custom</c>: <c>;</c>.</summary>
    Custom = (byte)';',

    /// <summary>The whole map with the dead, the client's <c>WholeMapWithTheDead</c>: <c>A</c>.</summary>
    WholeMapWithTheDead = (byte)'A',

    /// <summary>The client's <c>Boomerang</c>: <c>B</c>.</summary>
    Boomerang = (byte)'B',

    /// <summary>The client's <c>Circle</c>: <c>C</c>.</summary>
    Circle = (byte)'C',

    /// <summary>The client's <c>Checkerboard</c>: <c>D</c>.</summary>
    Checkerboard = (byte)'D',

    /// <summary>The client's <c>Fork</c>: <c>F</c>.</summary>
    Fork = (byte)'F',

    /// <summary>The client's <c>Square</c>: <c>G</c>.</summary>
    Square = (byte)'G',

    /// <summary>The client's <c>OutsideCircle</c>: <c>I</c>.</summary>
    OutsideCircle = (byte)'I',

    /// <summary>The client's <c>Line</c>: <c>L</c>.</summary>
    Line = (byte)'L',

    /// <summary>The client's <c>Regular</c>: <c>N</c>.</summary>
    Regular = (byte)'N',

    /// <summary>The client's <c>Ring</c>: <c>O</c>.</summary>
    Ring = (byte)'O',

    /// <summary>One cell, the client's <c>Point</c>: <c>P</c>.</summary>
    Point = (byte)'P',

    /// <summary>The client's <c>CrossWithoutCenter</c>: <c>Q</c>.</summary>
    CrossWithoutCenter = (byte)'Q',

    /// <summary>The client's <c>Rectangle</c>: <c>R</c>.</summary>
    Rectangle = (byte)'R',

    /// <summary>The client's <c>PerpendicularLine</c>: <c>T</c>.</summary>
    PerpendicularLine = (byte)'T',

    /// <summary>The client's <c>HalfCircle</c>: <c>U</c>.</summary>
    HalfCircle = (byte)'U',

    /// <summary>The client's <c>Cone</c>: <c>V</c>.</summary>
    Cone = (byte)'V',

    /// <summary>The client's <c>SquareWithoutDiagonals</c>: <c>W</c>.</summary>
    SquareWithoutDiagonals = (byte)'W',

    /// <summary>The client's <c>Cross</c>: <c>X</c>.</summary>
    Cross = (byte)'X',

    /// <summary>The client's <c>OutsideComplexCircle</c>: <c>Z</c>.</summary>
    OutsideComplexCircle = (byte)'Z',

    /// <summary>The whole map, the client's <c>WholeMap</c>: <c>a</c>.</summary>
    WholeMap = (byte)'a',

    /// <summary>The client's <c>LineFromCaster</c>: <c>l</c>.</summary>
    LineFromCaster = (byte)'l',
}
