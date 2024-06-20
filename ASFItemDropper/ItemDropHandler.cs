using ArchiSteamFarm.Core;
using ArchiSteamFarm.Steam;
using SteamKit2;
using SteamKit2.Internal;
using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;

namespace ASFItemDropper;


public sealed class ItemDropHandler : ClientMsgHandler
{
    private SteamUnifiedMessages.UnifiedService<IInventory>? _inventoryService;
    private SteamUnifiedMessages.UnifiedService<IPlayer>? _PlayerService;

    ConcurrentDictionary<ulong, StoredResponse> Responses = new();

    public override void HandleMsg(IPacketMsg packetMsg)
    {
        var handler = Client.GetHandler<SteamUnifiedMessages>();

        if (packetMsg == null)
        {
            ASF.ArchiLogger.LogNullError(nameof(packetMsg));

            return;
        }

        switch (packetMsg.MsgType)
        {
            case EMsg.ClientGetUserStatsResponse:
                break;
            case EMsg.ClientStoreUserStatsResponse:
                break;
        }

    }


    internal string ItemIdleingStart(Bot bot, uint appid)
    {
        var response = new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayed);
        response.Body.games_played.Add(new CMsgClientGamesPlayed.GamePlayed
        {
            game_id = new GameID(appid),
            steam_id_gs = bot.SteamID
            //  steam_id_for_user = bot.SteamID

        });

