using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;

using RetakesPlugin.Enums;
using RetakesPlugin.Memory;
using RetakesPlugin.Models;
using RetakesPlugin.Services;
using RetakesPlugin.Utils;
using RetakesPluginShared.Enums;

using TimerFlags = CounterStrikeSharp.API.Modules.Timers.TimerFlags;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace RetakesPlugin.Managers;

public sealed class GrenadeManager
{
    private readonly MapConfigService _mapConfigService;
    private readonly Dictionary<Bombsite, Dictionary<CsTeam, List<Grenade>>> _grenades = new();
    private readonly Random _random = new();

    public GrenadeManager(MapConfigService mapConfigService)
    {
        _mapConfigService = mapConfigService;
        CalculateMapGrenades();
        Logger.LogInfo("GrenadeManager", "Grenade manager initialized");
    }

    public void CalculateMapGrenades()
    {
        _grenades.Clear();

        _grenades.Add(Bombsite.A, new Dictionary<CsTeam, List<Grenade>>()
        {
            { CsTeam.Terrorist, [] },
            { CsTeam.CounterTerrorist, [] }
        });
        _grenades.Add(Bombsite.B, new Dictionary<CsTeam, List<Grenade>>()
        {
            { CsTeam.Terrorist, [] },
            { CsTeam.CounterTerrorist, [] }
        });

        foreach (var grenade in _mapConfigService.GetGrenadesClone())
        {
            _grenades[grenade.Bombsite][grenade.Team].Add(grenade);
        }

        Logger.LogInfo("GrenadeManager", $"Map grenades calculated: A site T={_grenades[Bombsite.A][CsTeam.Terrorist].Count}, CT={_grenades[Bombsite.A][CsTeam.CounterTerrorist].Count}; B site T={_grenades[Bombsite.B][CsTeam.Terrorist].Count}, CT={_grenades[Bombsite.B][CsTeam.CounterTerrorist].Count}");
    }

    public void SetupGrenades(Bombsite bombsite)
    {
        var allGrenades = _grenades[bombsite];
        
        if (allGrenades[CsTeam.Terrorist].Count == 0 && allGrenades[CsTeam.CounterTerrorist].Count == 0)
        {
            Logger.LogDebug("GrenadeManager", $"No grenades configured for bombsite {bombsite}");
            return;
        }

        var freezeTimeDuration = 0f;
        var freezeTime = ConVar.Find("mp_freezetime");
        if (freezeTime != null)
        {
            freezeTimeDuration = freezeTime.GetPrimitiveValue<int>();
        }

        var teams = new List<CsTeam> { CsTeam.Terrorist, CsTeam.CounterTerrorist };
        var nadesThrown = new Dictionary<CsTeam, int>
        {
            { CsTeam.Terrorist, 0 },
            { CsTeam.CounterTerrorist, 0 },
        };

        foreach (var team in teams)
        {
            var teamPlayerCount = PlayerHelper.GetPlayerCount(team);
            
            foreach (var grenade in allGrenades[team])
            {
                // Limit grenades to number of players on team
                if (nadesThrown[team] >= teamPlayerCount)
                {
                    Logger.LogDebug("GrenadeManager", $"Skipping \"{grenade.Name}\" - not enough players on team");
                    continue;
                }

                var delay = freezeTimeDuration + grenade.Delay;
                new Timer(delay, () => ThrowGrenade(grenade), TimerFlags.STOP_ON_MAPCHANGE);
                nadesThrown[team]++;
                
                Logger.LogDebug("GrenadeManager", $"Scheduled grenade \"{grenade.Name}\" with delay {delay}s");
            }
        }

        Logger.LogInfo("GrenadeManager", $"Setup {nadesThrown[CsTeam.Terrorist] + nadesThrown[CsTeam.CounterTerrorist]} grenades for bombsite {bombsite}");
    }

    public void ThrowGrenade(Grenade grenade)
    {
        if (grenade.Position == null || grenade.Angle == null || grenade.Velocity == null)
        {
            Logger.LogWarning("GrenadeManager", $"Grenade \"{grenade.Name}\" has null position/angle/velocity");
            return;
        }

        CBaseCSGrenadeProjectile? createdGrenade = null;
        
        try
        {
            switch (grenade.Type)
            {
                case EGrenade.Smoke:
                {
                    createdGrenade = GrenadeFunctions.CSmokeGrenadeProjectile_CreateFunc.Invoke(
                        grenade.Position.Handle,
                        grenade.Angle.Handle,
                        grenade.Velocity.Handle,
                        grenade.Velocity.Handle,
                        IntPtr.Zero,
                        45,
                        (int)grenade.Team);
                    break;
                }
                case EGrenade.Molotov:
                case EGrenade.Incendiary:
                {
                    createdGrenade = GrenadeFunctions.CMolotovProjectile_CreateFunc.Invoke(
                        grenade.Position.Handle,
                        grenade.Angle.Handle,
                        grenade.Velocity.Handle,
                        grenade.Velocity.Handle,
                        IntPtr.Zero,
                        grenade.Type == EGrenade.Molotov ? 46 : 48);
                    break;
                }
                case EGrenade.HighExplosive:
                {
                    createdGrenade = GrenadeFunctions.CHEGrenadeProjectile_CreateFunc.Invoke(
                        grenade.Position.Handle,
                        grenade.Angle.Handle,
                        grenade.Velocity.Handle,
                        grenade.Velocity.Handle,
                        IntPtr.Zero,
                        44);
                    break;
                }
                case EGrenade.Decoy:
                {
                    createdGrenade = GrenadeFunctions.CDecoyProjectile_CreateFunc.Invoke(
                        grenade.Position.Handle,
                        grenade.Angle.Handle,
                        grenade.Velocity.Handle,
                        grenade.Velocity.Handle,
                        IntPtr.Zero,
                        47);
                    break;
                }
                case EGrenade.Flashbang:
                {
                    createdGrenade = Utilities.CreateEntityByName<CFlashbangProjectile>("flashbang_projectile");
                    if (createdGrenade == null)
                    {
                        Logger.LogWarning("GrenadeManager", "Failed to create flashbang projectile");
                        return;
                    }
                    createdGrenade.DispatchSpawn();
                    break;
                }
                default:
                    Logger.LogWarning("GrenadeManager", $"Unimplemented grenade type {grenade.Type}");
                    return;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("GrenadeManager", $"Failed to create grenade projectile: {ex.Message}");
            return;
        }

        if (createdGrenade != null && createdGrenade.DesignerName != "smokegrenade_projectile")
        {
            createdGrenade.Teleport(grenade.Position, grenade.Angle, grenade.Velocity);

            createdGrenade.InitialPosition.X = grenade.Position.X;
            createdGrenade.InitialPosition.Y = grenade.Position.Y;
            createdGrenade.InitialPosition.Z = grenade.Position.Z;

            createdGrenade.InitialVelocity.X = grenade.Velocity.X;
            createdGrenade.InitialVelocity.Y = grenade.Velocity.Y;
            createdGrenade.InitialVelocity.Z = grenade.Velocity.Z;

            createdGrenade.AngVelocity.X = grenade.Velocity.X;
            createdGrenade.AngVelocity.Y = grenade.Velocity.Y;
            createdGrenade.AngVelocity.Z = grenade.Velocity.Z;

            createdGrenade.TeamNum = (byte)grenade.Team;
        }

        Logger.LogInfo("GrenadeManager", $"Threw grenade \"{grenade.Name}\" ({grenade.Type})");
    }
}
