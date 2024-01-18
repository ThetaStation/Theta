﻿using Robust.Shared.Audio;

namespace Content.Server.Theta.ShipEvent.Components;

[RegisterComponent]
public sealed partial class ShipPickupBeaconComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("id", required: true)]
    public string? Id;

    [DataField("teleportationSound")]
    public SoundSpecifier? TeleportationSound;
}
