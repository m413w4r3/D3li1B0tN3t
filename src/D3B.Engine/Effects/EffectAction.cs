namespace D3B.Engine.Effects;

/// <summary>What an effect id does: its kind, its element, and the characteristic it changes.</summary>
/// <param name="Kind">What it does.</param>
/// <param name="Element">Its element, for damage, a steal or a heal.</param>
/// <param name="Characteristic">The characteristic a boost or a deboost changes, or 0.</param>
public readonly record struct EffectAction(EffectKind Kind, EffectElement Element, int Characteristic);
