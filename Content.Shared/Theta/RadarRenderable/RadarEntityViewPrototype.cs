﻿using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Theta.RadarRenderable;

[Prototype("radarEntityView")]
public sealed class RadarEntityViewPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = default!;

    [DataField("defaultColor", required: true)]
    public Color DefaultColor;

    [DataField("form")]
    public RadarRenderableForm OnRadarForm = default!;
}

public abstract class RadarRenderableForm
{
    [DataField("constScale")]
    public bool ConstantScale = true;
}

[Serializable]
[DataDefinition]
public sealed partial class ShapeRadarForm : RadarRenderableForm
{
    [DataField("vertices", required: true)]
    public Vector2[] Vertices = Array.Empty<Vector2>();

    [DataField("size")]
    public float Size = 1f;

    [DataField("primitiveTopology")]
    public Enum PrimitiveTopology = SharedDrawPrimitiveTopology.LineStrip;
}

[Serializable]
[DataDefinition]
public sealed partial class CircleRadarForm : RadarRenderableForm
{
    [DataField("radius")]
    public float Radius = 1f;

    [DataField("filled")]
    public bool Filled = true;
}

[Serializable]
[DataDefinition]
public sealed partial class CharRadarForm : RadarRenderableForm
{
    [DataField("char", required: true)]
    public char Char = '\0';

    [DataField("scale")]
    public float Scale = 1f;
}

[Serializable]
[DataDefinition]
public sealed partial class TextureRadarForm : RadarRenderableForm
{
    [DataField("sprite", required: true)]
    public SpriteSpecifier Sprite = default!;

    [DataField("scale")]
    public float Scale = 1f;
}


// Full copy of Robust.Client.DrawPrimitiveTopology
public enum SharedDrawPrimitiveTopology : byte
{
    PointList,
    TriangleList,
    TriangleFan,
    TriangleStrip,
    LineList,
    LineStrip,
    LineLoop
}
