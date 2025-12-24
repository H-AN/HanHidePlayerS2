using System.Data;
using Dapper;
using FreeSql;
using McMaster.NETCore.Plugins;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mono.Cecil.Cil;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using System.Reflection;
using System.Runtime.InteropServices;



namespace HanHidePlayerS2;

public class HanHidePlayerDatabase
{
    private readonly ISwiftlyCore _core;
    private readonly ILogger<HanHidePlayerDatabase> _logger;
    private readonly HanHidePlayerGlobals _globals;
    private readonly string _dbPath;
    private IFreeSql? _fsql;
    public HanHidePlayerDatabase(ISwiftlyCore core,
        ILogger<HanHidePlayerDatabase> logger,
        HanHidePlayerGlobals globals)
    {
        _core = core;
        _logger = logger;
        _globals = globals;



        string configDir = _core.PluginDataDirectory; // Path.GetDirectoryName(_core.Configuration.GetConfigPath("HanHidePlayerCFG.jsonc"))!;
        _dbPath = Path.Combine(configDir, "HanHidePlayer.db");
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);

        StartSqlite();
    }


    public void StartSqlite()
    {
        try
        {
            SQLitePCL.Batteries.Init();

            _fsql = new FreeSql.FreeSqlBuilder()
                .UseConnectionString(DataType.Sqlite, $"Data Source={_dbPath}")
                .UseAutoSyncStructure(true)
                .Build();

            if (_fsql.Ado.ExecuteConnectTest())
            {
                _fsql.CodeFirst.SyncStructure<PlayerSettings>();
                _logger.LogInformation($"{_core.Localizer["DataSpawn", _dbPath]}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"{_core.Localizer["DataSpawnError", ex.Message]}");
            if (ex.Message.Contains("e_sqlite3"))
            {
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                _logger.LogError($"{_core.Localizer["DataFileCantFind", inner.Message]}");
            }
        }
    }

    public async Task<PlayerSettings?> LoadPlayerAsync(ulong steamId)
    {
        if (_fsql == null)
        {
            _logger.LogError($"{_core.Localizer["DataLoadError"]}");
            return null;
        }

        try
        {
            return await _fsql.Select<PlayerSettings>()
                .Where(p => p.SteamId == steamId)
                .ToOneAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError($"{_core.Localizer["DataLoadPlayerError", ex.Message]}");
            return null;
        }
    }

    public async Task SavePlayerAsync(PlayerSettings settings)
    {
        if (_fsql == null) return;

        await _fsql.InsertOrUpdate<PlayerSettings>()
            .SetSource(settings)
            .ExecuteAffrowsAsync();
    }

    public async Task Save(IPlayer player)
    {
        await SavePlayerSettingsAsync(player);
    }

    public async Task SavePlayerSettingsAsync(IPlayer player)
    {
        if (player == null || !player.IsValid) return;

        if (_fsql == null) return;

        var settings = new PlayerSettings
        {
            SteamId = player.SteamID,
            HideAll = _globals.hideEnabled.Contains(player.PlayerID),
            ButtonHide = _globals.PlayerdButtonHideEnabled.GetValueOrDefault(player.PlayerID, true),
            DistanceHide = _globals.PlayerdistanceHideEnabled.GetValueOrDefault(player.PlayerID, false),
            HideDistance = _globals.PlayerHideDistance.GetValueOrDefault(player.PlayerID, _globals.maxHideDistance)
        };

        await _fsql.InsertOrUpdate<PlayerSettings>()
            .SetSource(settings)
            .ExecuteAffrowsAsync();
    }



}