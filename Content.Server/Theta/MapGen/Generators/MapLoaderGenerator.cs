using System.Numerics;
using Robust.Server.Maps;
using Robust.Shared.Map;

namespace Content.Server.Theta.MapGen.Generators;

public sealed partial class MapLoaderGenerator : IMapGenGenerator
{
    [DataField("mapPath", required: true)]
    public string MapPath = "";

    public IEnumerable<EntityUid> Generate(MapGenSystem sys, MapId targetMap)
    {
        var loadOptions = new MapLoadOptions
        {
            Rotation = sys.Random.NextAngle(),
            Offset = Vector2.Zero,
            LoadMap = false
        };

        if (sys.MapLoader.TryLoad(targetMap, MapPath, out var rootUids, loadOptions))
            return rootUids;

        return new List<EntityUid>();
    }
}
