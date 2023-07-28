﻿using Robust.Shared.Prototypes;

namespace Content.Shared.Theta.RadarRenderable;

[Prototype("radarEntityView")]
public sealed class RadarEntityViewPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = default!;

    [DataField("defaultColor", required: true)]
    public Color DefaultColor;

    [DataField("form")]
    public Forms Form = Forms.Circle;

    [DataField("size")]
    public float Size = 1f;

    public enum Forms
    {
        Circle,
        FootingTriangle,
        CenteredTriangle,
        Line,
    }
}
