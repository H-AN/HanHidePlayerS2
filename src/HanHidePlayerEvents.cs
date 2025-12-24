using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Mono.Cecil.Cil;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.SteamAPI;
using static Dapper.SqlMapper;

namespace HanHidePlayerS2;

public class HanHidePlayerEvents
{
    private readonly ISwiftlyCore _core;
    private readonly ILogger<HanHidePlayerEvents> _logger;
    private readonly HanHidePlayerGlobals _globals;
    private readonly HanHidePlayerHelpers _helpers;
    private readonly HanHidePlayerDatabase _database;
    public HanHidePlayerEvents(ISwiftlyCore core,
        ILogger<HanHidePlayerEvents> logger,
        HanHidePlayerGlobals globals, HanHidePlayerHelpers helpers,
        HanHidePlayerDatabase database)
    {
        _core = core;
        _logger = logger;
        _globals = globals;
        _helpers = helpers;
        _database = database;
    }

    public void HookEvents()
    {
        _core.Event.OnClientConnected += Event_OnClientConnected;
        _core.Event.OnTick += Event_OnTick;
        _core.Event.OnClientKeyStateChanged += Event_OnClientKeyStateChanged;

        _core.Event.OnMapUnload += Event_OnMapUnload;
        _core.Event.OnClientDisconnected += Event_OnClientDisconnected;

        _core.Event.OnClientSteamAuthorize += Event_OnClientSteamAuthorize;
    }

    private void Event_OnClientSteamAuthorize(IOnClientSteamAuthorizeEvent @event)
    {
        var player = _core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player == null)
            return;

        _globals.Players[player.SteamID] = player;

