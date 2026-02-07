using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Modules.Utils;

using RetakesPlugin.Configs.JsonConverters;
using RetakesPlugin.Enums;
using RetakesPluginShared.Enums;

namespace RetakesPlugin.Models;

public class Grenade
{
    public string? Name { get; set; }
    
    public EGrenade Type { get; set; }

    [JsonConverter(typeof(VectorJsonConverter))]
    public Vector? Position { get; set; }

    [JsonConverter(typeof(QAngleJsonConverter))]
    public QAngle? Angle { get; set; }

    [JsonConverter(typeof(VectorJsonConverter))]
    public Vector? Velocity { get; set; }

    public CsTeam Team { get; set; }
    
    public Bombsite Bombsite { get; set; }
    
    public float Delay { get; set; }
}
