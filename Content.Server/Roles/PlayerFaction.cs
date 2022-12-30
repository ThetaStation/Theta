﻿using Robust.Shared.Utility;

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
	public SpriteSpecifier Icon { get; }

	/// <summary>
	/// Members of the faction
	/// </summary>
	public List<Role> Members { get; }

	public PlayerFaction(string name, string iconPath)
	{
		Name = name;
		Icon = new SpriteSpecifier.Texture(new ResourcePath(iconPath));
		Members = new List<Role>();
	}

	public void AddMember(Role member)
	{
		if(Members.Contains(member)) { return; }
		member.Faction = this;
		Members.Add(member);
	}

	public void RemoveMember(Role member)
	{
		if (!Members.Contains(member)) { return; }
		member.Faction = null;
		Members.Remove(member);
	}

    public EntityUid GetMemberEntity(Role member)
    {
        if (!Members.Contains(member)) { return EntityUid.Invalid; }

        EntityUid? entity = member.Mind.OwnedEntity;
        if (entity != null) { return (EntityUid)entity; }
        return EntityUid.Invalid;
    }

    public List<Mind.Mind> GetLivingMembers()
	{
		List<Mind.Mind> living = new();
		foreach (Role member in Members)
		{
			if (!member.Mind.CharacterDeadPhysically) { living.Add(member.Mind); }
		}

		return living;
	}
}
