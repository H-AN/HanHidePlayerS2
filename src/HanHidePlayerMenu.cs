using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mono.Cecil.Cil;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.SteamAPI;

namespace HanHidePlayerS2;

public class HanHidePlayerMenu
{
    private readonly ILogger<HanHidePlayerMenu> _logger;
    private readonly ISwiftlyCore _core;
    private readonly HanHidePlayerMenuHelper _menuhelper;
    private readonly HanHidePlayerGlobals _globals;
    private readonly HanHidePlayerDatabase _database;
    public HanHidePlayerMenu(ISwiftlyCore core, ILogger<HanHidePlayerMenu> logger
        ,HanHidePlayerMenuHelper menuhelper, HanHidePlayerGlobals globals,
        HanHidePlayerDatabase database)
    {
        _core = core;
        _logger = logger;
        _menuhelper = menuhelper;
        _globals = globals;
        _database = database;
    }

    public IMenuAPI OpenHideMenu(IPlayer player)
    {
        var main = _core.MenusAPI.CreateBuilder();
        IMenuAPI menu = _menuhelper.CreateMenu($"{_core.Translation.GetPlayerLocalizer(player)["MenuTitle"]}");

        menu.AddOption(new TextMenuOption(HtmlGradient.GenerateGradientText(
            $"{_core.Translation.GetPlayerLocalizer(player)["MenuSelectHideType"]}",
            Color.Red, Color.LightBlue, Color.Red),
            updateIntervalMs: 500, pauseIntervalMs: 100)
        {
            TextStyle = MenuOptionTextStyle.ScrollLeftLoop
        });

        string statusAll = _globals.hideEnabled.Contains(player.PlayerID) ?
        $"{_core.Translation.GetPlayerLocalizer(player)["MenuButtonOpen"]}" : $"{_core.Translation.GetPlayerLocalizer(player)["MenuButtonClose"]}";
        var HideAllButton = new ButtonMenuOption($"{_core.Translation.GetPlayerLocalizer(player)["MenuHideAll", statusAll]}")
        {
            TextStyle = MenuOptionTextStyle.ScrollLeftLoop,
            CloseAfterClick = true
        };
        HideAllButton.Tag = "extend";

        HideAllButton.Click += async (_, args) =>
        {
            var clicker = args.Player;
            if (!clicker.IsValid)
                return;

            _globals.PlayerdistanceHideEnabled[clicker.PlayerID] = false;

            if (_globals.hideEnabled.Contains(clicker.PlayerID))
            {
                _globals.hideEnabled.Remove(clicker.PlayerID);
                clicker.ClearTransmitEntityBlocks();
                clicker.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["HideAllMsgOn"]}");
            }
            else
            {
                _globals.hideEnabled.Add(clicker.PlayerID);
                clicker.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["HideAllMsgOff"]}");
            }

            ulong sID = clicker.SteamID;
            int pID = clicker.PlayerID;
            _ = Task.Run(() => _database.Save(sID, pID));
        };

        menu.AddOption(HideAllButton);


        string statusBtn = _globals.PlayerdButtonHideEnabled.GetValueOrDefault(player.PlayerID, true) ?
        $"{_core.Translation.GetPlayerLocalizer(player)["MenuButtonOpen"]}" : $"{_core.Translation.GetPlayerLocalizer(player)["MenuButtonClose"]}";
        var HideforButton = new ButtonMenuOption($"{_core.Translation.GetPlayerLocalizer(player)["MenuButtonHide", statusBtn]}]")
        {
            TextStyle = MenuOptionTextStyle.ScrollLeftLoop,
            CloseAfterClick = true
        };
        HideforButton.Tag = "extend";

        HideforButton.Click += async (_, args) =>
        {
            var clicker = args.Player;
            if (!clicker.IsValid)
                return;

            bool current = _globals.PlayerdButtonHideEnabled.GetValueOrDefault(clicker.PlayerID, true);
            _globals.PlayerdButtonHideEnabled[clicker.PlayerID] = !current;

            ulong sID = clicker.SteamID;
            int pID = clicker.PlayerID;
            _ = Task.Run(() => _database.Save(sID, pID));
        };

        menu.AddOption(HideforButton);


        string statusDist = _globals.PlayerdistanceHideEnabled.GetValueOrDefault(player.PlayerID, false) ?
        $"{_core.Translation.GetPlayerLocalizer(player)["MenuButtonOpen"]}" : $"{_core.Translation.GetPlayerLocalizer(player)["MenuButtonClose"]}";
        float distance = _globals.PlayerHideDistance.GetValueOrDefault(player.PlayerID, _globals.maxHideDistance);
        var HideforDistance = new ButtonMenuOption($"{_core.Translation.GetPlayerLocalizer(player)["MenuDistHide", statusDist, distance]}")
        {
            TextStyle = MenuOptionTextStyle.ScrollLeftLoop,
            CloseAfterClick = true
        };
        HideforDistance.Tag = "extend";

        HideforDistance.Click += async (_, args) =>
        {
            var clicker = args.Player;
            if (!clicker.IsValid)
                return;

            if (_globals.hideEnabled.Contains(clicker.PlayerID))
            {
                _globals.hideEnabled.Remove(clicker.PlayerID);
                clicker.ClearTransmitEntityBlocks();
            }

            bool current = _globals.PlayerdistanceHideEnabled.GetValueOrDefault(clicker.PlayerID, false);
            _globals.PlayerdistanceHideEnabled[clicker.PlayerID] = !current;

            ulong sID = clicker.SteamID;
            int pID = clicker.PlayerID;
            _ = Task.Run(() => _database.Save(sID, pID));
        };

        menu.AddOption(HideforDistance);

        _core.MenusAPI.OpenMenuForPlayer(player, menu);
        return menu;
    }

}
