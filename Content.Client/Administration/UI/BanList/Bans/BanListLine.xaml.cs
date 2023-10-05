﻿using Content.Shared.Administration.BanList;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client.Administration.UI.BanList.Bans;

[GenerateTypedNameReferences]
public sealed partial class BanListLine : BoxContainer, IBanListLine<SharedServerBan>
{
    public SharedServerBan Ban { get; }

    public event Action<BanListLine>? IdsClicked;

    public BanListLine(SharedServerBan ban)
    {
        RobustXamlLoader.Load(this);

        Ban = ban;
        IdsHidden.OnPressed += IdsPressed;

        BanListEui.SetData(this, ban);
    }

    private void IdsPressed(ButtonEventArgs buttonEventArgs)
    {
        IdsClicked?.Invoke(this);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        IdsHidden.OnPressed -= IdsPressed;
        IdsClicked = null;
    }
}
