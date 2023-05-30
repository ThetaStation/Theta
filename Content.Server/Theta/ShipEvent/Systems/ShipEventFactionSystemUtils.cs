﻿//Because it's pain to work with 800+ line class

using System.Linq;
using Content.Server.Access.Systems;
using Content.Server.Explosion.Components;
using Content.Server.Mind;
using Content.Server.Mind.Components;
using Content.Server.Roles;
using Content.Shared.Chat;
using Content.Shared.Explosion;
using Content.Shared.Projectiles;
using Content.Shared.Theta.ShipEvent;
using Robust.Server.Maps;
using Robust.Server.Player;

namespace Content.Server.Theta.ShipEvent.Systems;

public sealed class ShipEventFaction : PlayerFaction
{
    public int Assists;
    public List<string>? Blacklist; //blacklist for ckeys
    public string Captain; //ckey

    public Color Color;

    public Dictionary<ShipEventFaction, int> Hits = new(); //hits from other teams, not vice-versa
    public int Kills;
    public int Points;
    public int Respawns;
    
    public ShipTypePrototype? ChosenShipType;
    public EntityUid Ship;
    public string ShipName = "";
    
    public bool ShouldRespawn; //whether this team is currently waiting for respawn
    public float TimeSinceRemoval; //time since last removal

    public bool OutOfBoundsWarningReceived; //whether this team has already received warning about going out of play area
    
    public int LastBonusInterval; //how much times this team has acquired bonus points for surviving bonus interval
    
    public ShipEventFaction(string name, string iconPath, Color color, string captain,
        int points = 0, List<string>? blacklist = null) : base(name, iconPath)
    {
        Color = color;
        Captain = captain;
        Points = points;
        Blacklist = blacklist;
    }
}

public sealed partial class ShipEventFactionSystem
{
    [Dependency] private readonly IdCardSystem _cardSystem = default!;
    [Dependency] private readonly MindTrackerSystem _mindTrack = default!;

    private void Announce(string message)
    {
        _chatSys.DispatchGlobalAnnouncement(message, Loc.GetString("shipevent-announcement-title"));
    }

    /// <summary>
    /// Sends chat message to all team members
    /// </summary>
    /// <param name="team">team to which message should be send</param>
    /// <param name="message">message text</param>
    /// <param name="chatChannel">chat channel (local by default)</param>
    /// <param name="color">color of message (team's color by default)</param>
    private void TeamMessage(ShipEventFaction team, string message, ChatChannel chatChannel = ChatChannel.Local,
        Color? color = null)
    {
        if (color == null)
            color = team.Color;

        foreach (var mind in team.GetLivingMembersMinds())
        {
            if (mind.Session != null)
                _chatSys.SendSimpleMessage(message, mind.Session, chatChannel, color);
        }
    }

    private string GenerateTeamName()
    {
        _lastTeamNumber += 1;
        return $"Team №{_lastTeamNumber}";
    }

    private string GetName(EntityUid entity)
    {
        if (EntityManager.TryGetComponent(entity, out MetaDataComponent? metaComp))
            return metaComp.EntityName;

        return string.Empty;
    }

    private void SetName(EntityUid entity, string name)
    {
        if (EntityManager.TryGetComponent(entity, out MetaDataComponent? metaComp))
            metaComp.EntityName = name;

        if (_cardSystem.TryFindIdCard(entity, out var idCard))
        {
            _cardSystem.TryChangeFullName(idCard.Owner, name, idCard);
        }

        _idSys.QueueIdentityUpdate(entity);
    }

    private List<EntityUid> GetShipComponentHolders<T>(EntityUid shipEntity) where T : IComponent
    {
        List<EntityUid> entities = new();
        foreach (var comp in EntityManager.EntityQuery<T>())
        {
            if (Transform(comp.Owner).GridUid == shipEntity)
                entities.Add(comp.Owner);
        }

        return entities;
    }
    
    private List<T> GetShipComponents<T>(EntityUid shipEntity) where T : IComponent
    {
        List<T> comps = new();
        foreach (var comp in EntityManager.EntityQuery<T>())
        {
            if (Transform(comp.Owner).GridUid == shipEntity)
                comps.Add(comp);
        }

        return comps;
    }

