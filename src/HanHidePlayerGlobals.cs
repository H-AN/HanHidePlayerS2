using SwiftlyS2.Shared.Players;
using FreeSql.DataAnnotations;

namespace HanHidePlayerS2;

public class HanHidePlayerGlobals
{
    public HashSet<int> hideEnabled = new();

    public Dictionary<int, HashSet<int>> blockMap = new();

    public float maxHideDistance = 500f;

    public Dictionary<int, float> PlayerHideDistance = new();
    public Dictionary<int, bool> PlayerdistanceHideEnabled = new();
    public Dictionary<int, bool> PlayerdButtonHideEnabled = new();
    public Dictionary<int, PlayerSettings> PendingSettings { get; set; } = new();

    public Dictionary<ulong, IPlayer> Players = new();

    public readonly object SaveLock = new();

    public readonly Dictionary<int, DateTime> LastCommandTime = new();


}

public class PlayerSettings
{
    [Column(IsPrimary = true)]
    public ulong SteamId { get; set; }

    public bool HideAll { get; set; } = false;
    public bool DistanceHide { get; set; } = false;
    public bool ButtonHide { get; set; } = true;
    public float HideDistance { get; set; } = 500f;

    public static PlayerSettings CreateDefault(float defaultDistance)
    {
        return new PlayerSettings
        {
            HideAll = false,
            DistanceHide = false,
            ButtonHide = true,
            HideDistance = defaultDistance
        };
    }

}

public enum HideMode
{
    None,       // ²»Òþ²Ø
    HideAll,    // È«¾ÖÒþ²Ø
    KeyToggle,  // °´¼üÇÐ»»Òþ²Ø
    Distance    // ¾àÀëÒþ²Ø
}
