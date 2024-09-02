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
    private IGameplayBank bank;
    
    public string GetName()
    {
        return "NQ.InteractionDemo";
    }
    public async Task ElementPropertyUpdate(string _, PlayerId pid, ElementPropertyUpdate epu)
    {
        if (epu.name != "element_state")
            return;
        var ei = await orleans.GetConstructElementsGrain(epu.constructId).GetElement(epu.elementId);
        var ename = bank.GetDefinition(ei.elementType).Name;
        if (ename != "CustomButtonA")
            return;
        logger.LogInformation("*** custom button triggered");
    }
    public Task Initialize(IServiceProvider isp)
    {
        this.isp = isp;
        this.orleans = isp.GetRequiredService<IClusterClient>();
        this.logger = isp.GetRequiredService<ILogger<MyDuMod>>();
        this.bank = isp.GetRequiredService<IGameplayBank>();
        isp.GetRequiredService<IHookCallManager>().Register(
                "ElementManagementGrain.ElementPropertyUpdate", HookMode.PreCall, this,
                "ElementPropertyUpdate");
        return Task.CompletedTask;
    }
    public Task<ModInfo> GetModInfoFor(ulong playerId, bool admin)
    {
        return Task.FromResult<ModInfo>(null);
    }
    public Task TriggerAction(ulong playerId, ModAction action)
    {
        return Task.CompletedTask;
    }
}