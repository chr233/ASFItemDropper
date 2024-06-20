using ArchiSteamFarm.Core;
using ArchiSteamFarm.Localization;
using ArchiSteamFarm.Plugins.Interfaces;
using ArchiSteamFarm.Steam;
using ArchiSteamFarm.Steam.Interaction;
using ASFItemDropper;
using SteamKit2;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Composition;

namespace ASFItemDropper;

[Export(typeof(IPlugin))]
// public sealed class ASFItemDropManager : IBotSteamClient, IBotCommand, IBotCardsFarmerInfo {
public sealed class ASFItemDropManager : IBotSteamClient, IBotCommand2
{
    private static ConcurrentDictionary<Bot, ItemDropHandler> ItemDropHandlers { get; } = new();

    public string Name => "ASF Item Dropper";

    public Version Version => typeof(ASFItemDropManager).Assembly.GetName().Version ?? new Version("0");

    public Task OnLoaded()
    {
        ASF.ArchiLogger.LogGenericInfo($"ASF Item Dropper Plugin v{Version} by webben | modified by chr_");

        // Creating iDrop_Logdir for storing item drop information
        string iDrop_Logdir = Path.Join("plugins", "ASFItemDropper", "droplogs");

        if (!Directory.Exists(iDrop_Logdir))
        {
            Directory.CreateDirectory(iDrop_Logdir);
        }

        return Task.CompletedTask;
    }

    public async Task<string?> OnBotCommand(Bot bot, EAccess access, string message, string[] args, ulong steamID = 0)
    {
        if (!Enum.IsDefined(access))
        {
            throw new InvalidEnumArgumentException(nameof(access), (int)access, typeof(EAccess));
        }

        switch (args[0].ToUpperInvariant())
        {
            // args.Length == 1base count of arguments
            // !istart bot1,bot2,bot3 218620
            //   cmd==Arg0  | arguments.length == 2 || arg[1] == bot1,bot2,bot3, arg[2] == 218620
            // !istart bot1,bot2,bot3 218620
            //   cmd  | arguments.length == 1

            // istart 218620 droplist
            case "ISTART" when args.Length == 3 && access >= EAccess.Master:
                return await StartItemIdle(bot, args[1], Utilities.GetArgsAsText(args, 2, ",")).ConfigureAwait(false);
            // istart bot1,bot2,bot3 218620 droplist
            case "ISTART" when args.Length == 4 && access >= EAccess.Master:
                return await StartItemIdle(args[1], args[2], Utilities.GetArgsAsText(args, 3, ",")).ConfigureAwait(false);
            // istop
            case "ISTOP" when args.Length == 1 && access >= EAccess.Master:
                return await StopItemIdle(bot).ConfigureAwait(false);
            // istop bot1,bot2,bot3
            case "ISTOP" when args.Length == 2 && access >= EAccess.Master:
                return await StopItemIdle(args[1]).ConfigureAwait(false);

            // idrop bot1,bot2,bot appid1 item1
            case "IDROP" when args.Length == 4 && access >= EAccess.Master:
                return await CheckItem(args[1], args[2], Utilities.GetArgsAsText(args, 3, ","), true).ConfigureAwait(false);
            // idrop appid1 item1
            case "IDROP" when args.Length == 3 && access >= EAccess.Master:
                return await CheckItem(bot, args[1], Utilities.GetArgsAsText(args, 2, ","), true).ConfigureAwait(false);

            // idrops bot1,bot2,bot appid1 item1
            case "IDROPS" when args.Length == 4 && access >= EAccess.Master:
                return await CheckItem(args[1], args[2], Utilities.GetArgsAsText(args, 3, ","), false).ConfigureAwait(false);
            // idrops appid1 item1
            case "IDROPS" when args.Length == 3 && access >= EAccess.Master:
                return await CheckItem(bot, args[1], Utilities.GetArgsAsText(args, 2, ","), false).ConfigureAwait(false);

            // idroppt bot1,bot2,bot appid1
            case "IDROPPT" when args.Length == 3 && access >= EAccess.Master:
                return await CheckPlaytime(args[1], args[2]).ConfigureAwait(false);
            // idroppt appid1
            case "IDROPPT" when args.Length == 2 && access >= EAccess.Master:
                return await CheckPlaytime(bot, args[1]).ConfigureAwait(false);

            // idropdeflist
            case "IDROPDEFLIST" when args.Length == 1 && access >= EAccess.Master:
                return await ItemDropDefList(bot).ConfigureAwait(false);
            // idropdeflist bot1,bot2
            case "IDROPDEFLIST" when args.Length == 2 && access >= EAccess.Master:
                return await ItemDropDefList(args[1]).ConfigureAwait(false);

            // idroptest bot1,bot2,bot appid1
            case "IDROPTEST" when args.Length == 3 && access >= EAccess.Master:
                return await ItemDropTest(args[1], args[2]).ConfigureAwait(false);
            // idroptest appid1
            case "IDROPTEST" when args.Length == 2 && access >= EAccess.Master:
                return await ItemDropTest(bot, args[1]).ConfigureAwait(false);

            default:
                return null;
        }
    }

