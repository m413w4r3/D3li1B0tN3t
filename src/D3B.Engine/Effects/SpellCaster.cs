namespace D3B.Engine.Effects;

/// <summary>Who casts the spell an effect casts, and where: the client's actions that cast a spell, read as the references read them (D227).</summary>
public enum SpellCaster
{
    /// <summary>The fighter the effect reaches casts it on its own cell: 792, 793, 2792 and 2793.</summary>
    TargetOnItself,

    /// <summary>The caster casts it on the cell of the fighter the effect reaches: 1160, 2160 and 1018.</summary>
    CasterOnTarget,

    /// <summary>The fighter the effect reaches casts it on the cell cast on: 2794 and 2795.</summary>
    TargetOnCell,

    /// <summary>The caster casts it on the cell cast on: 2960.</summary>
    CasterOnCell,

    /// <summary>The fighter the effect reaches casts it on the caster's cell: 1017 and 2017.</summary>
    TargetOnCaster,

    /// <summary>The caster casts it on its own cell: 1019.</summary>
    CasterOnItself,
}
