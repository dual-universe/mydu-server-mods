using System;
using System.Net.Http;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using BotLib.BotClient;
using BotLib.Generated;
using BotLib.Protocols;
using BotLib.Protocols.Queuing;
using Microsoft.Extensions.DependencyInjection;
using NQutils;
using NQutils.Config;
using NQutils.Logging;
using NQutils.Sql;
using Orleans;
using NQ.Interfaces;
using NQ;
using NQ.RDMS;
using NQ.Router;
using System.Threading.Channels;
using Backend;
using Backend.Business;
using Newtonsoft.Json;


/// Mod base class
public class Mod
{
    public static IDuClientFactory RestDuClientFactory => serviceProvider.GetRequiredService<IDuClientFactory>();
    /// Use this to acess registered service
    protected static IServiceProvider serviceProvider;
    /// Use this to make gameplay calls, see "Interfaces/GrainGetterExtensions.cs" for what's available
    protected static IClusterClient orleans;
    /// Use this object for various data access/modify helper functions
    protected static IDataAccessor dataAccessor;
    /// Conveniance field for mods who need a single bot
    protected Client bot;
    /// Create or login a user, return bot client instance
    public static async Task<Client> CreateUser(string prefix, bool allowExisting = false, bool randomize = true)
    {
        string username = prefix;
        if (randomize)
        {
            // Do not use random utilities as they are using tests random (that is seeded), and we want to be able to start the same test multiple times
            Random r = new Random(Guid.NewGuid().GetHashCode());
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyz";
            username = prefix + '-' + new string(Enumerable.Repeat(0, 127 - prefix.Length).Select(_ => chars[r.Next(chars.Length)]).ToArray());
        }
        LoginInformations pi = LoginInformations.BotLogin(username, Environment.GetEnvironmentVariable("BOT_LOGIN"), Environment.GetEnvironmentVariable("BOT_PASSWORD"));
        return await Client.FromFactory(RestDuClientFactory, pi, allowExising: allowExisting);
    }
    /// Setup everything, must be called once at startup
    public static async Task Setup()
    {
        var services = new ServiceCollection();

        //services.RegisterCoreServices();
        var qurl = Environment.GetEnvironmentVariable("QUEUEING");
        if (qurl == "")
            qurl = "http://queueing:9630";
        services
        .AddSingleton<ISql, Sql>()
        .AddInitializableSingleton<IGameplayBank, GameplayBank>()
        .AddSingleton<ILocalizationManager, LocalizationManager>()
        .AddTransient<IDataAccessor, DataAccessor>()
        //.AddLogging(logging => logging.Setup(logWebHostInfo: false))
        .AddOrleansClient("IntegrationTests")
        .AddHttpClient()
        .AddTransient<NQutils.Stats.IStats, NQutils.Stats.FakeIStats>()
        .AddSingleton<IQueuing, RealQueuing>(sp => new RealQueuing(qurl, sp.GetRequiredService<IHttpClientFactory>().CreateClient()))
        .AddSingleton<IDuClientFactory, BotLib.Protocols.GrpcClient.DuClientFactory>()
        //.AddSingleton<Backend.AWS.IS3, Backend.AWS.FakeS3.FakeS3Singleton>()
        //.AddSingleton<Backend.Storage.IItemStorageService, Backend.Storage.ItemStorageService>()
        ;//.AddInitializableSingleton<IUserContent, UserContent>();
        var sp = services.BuildServiceProvider();
        serviceProvider = sp;
        await serviceProvider.StartServices();
        ClientExtensions.SetSingletons(sp);
        ClientExtensions.UseFactory(sp.GetRequiredService<IDuClientFactory>());
        orleans = serviceProvider.GetRequiredService<IClusterClient>();
        dataAccessor = serviceProvider.GetRequiredService<IDataAccessor>();
    }
    public async Task Start()
    {
        try
        {
            await Loop();
        }
        catch(Exception e)
        {
            Console.WriteLine($"{e}\n{e.StackTrace}");
            throw;
        }
    }
    /// Override this with main bot code
    public virtual Task Loop()
    {
        return Task.CompletedTask;
    }
    /// Conveniance helper for running code forever
    public async Task SafeLoop(Func<Task> action, int exceptionDelayMs,
        Func<Task> reconnect)
    {
        while (true)
        {
            try
            {
                await action();
            }
            catch(NQutils.Exceptions.BusinessException be) when (be.error.code == NQ.ErrorCode.InvalidSession)
            {
                Console.WriteLine("reconnecting");
                await reconnect();
                await Task.Delay(exceptionDelayMs);
            }
            catch(Exception e)
            {
                Console.WriteLine($"Exception in mod action: {e}\n{e.StackTrace}");
                await Task.Delay(exceptionDelayMs);
            }
        }
    }
    public async Task ChatDmOnMention(string key)
    {
        var listener = bot.Events.MessageReceived.Listener();
        while (true)
        {
            var msg = await listener.GetLastEventWait(mc=>true,1000000000);
            listener.Clear();
            if (msg.message.Contains(key))
            {
                await bot.Req.ChatMessageSend(new NQ.MessageContent
                        {
                            channel = new NQ.MessageChannel
                            {
                                channel = MessageChannelType.PRIVATE,
                                targetId = msg.fromPlayerId,
                            },
                            message = "You wanted to talk to me?",
                        });
            }
        }
    }
}
public class TraderConfig
{
    public Dictionary<string, double> buyPrices = new();
    public Dictionary<string, double> buyRecursivePrices = new();
    public double margin = 1.1;
    public double orderQuantity = 100000;
    public double orderRefreshRatio = 0.7;
}

