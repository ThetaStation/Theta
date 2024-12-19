using Robust.Shared.Serialization;

namespace Content.Shared.Theta.ShipEvent.UI;

[Serializable, NetSerializable]
public sealed class CaptainMenuBoundUserInterfaceState : BoundUserInterfaceState
{
    public string Name;
    public ShipTypePrototype? CurrentShipType;
    public List<string> Members;
    public string? Password;
    public int MaxMembers;

    public CaptainMenuBoundUserInterfaceState(string name, List<string> members, ShipTypePrototype? currentShipType, string? password, int maxMembers)
    {
        Name = name;
        Members = members;
        CurrentShipType = currentShipType;
        Password = password;
        MaxMembers = maxMembers;
    }
}

[Serializable, NetSerializable]
public sealed class CaptainMenuChangeShipMessage : BoundUserInterfaceMessage
{
    public ShipTypePrototype NewShip = default!;

    public CaptainMenuChangeShipMessage(ShipTypePrototype newShip)
    {
        NewShip = newShip;
    }
}

[Serializable, NetSerializable]
public sealed class CaptainMenuKickMemberMessage : BoundUserInterfaceMessage
{
    public string CKey;

    public CaptainMenuKickMemberMessage(string ckey)
    {
        CKey = ckey;
    }
}

[Serializable, NetSerializable]
public sealed class CaptainMenuSetPasswordMessage : BoundUserInterfaceMessage
{
    public string? Password;

    public CaptainMenuSetPasswordMessage(string? password)
    {
        Password = password;
    }
}

[Serializable, NetSerializable]
public sealed class CaptainMenuSetMaxMembersMessage : BoundUserInterfaceMessage
{
    public int MaxMembers;

    public CaptainMenuSetMaxMembersMessage(int maxMembers)
    {
        MaxMembers = maxMembers;
    }
}

[Serializable, NetSerializable]
public sealed class CaptainMenuSetCaptainMessage : BoundUserInterfaceMessage
{
    public string? CKey;

    public CaptainMenuSetCaptainMessage(string? ckey)
    {
        CKey = ckey;
    }
}

[Serializable, NetSerializable]
public sealed class CaptainMenuFormFleetMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class CaptainMenuRespawnTeamMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class CaptainMenuDisbandTeamMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public enum CaptainMenuUiKey
{
    Key
}
