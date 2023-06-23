﻿using Content.Server.Shuttles.Systems;
using Content.Server.Theta.ShipEvent.Components;
using Content.Shared.Shuttles.Components;

namespace Content.Server.Theta.ShipEvent.Systems;

public sealed class ChangeIFFOnSplitSystem : EntitySystem
{
    [Dependency] private readonly ShuttleSystem _shuttleSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ChangeIFFOnSplitComponent, PostGridSplitEvent>(OnSplit);
    }

    private void OnSplit(EntityUid uid, ChangeIFFOnSplitComponent comp, ref PostGridSplitEvent args)
    {
        if (comp.Replicate)
        {
            var newComp = AddComp<ChangeIFFOnSplitComponent>(args.Grid);
            (newComp.NewFlags, newComp.NewColor, newComp.Remove, newComp.Replicate) =
                (comp.NewFlags, comp.NewColor, comp.Remove, comp.Replicate);
        }

        if (comp.Remove)
        {
            RemComp<IFFComponent>(args.Grid);
            return;
        }

        IFFComponent? originIff = CompOrNull<IFFComponent>(args.OldGrid);

        IFFFlags flags = comp.NewFlags ?? (originIff?.Flags ?? IFFFlags.None);
        Color color = comp.NewColor ?? (originIff?.Color ?? Color.White);

        var newIff = EnsureComp<IFFComponent>(args.Grid);
        Logger.Info($"Adding flags: {flags}. Comp override flags: {(comp.NewFlags == null ? "NULL" : comp.NewFlags)}; " +
                    $"Origin IFF flags: {(originIff?.Flags == null ? "NULL" : originIff.Flags)};");
        _shuttleSystem.AddIFFFlag(args.Grid, flags, newIff);
        _shuttleSystem.SetIFFColor(args.Grid, color, newIff);
    }
}