        Task.Run(() => LoadPlayerSettingsAsync(player));
    }

    private async Task LoadPlayerSettingsAsync(IPlayer player)
    {
        if (player.SteamID == 0)
        {
            _logger.LogWarning($"{_core.Localizer["DataSteamError", player.Controller.PlayerName]}");
            return;
        }

        ulong steamId = player.SteamID;
        var settings = await _database.LoadPlayerAsync(steamId);

        if (settings == null)
        {
            settings = PlayerSettings.CreateDefault(_globals.maxHideDistance);
            settings.SteamId = steamId;

            _logger.LogInformation($"{_core.Localizer["DataNewPlayer", player.Controller.PlayerName]}");
            await _database.SavePlayerAsync(settings);
        }

        lock (_globals.PendingSettings)
        {
            _globals.PendingSettings[player.PlayerID] = settings;
        }

        ApplySettingsToGlobals(player.PlayerID, settings);
    }



    private void Event_OnClientConnected(SwiftlyS2.Shared.Events.IOnClientConnectedEvent @event)
    {
        var player = _core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player == null || !player.IsValid)
            return;

        if (player.IsFakeClient)
            return;

        if (!_globals.PlayerdistanceHideEnabled.ContainsKey(player.PlayerID))
            _globals.PlayerdistanceHideEnabled[player.PlayerID] = false;

        if (!_globals.PlayerdButtonHideEnabled.ContainsKey(player.PlayerID))
            _globals.PlayerdButtonHideEnabled[player.PlayerID] = true;

        if (!_globals.PlayerHideDistance.ContainsKey(player.PlayerID))
            _globals.PlayerHideDistance[player.PlayerID] = _globals.maxHideDistance;

        if (!_globals.blockMap.ContainsKey(player.PlayerID))
            _globals.blockMap[player.PlayerID] = new HashSet<int>();


        lock (_globals.PendingSettings)
        {
            if (_globals.PendingSettings.TryGetValue(player.PlayerID, out var settings))
            {
                ApplySettingsToGlobals(player.PlayerID, settings);
                _globals.PendingSettings.Remove(player.PlayerID);
            }
        }
    }

    private void ApplySettingsToGlobals(int playerId, PlayerSettings settings)
    {
        _globals.PlayerdistanceHideEnabled[playerId] = settings.DistanceHide;
        _globals.PlayerdButtonHideEnabled[playerId] = settings.ButtonHide;
        _globals.PlayerHideDistance[playerId] = settings.HideDistance;

        if (settings.HideAll)
            _globals.hideEnabled.Add(playerId);
        else
            _globals.hideEnabled.Remove(playerId);
    }

    private void Event_OnTick()
    {
        var players = _core.PlayerManager.GetAllPlayers();
        foreach (var viewer in players)
        {
            if (viewer == null || !viewer.IsValid || viewer.IsFakeClient)
                continue;

            var viewerPawn = viewer.PlayerPawn;
            if (viewerPawn == null || !viewerPawn.IsValid)
                continue;

            if (!_globals.blockMap.ContainsKey(viewer.PlayerID))
                _globals.blockMap[viewer.PlayerID] = new HashSet<int>();

            var hideSet = _globals.blockMap[viewer.PlayerID];
            hideSet.Clear();

            foreach (var target in players)
            {
                if (target == null || !target.IsValid)
                    continue;

                if (target.PlayerID == viewer.PlayerID)
                    continue;

                var targetPawn = target.PlayerPawn;
                if (targetPawn == null || !targetPawn.IsValid)
                    continue;

                bool shouldHide = false;

                if (_globals.hideEnabled.Contains(viewer.PlayerID) && targetPawn.TeamNum == viewerPawn.TeamNum)
                {
                    shouldHide = true;
                }

                if (_globals.PlayerdistanceHideEnabled.GetValueOrDefault(viewer.PlayerID, false) && targetPawn.TeamNum == viewerPawn.TeamNum)
                {
                    var viewerPos = viewerPawn.AbsOrigin;
                    if (viewerPos == null)
                        continue;

                    var targetPos = targetPawn.AbsOrigin;
                    if (targetPos == null)
                        continue;

                    float distanceSqr = _helpers.DistanceSquared(viewerPos.Value, targetPos.Value);
                    float distance = _globals.PlayerHideDistance.GetValueOrDefault(viewer.PlayerID, _globals.maxHideDistance);
                    if (distanceSqr <= distance * distance)
                        shouldHide = true;
                }

                if (shouldHide)
                    hideSet.Add(target.PlayerID);
            }

            foreach (var target in players)
            {
                if (target == null || !target.IsValid || target.PlayerID == viewer.PlayerID)
                    continue;

                var targetPawn = target.PlayerPawn;
                if (targetPawn == null || !targetPawn.IsValid)
                    continue;

                if (hideSet.Contains(target.PlayerID))
                {
                    if (targetPawn.IsTransmitting(viewer.PlayerID))
                        targetPawn.SetTransmitState(false, viewer.PlayerID);
                }
                else
                {
                    if (!targetPawn.IsTransmitting(viewer.PlayerID))
                        targetPawn.SetTransmitState(true, viewer.PlayerID);
                }
            }
        }
    }

    private void Event_OnClientKeyStateChanged(SwiftlyS2.Shared.Events.IOnClientKeyStateChangedEvent @event)
    {
        var player = _core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player == null || !player.IsValid || player.IsFakeClient)
            return;

        if (!_globals.PlayerdButtonHideEnabled.GetValueOrDefault(player.PlayerID, true))
            return;

        if ((player.PressedButtons & GameButtonFlags.Mouse2) != 0)
        {
            if (_globals.hideEnabled.Contains(player.PlayerID))
            {
                _globals.hideEnabled.Remove(player.PlayerID);
            }
            else
            {
                _globals.hideEnabled.Add(player.PlayerID);
            }
        }
    }

    private void Event_OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        var player = _core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player == null)
            return;

        _globals.Players.Remove(player.SteamID);
        _globals.hideEnabled.Remove(player.PlayerID);
        _globals.blockMap.Remove(player.PlayerID);
        _globals.PlayerHideDistance.Remove(player.PlayerID);
        _globals.PlayerdistanceHideEnabled.Remove(player.PlayerID);
        _globals.PlayerdButtonHideEnabled.Remove(player.PlayerID);

    }

    private void Event_OnMapUnload(IOnMapUnloadEvent @event)
    {
        _globals.hideEnabled.Clear();
        _globals.blockMap.Clear();
        _globals.PlayerHideDistance.Clear();
        _globals.PlayerdistanceHideEnabled.Clear();
        _globals.PlayerdButtonHideEnabled.Clear();
    }


}