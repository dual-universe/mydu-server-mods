using Orleans;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Backend;
using Backend.Business;
using Backend.Database;
using NQutils.Config;
using Backend.Storage;
using Backend.Scenegraph;
using NQ;
using NQ.Grains.Core;
using NQ.Interfaces;
using NQutils;
using NQutils.Exceptions;
using NQutils.Net;
using NQutils.Serialization;
using NQutils.Sql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;


public class MyDuMod: IMod
{
    private IServiceProvider isp;
    private IClusterClient orleans;
    private ILogger logger;
    private Random rnd = new();
    public string GetName()
    {
        return "NQ.InterceptorDemo";
    }
    public Task Initialize(IServiceProvider isp)
    {
        this.isp = isp;
        this.orleans = isp.GetRequiredService<IClusterClient>();
        this.logger = isp.GetRequiredService<ILogger<MyDuMod>>();
        return Task.CompletedTask;
    }
    public Task<ModInfo> GetModInfoFor(ulong playerId, bool admin)
    {
        var res = new ModInfo
        {
            name = GetName(),
            actions = new List<ModActionDefinition>(),
        };
        res.actions.Add(new ModActionDefinition
            {
                id = 1,
                label = "Interceptor\\Deny construct creation",
                context = ModActionContext.Global,
            });
        res.actions.Add(new ModActionDefinition
            {
                id = 2,
                label = "Interceptor\\Allow construct creation",
                context = ModActionContext.Global,
            });
        res.actions.Add(new ModActionDefinition
            {
                id = 3,
                label = "Interceptor\\Troll players: rnd rotate",
                context = ModActionContext.Global,
            });
        return Task.FromResult(res);
    }
    public Task CanCreateContsructDeny(string key)
    {
        logger.LogInformation("Deny construct creation to {player}", key);
        throw new BusinessException(NQ.ErrorCode.Unauthorized, "blocked"); 
    }
    public async Task<ElementInfo> AddConstructElement(IIncomingGrainCallContext ctx, ElementInfo element)
    {
        ulong playerId = (ulong)((IGrainWithIntegerKey)ctx.Grain).GetPrimaryKeyLong();
        logger.LogInformation("Troll {player}", playerId);
        element.rotation = new NQ.Quat
        {
            x = (float)rnd.NextDouble(),
            y = (float)rnd.NextDouble(),
            z = (float)rnd.NextDouble(),
            w = (float)rnd.NextDouble(),
        };
        // Do not call again PlayerGrain.AddConstructElement it will infinite loop back here
        // Do this instead:
        await ctx.Invoke();
        return (ElementInfo)ctx.Result;
    }
    private HookHandle denyCC = null;
    public Task TriggerAction(ulong playerId, ModAction action)
    {
        if (action.actionId == 1)
        {
            denyCC = isp.GetRequiredService<IHookCallManager>().Register(
                "PlayerGrain.CanCreateConstruct", HookMode.PreCall, this,
                "CanCreateContsructDeny");
        }
        else if (action.actionId == 2)
        {
            isp.GetRequiredService<IHookCallManager>().Unregister(denyCC);
        }
        else if (action.actionId == 3)
        {
            isp.GetRequiredService<IHookCallManager>().Register(
                "PlayerGrain.AddConstructElement", HookMode.Replace, this,
                "AddConstructElement");
        }
        return Task.CompletedTask;
    }
}