        Client.Send(response);
        return "Start idling for " + appid;
    }

    internal string ItemIdleingStop(Bot bot)
    {
        var response = new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayed);
        {
            response.Body.games_played.Add(new CMsgClientGamesPlayed.GamePlayed { game_id = 0 });
        }

        Client.Send(response);
        return "Stop idling ";
    }

    internal async Task<string> CheckTime(uint appid, uint itemdefid, Bot bot, bool longoutput)
    {

        var steamUnifiedMessages = Client.GetHandler<SteamUnifiedMessages>();
        if (steamUnifiedMessages == null)
        {
            bot.ArchiLogger.LogNullError(nameof(steamUnifiedMessages));
            return "SteamUnifiedMessages Error";
        }

        var playtimeRequest = new CInventory_ConsumePlaytime_Request { appid = appid, itemdefid = itemdefid };
        _inventoryService = steamUnifiedMessages.CreateService<IInventory>();
        var playtimeResponse = await _inventoryService.SendMessage(x => x.ConsumePlaytime(playtimeRequest));
        var resultGamesPlayed = playtimeResponse.GetDeserializedResponse<CInventory_Response>();


        if (resultGamesPlayed.item_json == null) bot.ArchiLogger.LogGenericWarning(message: $"{resultGamesPlayed.item_json}");
        if (resultGamesPlayed == null) bot.ArchiLogger.LogNullError("resultGamesPlayed");


        var gamesOwnedRequest = new CPlayer_GetOwnedGames_Request { steamid = bot.SteamID, include_appinfo = true, include_free_sub = true, include_played_free_games = true };
        _PlayerService = steamUnifiedMessages.CreateService<IPlayer>();
        var ownedReponse = await _PlayerService.SendMessage(x => x.GetOwnedGames(gamesOwnedRequest));
        var consumePlaytime = ownedReponse.GetDeserializedResponse<CPlayer_GetOwnedGames_Response>();
        consumePlaytime.games.ForEach(action => bot.ArchiLogger.LogGenericInfo(message: $"{action.appid} - {action.has_community_visible_stats} - {action.name} - {action.playtime_forever}"));
        var resultFilteredGameById = consumePlaytime.games.Find(game => game.appid == (int)appid);

        if (consumePlaytime.games == null) bot.ArchiLogger.LogNullError(nameof(consumePlaytime.games));
        if (resultFilteredGameById == null) bot.ArchiLogger.LogNullError("resultFilteredGameById ");

        var appidPlaytimeForever = 0;
        if (resultGamesPlayed != null && resultFilteredGameById != null)
        {
            bot.ArchiLogger.LogGenericDebug(message: $"Playtime for {resultFilteredGameById.name} is: {resultFilteredGameById.playtime_forever}");
            appidPlaytimeForever = resultFilteredGameById.playtime_forever;
        }


        // proceed only when the player has played the request game id
        if (resultGamesPlayed != null && resultGamesPlayed.item_json != "[]")

            try
            {
                var summstring = "";

                foreach (var item in ItemList.FromJson(resultGamesPlayed.item_json))
                {
                    if (longoutput)
                        summstring += $"Item drop @{item.StateChangedTimestamp} => i.ID: {appid}_{item.Itemid}, i.Def: {item.Itemdefid} (a.PT: {appidPlaytimeForever}m)";
                    else
                        summstring += $"Item drop @{item.StateChangedTimestamp}";

                    // item drop time taken from Steam, to be added to newline
                    var new_v0 = item.StateChangedTimestamp;

                    // Creating iDrop_Logfile if not exists and write a header
                    var iDrop_Logfile = "";
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                        // setting filename for Linux OS
                        iDrop_Logfile = $"plugins/ASFItemDropper/droplogs/{bot.BotName}_{appid}.log";
                    else
                        // setting filename for Windows OS
                        iDrop_Logfile = $"plugins\\ASFItemDropper\\droplogs\\{bot.BotName}_{appid}.log";

                    if (!File.Exists(iDrop_Logfile))
                        using (var sw = File.AppendText(iDrop_Logfile))
                        {
                            // writing header information and first dummy data
                            sw.WriteLine($"# Droplog for bot '{bot.BotName}' and AppID {appid} ({resultFilteredGameById?.name})");
                            sw.WriteLine($"# Timestamp;TimeDiff;Playtime;PlaytimeDiff");
                            sw.WriteLine($"{new_v0};0.00:00:00;0;0");
                        }

                    var newline = "";
                    var lastline = File.ReadLines(iDrop_Logfile).Last();
                    var old_values = lastline.Split(';', StringSplitOptions.TrimEntries);

                    // date format of item drop time from Steam, needed for converting
                    var format = "yyyyMMdd'T'HHmmss'Z'";

                    // converting item drop times back to UTC for later calculation
                    var new_v0utc = DateTime.ParseExact(new_v0, format, CultureInfo.InvariantCulture);
                    var old_v0utc = DateTime.ParseExact(old_values[0], format, CultureInfo.InvariantCulture);

                    // calculating difference between last two item drops (newline - lastline)
                    var duration = new_v0utc.Subtract(old_v0utc);
                    var new_v1 = duration.ToString(@"d\.hh\:mm\:ss", CultureInfo.InvariantCulture);

                    // setting and converting appidPlaytimeForever of game for later calculation
                    var new_v2 = Convert.ToUInt32(appidPlaytimeForever);

                    // calculating the playtime difference from newline to lastline
                    var new_v3 = new_v2 - Convert.ToUInt32(old_values[2]);

                    // setup and append newline to droplogfile
                    newline = $"{new_v0};{new_v1};{new_v2};{new_v3}";

                    using (var sw = File.AppendText(iDrop_Logfile))
                        sw.WriteLine($"{newline}");
                }
                return summstring;
            }
            catch (Exception e)
            {
                bot.ArchiLogger.LogGenericError(message: e.Message);
                return "Error while parse consumePlaytime";
            }
        else

            if (longoutput)
            return $"No item drop for game '{resultFilteredGameById?.name}' with playtime {appidPlaytimeForever}m.";
        else
            return $"No item drop.";
    }

    internal async Task<string> CheckPlaytime(uint appid, Bot bot)
    {

        var steamUnifiedMessages = Client.GetHandler<SteamUnifiedMessages>();
        if (steamUnifiedMessages == null)
        {
            bot.ArchiLogger.LogNullError(nameof(steamUnifiedMessages));
            return "SteamUnifiedMessages Error";
        }

        var gamesOwnedRequest = new CPlayer_GetOwnedGames_Request { steamid = bot.SteamID, include_appinfo = true, include_free_sub = true, include_played_free_games = true };
        _PlayerService = steamUnifiedMessages.CreateService<IPlayer>();
        var ownedReponse = await _PlayerService.SendMessage(x => x.GetOwnedGames(gamesOwnedRequest));
        var consumePlaytime = ownedReponse.GetDeserializedResponse<CPlayer_GetOwnedGames_Response>();
        consumePlaytime.games.ForEach(action => bot.ArchiLogger.LogGenericInfo(message: $"{action.appid} - {action.has_community_visible_stats} - {action.name} - {action.playtime_forever}"));
        var resultFilteredGameById = consumePlaytime.games.Find(game => game.appid == (int)appid);

        if (consumePlaytime.games == null) bot.ArchiLogger.LogNullError(nameof(consumePlaytime.games));
        if (resultFilteredGameById == null) bot.ArchiLogger.LogNullError("resultFilteredGameById");

        uint appidPlaytimeForever = 0;
        bot.ArchiLogger.LogGenericDebug(message: $"Playtime for {resultFilteredGameById?.name} is: {resultFilteredGameById?.playtime_forever}");
        appidPlaytimeForever = Convert.ToUInt32(resultFilteredGameById.playtime_forever);
        var appidPlaytimeHours = appidPlaytimeForever / 60;
        var appidPlaytimeMinutes = Convert.ToByte(appidPlaytimeForever % 60);
        var PTMinutes = appidPlaytimeForever.ToString("N0", CultureInfo.InvariantCulture);
        var PTHours = appidPlaytimeHours.ToString("N0", CultureInfo.InvariantCulture);

        var summstring = "";
        summstring += $"Playtime for game '{resultFilteredGameById.name}' is {PTMinutes}m = {PTHours}h {appidPlaytimeMinutes}m";

        return summstring;
    }

    internal string ItemDropDefList(Bot bot)
    {
        var response = new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayed);

        var IDDL_File = "";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            // setting filename for Linux OS
            IDDL_File = $"plugins/ASFItemDropper/idropdeflist.txt";
        else
            // setting filename for Windows OS
            IDDL_File = $"plugins\\ASFItemDropper\\idropdeflist.txt";
        var idropdeflist_txt = "";

        var fileExists = File.Exists(IDDL_File);

        if (fileExists)
        {
            idropdeflist_txt = "\n";
            idropdeflist_txt += File.ReadAllText(IDDL_File);
        }
        else
            idropdeflist_txt = "## INFO: File 'idropdeflist.txt' does not exist.";

        return idropdeflist_txt;
    }

    // Testing section for checking new feature as single/independent command
    internal string ItemDropTest(uint appid, Bot bot)
    {
        // Creating iDrop_Logfile if not exists and write a header
        var iDrop_Logfile = $"plugins\\ASFItemDropper\\droplogs\\{bot.BotName}_{appid}.log";

        if (!File.Exists(iDrop_Logfile))
            using (var sw = File.AppendText(iDrop_Logfile))
            {
                //sw.WriteLine($"# Droplog for bot '{bot.BotName}' and AppID {appid} ({resultFilteredGameById.name})");
                sw.WriteLine($"# Droplog for bot '{bot.BotName}' and AppID {appid}");
                sw.WriteLine($"# Timestamp;TimeDiff;Playtime;PlaytimeDiff");
            }

        var newline = "";
        var lastline = File.ReadLines(iDrop_Logfile).Last();
        var old_values = lastline.Split(';', StringSplitOptions.TrimEntries);

        //DateTime nowutc = DateTime.UtcNow;

        // date format to make it look like Droptime from Steam
        var format = "yyyyMMdd'T'HHmmss'Z'";
        // new dummy timestamp for to be added newline
        var new_v0 = DateTime.UtcNow.ToString(format);

        // converting dummy back to UTC for later
        var new_v0utc = DateTime.ParseExact(new_v0, format, CultureInfo.InvariantCulture);

        // check if lastline from droplogfile is a comment, means still no dropdata in
        if (lastline.Substring(0, 1) == "#")
            // using real overallplaytime from itemDrop
            //uint new_v2 = Convert.ToUInt32(old_values[2]);
            using (var sw = File.AppendText(iDrop_Logfile))
                // writing first dummy data
                sw.WriteLine($"{new_v0};0.00:00:00;0;0");
        else
        {
            // generating some random playtime for newline
            // sum of old_playtime + random
            var rnd = new Random();
            var new_v2 = Convert.ToUInt32(Convert.ToUInt32(old_values[2]) + rnd.Next(10, 50));

            // calculating the playtime diff from new to old
            var new_v3 = new_v2 - Convert.ToUInt32(old_values[2]);

            var old_v0utc = DateTime.ParseExact(old_values[0], format, CultureInfo.InvariantCulture);
            var duration = new_v0utc.Subtract(old_v0utc);
            //string new_v1 = duration.ToString();
            //string new_v1 = duration.ToString("G",CultureInfo.InvariantCulture);
            // best human readable format
            var new_v1 = duration.ToString(@"d\.hh\:mm\:ss", CultureInfo.InvariantCulture);


            // setup newline to be append to droplogfile
            newline = $"{new_v0};{new_v1};{new_v2};{new_v3}";
            using var sw = File.AppendText(iDrop_Logfile);
            sw.WriteLine($"{newline}");
        }

        // just some out to see the values on console or chat window
        var RC = "\n";
        RC += $"Lastline: {lastline}\n";
        RC += $"Newline : {newline}\n";
        RC += $"{new_v0utc}\n";
        RC += $"{new_v0}\n";

        return RC;
    }

}
