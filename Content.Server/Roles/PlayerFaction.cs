﻿using Robust.Server.Player;
using Robust.Shared.Utility;

namespace Content.Server.Roles;

public class PlayerFaction
{
    /// <summary>
    /// Name of the faction
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Icon of the faction
    /// </summary>
    public SpriteSpecifier? Icon { get; }

    /// <summary>
    /// Members of the faction
    /// </summary>
    public List<Role> Members { get; }

    public PlayerFaction(string name, string iconPath = "")
    {
        Name = name;
        if(iconPath != "")
            Icon = new SpriteSpecifier.Texture(new ResPath(iconPath));
        Members = new List<Role>();
    }
    
    public void AddMember(Role member)
    {
        if (Members.Contains(member))
            return;

        member.Faction = this;
        Members.Add(member);
    }
    
    public void RemoveMember(Role member)
    {
        if (!Members.Contains(member))
            return;

        member.Faction = null;
        Members.Remove(member);
    }
    
    public EntityUid GetMemberEntity(Role member)
    {
        if (!Members.Contains(member))
            return EntityUid.Invalid;

        EntityUid? entity = member.Mind.OwnedEntity;
        if (entity != null)
            return (EntityUid) entity;

        return EntityUid.Invalid;
    }

    public IPlayerSession? GetMemberSession(Role member)
    {
        return member.Mind.Session;
    }
    
    public List<EntityUid> GetLivingMembersEntities()
    {
        List<EntityUid> living = new();
        foreach (Role member in Members)
        {
            if (!member.Mind.CharacterDeadPhysically)
                living.Add((EntityUid)member.Mind.OwnedEntity!);
        }

        return living;
    }
    
    public List<Mind.Mind> GetLivingMembersMinds()
    {
        List<Mind.Mind> living = new();
        foreach (Role member in Members)
        {
            if (!member.Mind.CharacterDeadPhysically)
                living.Add(member.Mind);
        }

        return living;
    }

    /// <summary>
    /// Returns list of ckeys in this faction (logged-in members only)
    /// </summary>
    public List<string> GetMemberUserNames()
    {
        List<string> names = new();
        foreach (Role member in Members)
        {
            if (member.Mind.TryGetSession(out var session))
                names.Add(session.ConnectedClient.UserName);
        }

        return names;
    }

    /// <summary>
    /// Returns dictionary of ckeys, associated with member roles (logged-in members only)
    /// </summary>
    public Dictionary<string, Role> GetMembersByUserNames()
    {
        Dictionary<string, Role> pairs = new();
        foreach (Role member in Members)
        {
            if (member.Mind.TryGetSession(out var session))
                pairs[session.ConnectedClient.UserName] = member;
        }

        return pairs;
    }
}
