using SteamKit2.Internal;

namespace ASFItemDropper;

class StoredResponse
{
    public bool Success { get; set; }
    public CMsgClientGetUserStatsResponse? Response { get; set; }
}
