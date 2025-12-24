using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mono.Cecil.Cil;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;

namespace HanHidePlayerS2;

public class HanHidePlayerCommand
{
    private readonly ISwiftlyCore _core;
    private readonly ILogger<HanHidePlayerCommand> _logger;
    private readonly HanHidePlayerGlobals _globals;
    private readonly HanHidePlayerMenu _menu;
    private readonly HanHidePlayerDatabase _database;
    private readonly IOptionsMonitor<HanHidePlayerConfig> _config;
    public HanHidePlayerCommand(ISwiftlyCore core,
        ILogger<HanHidePlayerCommand> logger,
        HanHidePlayerGlobals globals,
        HanHidePlayerMenu menu,
        HanHidePlayerDatabase database,
        IOptionsMonitor<HanHidePlayerConfig> config)
    {
        _core = core;
        _logger = logger;
        _globals = globals;
        _menu = menu;
        _database = database;
        _config = config;
    }

    public void Commands()
    {

        string MenuCommand = string.IsNullOrEmpty(_config.CurrentValue.HideMenuCommand) ? "sw_hide" : _config.CurrentValue.HideMenuCommand;
        string HDCommand = string.IsNullOrEmpty(_config.CurrentValue.HidedistCommand) ? "sw_hd" : _config.CurrentValue.HidedistCommand;
        _core.Command.RegisterCommand($"{MenuCommand}", OnHidePlayer, true);
        _core.Command.RegisterCommand($"{HDCommand}", SetHideDistance, true);
    }

    private void OnHidePlayer(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null || !player.IsValid)
            return;

        _menu.OpenHideMenu(player);

    }

    private void SetHideDistance(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null || !player.IsValid)
            return;

        if (_globals.LastCommandTime.TryGetValue(player.PlayerID, out var lastTime))
        {
            if ((DateTime.Now - lastTime).TotalSeconds < 10)
            {
                var remaining = 10 - (int)(DateTime.Now - lastTime).TotalSeconds;
                player.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["CommandColdDown", remaining]}");
                return;
            }
        }

        if (context.Args.Length <= 0)
        {
            player.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["CommandHD"]}");
            return;
        }

        if (!int.TryParse(context.Args[0], out int dist) || dist <= 0 || dist >= 2000)
        {
            player.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["CommandHDDist"]}");
            return;
        }

        float Distance = (float)dist;
        _globals.PlayerHideDistance[player.PlayerID] = Distance;

        player.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["HDDistMessage", Distance]}");

        try
        {
            Task.Run(() => _database.Save(player));
        }
        catch (Exception ex)
        {
            _logger.LogError($"{_core.Localizer["SaveError", player.Controller.PlayerName, ex.Message]}");
        }
    }



}



