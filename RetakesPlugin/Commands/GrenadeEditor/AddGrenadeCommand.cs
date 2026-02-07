using System.Text.Json;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;

using RetakesPlugin.Enums;
using RetakesPlugin.Models;
using RetakesPlugin.Utils;
using RetakesPluginShared.Enums;

namespace RetakesPlugin.Commands.GrenadeEditor;

public class AddGrenadeCommand
{
    private readonly RetakesPlugin _plugin;
    private readonly JsonSerializerOptions _jsonOptions;

    public AddGrenadeCommand(RetakesPlugin plugin, JsonSerializerOptions jsonOptions)
    {
        _plugin = plugin;
        _jsonOptions = jsonOptions;
    }

    public void OnCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (!PlayerHelper.IsValid(player))
        {
            commandInfo.ReplyToCommand("[Retakes] You must be a player to execute this command.");
            return;
        }

        if (commandInfo.ArgCount < 3)
        {
            commandInfo.ReplyToCommand("[Retakes] Usage: css_addgrenade <T/CT> <A/B> [type] [name]");
            commandInfo.ReplyToCommand("[Retakes] Types: smoke, he, molotov, incendiary, flash, decoy");
            return;
        }

        var team = commandInfo.GetArg(1).ToUpper();
        if (team != "T" && team != "CT")
        {
            commandInfo.ReplyToCommand($"[Retakes] Invalid team '{team}'. Use T or CT.");
            return;
        }

        var bombsite = commandInfo.GetArg(2).ToUpper();
        if (bombsite != "A" && bombsite != "B")
        {
            commandInfo.ReplyToCommand($"[Retakes] Invalid bombsite '{bombsite}'. Use A or B.");
            return;
        }

        // Parse grenade type (default to smoke)
        var grenadeType = EGrenade.Smoke;
        if (commandInfo.ArgCount >= 4)
        {
            var typeArg = commandInfo.GetArg(3).ToLower();
            grenadeType = typeArg switch
            {
                "smoke" => EGrenade.Smoke,
                "he" or "hegrenade" or "highexplosive" => EGrenade.HighExplosive,
                "molotov" or "molo" => EGrenade.Molotov,
                "incendiary" or "inc" => EGrenade.Incendiary,
                "flash" or "flashbang" => EGrenade.Flashbang,
                "decoy" => EGrenade.Decoy,
                _ => EGrenade.Smoke
            };
        }

        // Parse name (optional)
        var name = commandInfo.ArgCount >= 5 ? commandInfo.GetArg(4) : "New Grenade";

        var pawn = player!.PlayerPawn.Value;
        if (pawn == null || pawn.AbsOrigin == null)
        {
            commandInfo.ReplyToCommand("[Retakes] Could not get player position.");
            return;
        }

        var grenade = new Grenade
        {
            Name = name,
            Type = grenadeType,
            Position = new Vector(pawn.AbsOrigin.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z),
            Angle = new QAngle(pawn.EyeAngles.X, pawn.EyeAngles.Y, pawn.EyeAngles.Z),
            Velocity = new Vector(-431.16f, -115.31f, 506.39f), // Default velocity - user needs to adjust
            Team = team == "T" ? CsTeam.Terrorist : CsTeam.CounterTerrorist,
            Bombsite = bombsite == "A" ? Bombsite.A : Bombsite.B,
            Delay = 0.0f
        };

        var json = JsonSerializer.Serialize(grenade, _jsonOptions);

        player.PrintToConsole("===========================================");
        player.PrintToConsole("GRENADE CONFIG - Copy this to your map config:");
        player.PrintToConsole("===========================================");
        player.PrintToConsole(json);
        player.PrintToConsole("===========================================");
        player.PrintToConsole("NOTE: Adjust 'Velocity' values for throw direction!");
        player.PrintToConsole("===========================================");

        commandInfo.ReplyToCommand($"[Retakes] Grenade config printed to console. Type: {grenadeType}, Team: {team}, Site: {bombsite}");

        Logger.LogInfo("AddGrenadeCommand", $"Generated grenade config for {name} at position {pawn.AbsOrigin}");
    }
}
