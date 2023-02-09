﻿using Robust.Shared.Serialization;

namespace Content.Shared.Shuttles.Events;

/// <summary>
/// Raised on the client when it change shuttle name
/// </summary>
[Serializable, NetSerializable]
public sealed class ShuttleConsoleChangeShipNameMessage : BoundUserInterfaceMessage
{
    public string? NewShipName;
}
