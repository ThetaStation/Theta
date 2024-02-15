using Content.Server.Shuttles.Systems;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;

namespace Content.Server.Theta.MapGen.Processors;

/// <summary>
/// Processor which adds specified IFF flags onto processed grid
/// </summary>
public sealed partial class FlagIFFProcessor : IMapGenProcessor
{
    [DataField("flags", required: true)]
    public List<IFFFlags> Flags = new();

    /// <summary>
    /// If set, will clear all old flags which were present on processed grid
    /// </summary>
    [DataField("resetOldFlags")]
    public bool ResetOldFlags;

    [DataField("nameOverride")]
    public string? NameOverride;

    [DataField("colorOverride")]
    public Color? ColorOverride;

    public void Process(MapGenSystem sys, MapId targetMap, EntityUid gridUid, bool isGlobal)
    {
        var shuttleSys = sys.EntMan.System<ShuttleSystem>();
        if (isGlobal)
        {
            foreach (var childGridUid in sys.SpawnedGrids)
            {
                ApplyFlags(sys.EntMan, shuttleSys, childGridUid);
            }
        }
        else
        {
            ApplyFlags(sys.EntMan, shuttleSys, gridUid);
        }
    }

    public void ApplyFlags(IEntityManager entMan, ShuttleSystem shuttleSys, EntityUid gridUid)
    {
        var iffComp = entMan.EnsureComponent<IFFComponent>(gridUid);

        if (ResetOldFlags)
            shuttleSys.ResetIFFFlags(gridUid, iffComp);

        if (NameOverride != null)
        {
            IoCManager.Resolve<MetaDataSystem>().SetEntityName(gridUid, NameOverride);
        }

        if(ColorOverride != null)
            shuttleSys.SetIFFColor(gridUid, ColorOverride.Value, iffComp);

        foreach (IFFFlags flag in Flags)
        {
            shuttleSys.AddIFFFlag(gridUid, flag, iffComp);
        }
    }
}
