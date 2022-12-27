﻿using Content.Shared.MachineLinking;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Theta.ShipEvent.Console;

[RegisterComponent, Access(typeof(CannonConsoleSystem))]
public sealed class CannonConsoleComponent : Component
{
    /// <summary>
    /// The machine linking port for the console
    /// </summary>
    [DataField("linkingPort", customTypeSerializer: typeof(PrototypeIdSerializer<TransmitterPortPrototype>))]
    public readonly string LinkingPort = "CannonConsoleSender";
}