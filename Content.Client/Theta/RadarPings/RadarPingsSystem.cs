﻿using System.Numerics;
using Content.Client.Shuttles;
using Content.Shared.Mobs.Components;
using Content.Shared.Theta.RadarPings;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client.Theta.RadarPings;

public sealed class RadarPingsSystem : SharedRadarPingsSystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public event Action<PingInformation>? OnEventReceived;

    private bool _canNetworkPing = true;

    public override void Initialize()
    {
        SubscribeNetworkEvent<SendPingEvent>(ReceivePing);
    }

    private void ReceivePing(SendPingEvent ev)
    {
        PlayPing(ev.Ping);
    }

    public void SendPing(EntityUid pingOwner, Vector2 coordinates)
    {
        EntityUid? sender = _playerManager.LocalSession?.AttachedEntity;
        if (sender == null)
        {
            Log.Error("Ping sender is null, how's that possible?");
            return;
        }

        if (_canNetworkPing)
        {
            RaiseNetworkEvent(new SpreadPingEvent(GetNetEntity(sender.Value), GetNetEntity(pingOwner), coordinates));
            _canNetworkPing = false;
            Timer.Spawn(NetworkPingCd, () => _canNetworkPing = true);
        }

        PlaySignalSound(Filter.Entities(sender.Value));
        PlayPing(GetPing(pingOwner, coordinates));
    }

    private void PlayPing(PingInformation ping)
    {
        OnEventReceived?.Invoke(ping);
    }

    protected override PingInformation GetPing(EntityUid sender, Vector2 coordinates)
    {
        var color = DefaultPingColor;
        if (HasComp<ShuttleConsoleComponent>(sender))
        {
            color = CaptainPingColor;
        }
        else if (HasComp<MobStateComponent>(sender))
        {
            color = MobPingColor;
        }

        return new PingInformation(coordinates, color);
    }
}
