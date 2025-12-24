using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mono.Cecil.Cil;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;


namespace HanHidePlayerS2;


[PluginMetadata(
    Id = "HanHidePlayerS2",
    Version = "1.0.0",
    Name = "HanHidePlayerS2",
    Author = "H-AN",
    Description = "隐藏附近玩家 for Sw2/Hide Player for Sw2")]

public partial class HanHidePlayerS2(ISwiftlyCore core) : BasePlugin(core)
{
    private ServiceProvider? ServiceProvider { get; set; }
    private HanHidePlayerConfig _HanHidePlayerCFG = null!;
    private HanHidePlayerGlobals _Globals = null!;
    private HanHidePlayerEvents _Events = null!;
    private HanHidePlayerCommand _Commands = null!;
    public override void Load(bool hotReload)
    {
        Core.Configuration.InitializeJsonWithModel<HanHidePlayerConfig>("HanHidePlayerCFG.jsonc", "HanHidePlayerCFG").Configure(builder =>
        {
            builder.AddJsonFile("HanHidePlayerCFG.jsonc", false, true);
        });


        var collection = new ServiceCollection();
        collection.AddSwiftly(Core);

        collection
            .AddOptionsWithValidateOnStart<HanHidePlayerConfig>()
            .BindConfiguration("HanHidePlayerCFG");


        collection.AddSingleton<HanHidePlayerGlobals>();
        collection.AddSingleton<HanHidePlayerEvents>();
        collection.AddSingleton<HanHidePlayerHelpers>();
        collection.AddSingleton<HanHidePlayerDatabase>();
        collection.AddSingleton<HanHidePlayerCommand>();
        collection.AddSingleton<HanHidePlayerMenu>();
        collection.AddSingleton<HanHidePlayerMenuHelper>();

        ServiceProvider = collection.BuildServiceProvider();

        _Globals = ServiceProvider.GetRequiredService<HanHidePlayerGlobals>();
        _Events = ServiceProvider.GetRequiredService<HanHidePlayerEvents>();
        _Commands = ServiceProvider.GetRequiredService<HanHidePlayerCommand>();

        var monitor = ServiceProvider.GetRequiredService<IOptionsMonitor<HanHidePlayerConfig>>();
        _HanHidePlayerCFG = monitor.CurrentValue;

        if (_HanHidePlayerCFG.EnablePlugins)
        {
            _Commands.Commands();
            _Events.HookEvents();
        }
    }

    public override void Unload()
    {
        ServiceProvider?.Dispose();
    }

}