    public Task OnBotSteamCallbacksInit(Bot bot, CallbackManager callbackManager)
    {
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<ClientMsgHandler>?> OnBotSteamHandlersInit(Bot bot)
    {
        ItemDropHandler CurrentBotItemDropHandler = new();
        ItemDropHandlers.TryAdd(bot, CurrentBotItemDropHandler);
        IReadOnlyCollection<ClientMsgHandler> result = [CurrentBotItemDropHandler];
        return Task.FromResult<IReadOnlyCollection<ClientMsgHandler>?>(result);
    }

    //Responses

    private static async Task<string?> StartItemIdle(Bot bot, string appid, string droplist)
    {
        if (!uint.TryParse(appid, out uint appId))
        {
            return bot.Commands.FormatBotResponse(string.Format(Strings.ErrorIsInvalid, nameof(appId)));
        }
        if (!ItemDropHandlers.TryGetValue(bot, out ItemDropHandler? ItemDropHandler))
        {
            return bot.Commands.FormatBotResponse(string.Format(Strings.ErrorIsEmpty, nameof(ItemDropHandlers)));
        }

        return bot.Commands.FormatBotResponse(await Task.Run<string>(() => ItemDropHandler.ItemIdleingStart(bot, appId)).ConfigureAwait(false));

    }

    private static async Task<string?> StartItemIdle(string botNames, string appid, string droplist)
    {
        HashSet<Bot>? bots = Bot.GetBots(botNames);
        if ((bots == null) || (bots.Count == 0))
        {
            return Commands.FormatStaticResponse(string.Format(Strings.BotNotFound, botNames));
        }

        IList<string?> results = await Utilities.InParallel(bots.Select(bot => StartItemIdle(bot, appid, droplist))).ConfigureAwait(false);

        List<string?> responses = new(results.Where(result => !string.IsNullOrEmpty(result)));

        return responses.Count > 0 ? string.Join(Environment.NewLine, responses) : null;

    }

    private static async Task<string?> StopItemIdle(Bot bot)
    {
        if (!ItemDropHandlers.TryGetValue(bot, out ItemDropHandler? ItemDropHandler))
        {
            return bot.Commands.FormatBotResponse(string.Format(Strings.ErrorIsEmpty, nameof(ItemDropHandlers)));
        }

        return bot.Commands.FormatBotResponse(await Task.Run<string>(() => ItemDropHandler.ItemIdleingStop(bot)).ConfigureAwait(false));

    }
    private static async Task<string?> StopItemIdle(string botNames)
    {
        HashSet<Bot>? bots = Bot.GetBots(botNames);
        if ((bots == null) || (bots.Count == 0))
        {
            return Commands.FormatStaticResponse(string.Format(Strings.BotNotFound, botNames));
        }

        IList<string?> results = await Utilities.InParallel(bots.Select(bot => StopItemIdle(bot))).ConfigureAwait(false);

        List<string?> responses = new(results.Where(result => !string.IsNullOrEmpty(result)));

        return responses.Count > 0 ? string.Join(Environment.NewLine, responses) : null;

    }

    private static async Task<string?> ItemDropDefList(Bot bot)
    {
        if (!ItemDropHandlers.TryGetValue(bot, out ItemDropHandler? ItemDropHandler))
        {
            return bot.Commands.FormatBotResponse(string.Format(Strings.ErrorIsEmpty, nameof(ItemDropHandlers)));
        }

        return bot.Commands.FormatBotResponse(await Task.Run<string>(() => ItemDropHandler.ItemDropDefList(bot)).ConfigureAwait(false));

    }

    private static async Task<string?> ItemDropDefList(string botNames)
    {
        HashSet<Bot>? bots = Bot.GetBots(botNames);
        if ((bots == null) || (bots.Count == 0))
        {
            return Commands.FormatStaticResponse(string.Format(Strings.BotNotFound, botNames));
        }

        IList<string?> results = await Utilities.InParallel(bots.Select(bot => ItemDropDefList(bot))).ConfigureAwait(false);

        List<string?> responses = new(results.Where(result => !string.IsNullOrEmpty(result)));

        return responses.Count > 0 ? string.Join(Environment.NewLine, responses) : null;
    }

    private static async Task<string?> CheckItem(Bot bot, string appid, string itemdefId, bool longoutput)
    {
        if (!uint.TryParse(appid, out uint appId))
        {
            return bot.Commands.FormatBotResponse(string.Format(Strings.ErrorIsInvalid, nameof(appId)));
        }
        if (!uint.TryParse(itemdefId, out uint itemdefid))
        {
            return bot.Commands.FormatBotResponse(string.Format(Strings.ErrorIsInvalid, nameof(itemdefid)));
        }
        if (!ItemDropHandlers.TryGetValue(bot, out ItemDropHandler? ItemDropHandler))
        {
            return bot.Commands.FormatBotResponse(string.Format(Strings.ErrorIsEmpty, nameof(ItemDropHandlers)));
        }

        return bot.Commands.FormatBotResponse(await Task.Run<string>(() => ItemDropHandler.CheckTime(appId, itemdefid, bot, longoutput)).ConfigureAwait(false));

    }

    private static async Task<string?> CheckItem(string botNames, string appid, string itemdefId, bool longoutput)
    {
        HashSet<Bot>? bots = Bot.GetBots(botNames);

        if ((bots == null) || (bots.Count == 0))
        {
            return Commands.FormatStaticResponse(string.Format(Strings.BotNotFound, botNames));
        }

        IList<string?> results = await Utilities.InParallel(bots.Select(bot => CheckItem(bot, appid, itemdefId, longoutput))).ConfigureAwait(false);

        List<string?> responses = new(results.Where(result => !string.IsNullOrEmpty(result)));

        return responses.Count > 0 ? string.Join(Environment.NewLine, responses) : "No Results";

    }

    private static async Task<string?> CheckPlaytime(Bot bot, string appid)
    {
        if (!uint.TryParse(appid, out uint appId))
        {
            return bot.Commands.FormatBotResponse(string.Format(Strings.ErrorIsInvalid, nameof(appId)));
        }
        if (!ItemDropHandlers.TryGetValue(bot, out ItemDropHandler? ItemDropHandler))
        {
            return bot.Commands.FormatBotResponse(string.Format(Strings.ErrorIsEmpty, nameof(ItemDropHandlers)));
        }

        return bot.Commands.FormatBotResponse(await Task.Run<string>(() => ItemDropHandler.CheckPlaytime(appId, bot)).ConfigureAwait(false));

    }

    private static async Task<string?> CheckPlaytime(string botNames, string appid)
    {
        HashSet<Bot>? bots = Bot.GetBots(botNames);

        if ((bots == null) || (bots.Count == 0))
        {
            return Commands.FormatStaticResponse(string.Format(Strings.BotNotFound, botNames));
        }

        IList<string?> results = await Utilities.InParallel(bots.Select(bot => CheckPlaytime(bot, appid))).ConfigureAwait(false);

        List<string?> responses = new(results.Where(result => !string.IsNullOrEmpty(result)));

        return responses.Count > 0 ? string.Join(Environment.NewLine, responses) : "No Results";

    }

    // Testing section for checking new feature as single/independent command
    private static async Task<string?> ItemDropTest(Bot bot, string appid)
    {
        if (!uint.TryParse(appid, out uint appId))
        {
            return bot.Commands.FormatBotResponse(string.Format(Strings.ErrorIsInvalid, nameof(appId)));
        }
        if (!ItemDropHandlers.TryGetValue(bot, out ItemDropHandler? ItemDropHandler))
        {
            return bot.Commands.FormatBotResponse(string.Format(Strings.ErrorIsEmpty, nameof(ItemDropHandlers)));
        }
        return bot.Commands.FormatBotResponse(await Task.Run<string>(() => ItemDropHandler.ItemDropTest(appId, bot)).ConfigureAwait(false));
    }

    private static async Task<string?> ItemDropTest(string botNames, string appid)
    {
        HashSet<Bot>? bots = Bot.GetBots(botNames);
        if ((bots == null) || (bots.Count == 0))
        {
            return Commands.FormatStaticResponse(string.Format(Strings.BotNotFound, botNames));
        }
        IList<string?> results = await Utilities.InParallel(bots.Select(bot => ItemDropTest(bot, appid))).ConfigureAwait(false);
        List<string?> responses = new(results.Where(result => !string.IsNullOrEmpty(result)));
        return responses.Count > 0 ? string.Join(Environment.NewLine, responses) : "No Results";
    }
}
