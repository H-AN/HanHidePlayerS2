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

    /* 状态堆叠可能导致的服务器崩溃?  可以叠加三种状态的菜单
     * Could state stacking lead to server crashes?  The menu allows for stacking three different states.
     * */
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
            _core.Scheduler.NextTick(() =>
            {
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

                try
                {
                    Task.Run(() => _database.Save(player)); 
                }
                catch (Exception ex)
                {
                    _logger.LogError($"{_core.Localizer["SaveError", clicker.Controller.PlayerName, ex.Message]}");
                }


            });
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
            _core.Scheduler.NextTick(() =>
            {
                if (!clicker.IsValid)
                    return;

                bool current = _globals.PlayerdButtonHideEnabled.GetValueOrDefault(clicker.PlayerID, true);
                _globals.PlayerdButtonHideEnabled[clicker.PlayerID] = !current;

                try
                {
                    Task.Run(() => _database.Save(player));
                }
                catch (Exception ex)
                {
                    _logger.LogError($"{_core.Localizer["SaveError", clicker.Controller.PlayerName, ex.Message]}");
                }
            });
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
            _core.Scheduler.NextTick(() =>
            {
                if (!clicker.IsValid)
                    return;

                if (_globals.hideEnabled.Contains(clicker.PlayerID))
                {
                    _globals.hideEnabled.Remove(clicker.PlayerID);
                    clicker.ClearTransmitEntityBlocks();
                }

                bool current = _globals.PlayerdistanceHideEnabled.GetValueOrDefault(clicker.PlayerID, false);
                _globals.PlayerdistanceHideEnabled[clicker.PlayerID] = !current;

                try
                {
                    Task.Run(() => _database.Save(player));
                }
                catch (Exception ex)
                {
                    _logger.LogError($"{_core.Localizer["SaveError", clicker.Controller.PlayerName, ex.Message]}");
                }

            });
        };

        menu.AddOption(HideforDistance);

        _core.MenusAPI.OpenMenuForPlayer(player, menu);
        return menu;
    }

    /* 状态堆叠可能导致的服务器崩溃? 开启其中一项关闭其余项目 
     * Could state stacking lead to server crashes? Enable one option and disable the others.
     
    public IMenuAPI OpenHideMenu(IPlayer player)
    {
        var main = _core.MenusAPI.CreateBuilder();
        IMenuAPI menu = _menuhelper.CreateMenu($"{_core.Translation.GetPlayerLocalizer(player)["MenuTitle"]}");

        // 标题展示
        menu.AddOption(new TextMenuOption(HtmlGradient.GenerateGradientText(
            $"{_core.Translation.GetPlayerLocalizer(player)["MenuSelectHideType"]}",
            Color.Red, Color.LightBlue, Color.Red),
            updateIntervalMs: 500, pauseIntervalMs: 100)
        {
            TextStyle = MenuOptionTextStyle.ScrollLeftLoop
        });

        // 1. 全局隐藏按钮
        bool isAll = _globals.hideEnabled.Contains(player.PlayerID);
        string statusAll = isAll ? $"{_core.Translation.GetPlayerLocalizer(player)["MenuButtonOpen"]}" : $"{_core.Translation.GetPlayerLocalizer(player)["MenuButtonClose"]}";
        var HideAllButton = new ButtonMenuOption($"{_core.Translation.GetPlayerLocalizer(player)["MenuHideAll", statusAll]}")
        {
            TextStyle = MenuOptionTextStyle.ScrollLeftLoop,
            CloseAfterClick = true
        };
        HideAllButton.Click += (_, args) =>
        {
            SwitchHideMode(args.Player, isAll ? "None" : "All");
            return ValueTask.CompletedTask; // 显式返回已完成的任务
        };
        menu.AddOption(HideAllButton);

        // 2. 按键隐藏按钮
        bool isBtn = _globals.PlayerdButtonHideEnabled.GetValueOrDefault(player.PlayerID, false);
        string statusBtn = isBtn ? $"{_core.Translation.GetPlayerLocalizer(player)["MenuButtonOpen"]}" : $"{_core.Translation.GetPlayerLocalizer(player)["MenuButtonClose"]}";
        var HideforButton = new ButtonMenuOption($"{_core.Translation.GetPlayerLocalizer(player)["MenuButtonHide", statusBtn]}")
        {
            TextStyle = MenuOptionTextStyle.ScrollLeftLoop,
            CloseAfterClick = true
        };
        HideforButton.Click += (_, args) =>
        {
            SwitchHideMode(args.Player, isBtn ? "None" : "Button");
            return ValueTask.CompletedTask; // 显式返回已完成的任务
        };
        menu.AddOption(HideforButton);

        // 3. 距离隐藏按钮
        bool isDist = _globals.PlayerdistanceHideEnabled.GetValueOrDefault(player.PlayerID, false);
        float distance = _globals.PlayerHideDistance.GetValueOrDefault(player.PlayerID, _globals.maxHideDistance);
        string statusDist = isDist ? $"{_core.Translation.GetPlayerLocalizer(player)["MenuButtonOpen"]}" : $"{_core.Translation.GetPlayerLocalizer(player)["MenuButtonClose"]}";
        var HideforDistance = new ButtonMenuOption($"{_core.Translation.GetPlayerLocalizer(player)["MenuDistHide", statusDist, distance]}")
        {
            TextStyle = MenuOptionTextStyle.ScrollLeftLoop,
            CloseAfterClick = true
        };
        HideforDistance.Click += (_, args) =>
        {
            SwitchHideMode(args.Player, isDist ? "None" : "Distance");
            return ValueTask.CompletedTask; // 显式返回已完成的任务
        };
        menu.AddOption(HideforDistance);

        _core.MenusAPI.OpenMenuForPlayer(player, menu);
        return menu;
    }

    private void SwitchHideMode(IPlayer player, string modeType)
    {
        if (player == null || !player.IsValid) return;

        _core.Scheduler.NextTick(() =>
        {
            //先重置所有实体的传输封锁，防止状态叠加
            player.ClearTransmitEntityBlocks();

            //根据目标模式，更新互斥的状态变量
            switch (modeType)
            {
                case "All":
                    // 开启全局隐藏，关闭其他
                    if (!_globals.hideEnabled.Contains(player.PlayerID))
                        _globals.hideEnabled.Add(player.PlayerID);

                    _globals.PlayerdistanceHideEnabled[player.PlayerID] = false;
                    _globals.PlayerdButtonHideEnabled[player.PlayerID] = false;
                    player.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["HideAllMsgOn"]}");
                    break;

                case "Distance":
                    // 开启距离隐藏，关闭其他
                    _globals.hideEnabled.Remove(player.PlayerID);
                    _globals.PlayerdistanceHideEnabled[player.PlayerID] = true;
                    _globals.PlayerdButtonHideEnabled[player.PlayerID] = false;
                    break;

                case "Button":
                    // 开启按键隐藏，关闭其他
                    _globals.hideEnabled.Remove(player.PlayerID);
                    _globals.PlayerdistanceHideEnabled[player.PlayerID] = false;
                    _globals.PlayerdButtonHideEnabled[player.PlayerID] = true;
                    break;

                case "None":
                default:
                    // 全部关闭
                    _globals.hideEnabled.Remove(player.PlayerID);
                    _globals.PlayerdistanceHideEnabled[player.PlayerID] = false;
                    _globals.PlayerdButtonHideEnabled[player.PlayerID] = false;
                    player.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["HideAllMsgOff"]}");
                    break;
            }

            try
            {
                Task.Run(() => _database.Save(player));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Save Error for {player.Controller.PlayerName}: {ex.Message}");
            }
        });
    }

    */


}
