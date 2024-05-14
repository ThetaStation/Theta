﻿using System.Numerics;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Serialization;

namespace Content.Shared.Theta.RadarPings;

public abstract class SharedRadarPingsSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;

    private const string PingSound = "/Audio/Theta/Shipevent/radar_ping.ogg";
    protected readonly Color DefaultPingColor = Color.Blue;
    protected readonly Color CaptainPingColor = Color.Red;
    protected readonly Color MobPingColor = Color.LightGreen;

    protected readonly TimeSpan NetworkPingCd = TimeSpan.FromSeconds(0.3);


    protected abstract PingInformation GetPing(EntityUid sender, Vector2 coordinates);

    protected void PlaySignalSound(Filter hearer)
    {
        if(hearer.Count == 0)
            return;
        _audioSystem.PlayGlobal(PingSound, hearer, false);
    }
}

[Serializable, NetSerializable]
public sealed class SpreadPingEvent : EntityEventArgs
{
    public NetEntity Sender;
    public NetEntity PingOwner;
    public Vector2 Coordinates;

    public SpreadPingEvent(NetEntity sender, NetEntity pingOwner, Vector2 coordinates)
    {
        Sender = sender;
        PingOwner = pingOwner;
        Coordinates = coordinates;
    }
}

[Serializable, NetSerializable]
public sealed class SendPingEvent : EntityEventArgs
{
    public PingInformation Ping;

    public SendPingEvent(PingInformation ping)
    {
        Ping = ping;
    }
}

[Serializable, NetSerializable]
public record class PingInformation(Vector2 Coordinates, Color Color)
{
    public Vector2 Coordinates = Coordinates;
    public Color Color = Color;
}