public class ModTraderBot: Mod
{
    TraderConfig config;
    Dictionary<ulong, double> buyPrices = new();
    public ModTraderBot(string confPath)
    {
        var confText = System.IO.File.ReadAllText(confPath);
        config = JsonConvert.DeserializeObject<TraderConfig>(confText);
    }
    public override async Task Loop()
    {
        bot = await CreateUser("trader", true, false);
        var bank = bot.GameplayBank;
        foreach (var (k, v) in config.buyPrices)
        {
            buyPrices[bank.GetDefinition(k).Id] = v;
        }
        
        foreach (var (k, v) in config.buyRecursivePrices)
        {
            var baseEntry = bank.GetDefinition(k);
            var childrenIds = baseEntry.GetChildrenIdsRecursive();
            foreach (var childId in childrenIds)
            {
                var entry = bank.GetDefinition(childId);
                if (entry.GetChildren().Count() != 0)
                    continue;
                buyPrices[childId] = v;
            }
        }
        Console.WriteLine($"Trader will buy {buyPrices.Count} items");
        await SafeLoop(Action, 5000, async () => {
                bot = await CreateUser("trader", true, false);
        });
    }
    public async Task RefreshOrders()
    {
        var ml = await bot.Req.MarketGetList(2);
        var mids = ml.markets.Select(x=>x.marketId).ToList();
        foreach (var mkt in ml.markets)
        {
            var doneIds = new List<ulong>();
            var orders = await bot.Req.MarketGetMyOrders(
                new MarketSelectRequest
                {
                    marketIds = new List<ulong>{mkt.marketId},
                    itemTypes = buyPrices.Keys.ToList(),
                });
            foreach (var order in orders.orders)
            {
                doneIds.Add(order.itemType);
                if (order.buyQuantity > 0 && order.buyQuantity < (long)(config.orderQuantity * config.orderRefreshRatio))
                {
                    order.buyQuantity = (long)config.orderQuantity;
                    await bot.Req.MarketUpdateOrder(new MarketRequest
                        {
                            marketId = order.marketId,
                            itemType = order.itemType,
                            buyQuantity = (long)config.orderQuantity,
                            expirationDate = DateTime.Now.AddDays(3000).ToNQTimePoint(),
                            unitPrice = (long)(buyPrices[order.marketId]*100),
                            orderId = order.orderId,
                        });
                }
            }
            var doneHS = doneIds.ToHashSet();
            foreach (var (k, v) in buyPrices)
            {
                if (doneHS.Contains(k))
                    continue;
                await bot.Req.MarketPlaceOrder(new MarketRequest
                    {
                         marketId = mkt.marketId,
                         itemType = k,
                         buyQuantity = (long)config.orderQuantity,
                         expirationDate = DateTime.Now.AddDays(3000).ToNQTimePoint(),
                         unitPrice = (long)(v*100),
                    });
            }
        }
    }
    public async Task CheckContainers()
    {
        var ml = await bot.Req.MarketGetList(2);
        foreach (var mkt in ml.markets)
        {
            var storage = await bot.Req.MarketContainerGetMyContent(new MarketSelectRequest
                {
                    marketIds = new List<ulong>{mkt.marketId},
                    itemTypes = new List<ulong>{bot.GameplayBank.GetDefinition<NQutils.Def.BaseItem>().Id},
                });
            foreach (var slot in storage.slots)
            {
                if (!slot.purchased)
                    continue;
                var type = slot.itemAndQuantity.item.type;
                var mQty = slot.itemAndQuantity.quantity.value;
                Console.WriteLine($"Detected new {mQty} of {type}");
                await bot.Req.MarketPlaceOrder(new MarketRequest
                    {
                         marketId = mkt.marketId,
                         source = MarketRequestSource.FROM_MARKET_CONTAINER,
                         itemType = type,
                         buyQuantity = -mQty,
                         expirationDate = DateTime.Now.AddDays(3000).ToNQTimePoint(),
                         unitPrice = (long)(buyPrices[type] * config.margin * 100),
                    });
            }
        }
    }
    public async Task Action()
    {
        await RefreshOrders();
        await CheckContainers();
        await Task.Delay(10000);
    }
}


public static class Program
{
    public static void Main(string[] args)
    {
        var cmdline = String.Join(",", args);
        Console.WriteLine($"{cmdline}");
        try
        {
            Config.ReadYamlFileFromArgs("mod", args);
        }
        catch (Exception e)
        {
            Console.WriteLine($"{e}\n{e.StackTrace}");
            return;
        }
        Mod.Setup().Wait();
        var trader = new ModTraderBot(args[1]);
        trader.Start().Wait();
    }
}