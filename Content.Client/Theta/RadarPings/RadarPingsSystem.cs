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

    private readonly TimeSpan _networkPingCd = TimeSpan.FromSeconds(0.3);
    private bool _canNetworkPing = true;

    public override void Initialize()
    {
        SubscribeNetworkEvent<SendPingEvent>(ReceivePing);
    }

    private void ReceivePing(SendPingEvent ev)
    {
        OnEventReceived?.Invoke(ev.Ping);
    }

    public PingInformation SendPing(EntityUid pingOwner, Vector2 coordinates)
    {
        var sender = _playerManager.LocalPlayer!.ControlledEntity!.Value;
        if (_canNetworkPing)
        {
            RaiseNetworkEvent(new SpreadPingEvent(sender, pingOwner, coordinates));
            _canNetworkPing = false;
            Timer.Spawn(_networkPingCd, () => _canNetworkPing = true);
        }

        var ping = GetPing(pingOwner, coordinates);
        PlaySignalSound(Filter.Entities(sender), pingOwner);
        return ping;
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