    private int GetProjectileDamage(EntityUid entity)
    {
        if (EntityManager.TryGetComponent<MetaDataComponent>(entity, out var meta))
        {
            if (meta.EntityPrototype == null)
                return 0;

            if (_projectileDamage.ContainsKey(meta.EntityPrototype.ID))
                return _projectileDamage[meta.EntityPrototype.ID];

            var damage = 0;

            if (EntityManager.TryGetComponent<ProjectileComponent>(entity, out var proj))
                damage += (int) proj.Damage.Total;

            if (EntityManager.TryGetComponent<ExplosiveComponent>(entity, out var exp))
            {
                var damagePerIntensity =
                    (int) _protMan.Index<ExplosionPrototype>(exp.ExplosionType).DamagePerIntensity.Total;
                damage += (int) (exp.TotalIntensity * damagePerIntensity);
            }

            _projectileDamage[meta.EntityPrototype.ID] = damage;

            return damage;
        }

        return 0;
    }

    private IPlayerSession? GetSession(EntityUid entity)
    {
        if (EntityManager.TryGetComponent<MindComponent>(entity, out var mindComp))
        {
            if (mindComp.HasMind)
            {
                var session = mindComp.Mind!.Session;
                if (session != null)
                    return session;
            }
        }

        return null;
    }

    private IPlayerSession? GetSession(Mind.Mind mind)
    {
        var session = mind.Session;
        if (session != null)
            return session;

        return null;
    }
    
    public bool IsValidName(string name)
    {
        if (name == "")
            return false;

        foreach (var team in Teams)
        {
            if (team.Name == name)
                return false;
        }

        return true;
    }

    private Color GenerateTeamColor()
    {
        for (int c = 0; c < 100; c++)
        {
            var newColor = new Color(_random.NextFloat(0, 1), _random.NextFloat(0, 1), _random.NextFloat(0, 1));
            if (IsValidColor(newColor))
                return newColor;
        }

        return Color.White;
    }

    public bool IsValidColor(Color color)
    {
        var minimalColorDelta = 200; //not based on anything, simply a magic number for comparison

        foreach (var team in Teams)
        {
            var otherColor = team.Color;
            var delta = RedmeanColorDelta(color, otherColor);
            if (delta < minimalColorDelta)
                return false;
        }

        return true;
    }

    public bool IsValidColor(string color)
    {
        var newColor = Color.TryFromHex(color);
        if (newColor == null)
            return false;

        return IsValidColor((Color) newColor);
    }

    //todo: actually PR it to RT instead of putting it here
    private double RedmeanColorDelta(Color a, Color b)
    {
        var deltaR = a.RByte - b.RByte;
        var deltaG = a.GByte - b.GByte;
        var deltaB = a.BByte - b.BByte;
        var avgR = (a.RByte + b.RByte) / 2;
        var delta = (2 + avgR / 256) * deltaR * deltaR + 4 * deltaG * deltaG + (2 + (255 - avgR) / 256) * deltaB;
        return Math.Sqrt(delta);
    }

    private EntityUid RandomPosSpawn(string mapPath)
    {
        const int shipCollisionCheckRange = 30;
        
        Vector2i mapPos = Vector2i.Zero;
        for (int c = 0; c < 100; c++)
        {
            mapPos = (Vector2i) _random.NextVector2Box(0, 0, MaxSpawnOffset, MaxSpawnOffset).Rounded();
            if (!_mapMan.FindGridsIntersecting(TargetMap, 
                    new Box2(mapPos - shipCollisionCheckRange, mapPos + shipCollisionCheckRange)).Any())
                break;
        }

        var loadOptions = new MapLoadOptions
        {
            Rotation = _random.NextAngle(),
            Offset = mapPos,
            LoadMap = false
        };

        if (_mapSys.TryLoad(TargetMap, mapPath, out var rootUids, loadOptions))
            return rootUids[0];

        return EntityUid.Invalid;
    }

    //to avoid cluttering roundend statistics
    public void ClearMindTracker()
    {
        _mindTrack.ClearMindSet();
        foreach (var mind in EntityManager.EntityQuery<MindComponent>())
        {
            if(mind.HasMind)
                _mindTrack.AddMind(mind.Mind!);
        }
    }
}

