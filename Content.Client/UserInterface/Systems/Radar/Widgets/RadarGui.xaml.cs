﻿using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.UserInterface.Systems.Radar.Widgets;

[GenerateTypedNameReferences]
public sealed partial class RadarGui : UIWidget
{
    public RadarGui()
    {
        RobustXamlLoader.Load(this);
        SetSize = new(400, 400);
    }
}
