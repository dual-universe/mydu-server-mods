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
        return "NQ.BypassFTUE";
    }
    public async Task GiveStartupBlueprints(ulong playerId, List<string> blueprintNames)
    {
        /* this function looks for a container with gameplayTag 'startup_blueprints'.
             * which by default exists hidden in the arkship start room
             * It then gives all blueprints with name in list argument to the player
             */
        var sql = isp.GetRequiredService<ISql>();
        var itemStorage = isp.GetRequiredService<IItemStorageService>();
        var gameplayBank = isp.GetRequiredService<IGameplayBank>();
        logger.LogInformation("Client {playerId} is requesting startup blueprints: {blueprints}", playerId, String.Join(",", blueprintNames));
        // Get the startup blueprint container
        var containerId = await sql.GetElementByProperty("gameplayTag", "startup_blueprints");
        var content = await itemStorage.Get(StorageRef.Container(containerId), 0);
        var blueprintIds = new List<ulong>();
        foreach (var s in content.content)
        {
            if (s.content.type == gameplayBank.GetDefinition<NQutils.Def.Blueprint>().Id
                && blueprintNames.Contains(s.content.properties["name"].stringValue))
            {
                blueprintIds.Add(s.content.id);
                // protection against duplicate entries
                blueprintNames.Remove(s.content.properties["name"].stringValue);
            }
        }
        if (blueprintNames.Any())
        {
            var failing = String.Join(",", blueprintNames);
            throw new BusinessException(NQ.ErrorCode.InvalidParameters, $"Some blueprints were not found: {failing}");
        }
        var bpg = orleans.GetBlueprintGrain();
        var iaqs = new List<ItemAndQuantity>();
        foreach (var bid in blueprintIds)
        {
            var item = new ItemInfo { type = gameplayBank.IdFor<NQutils.Def.SingleUseBlueprint>(), id = bid };
            await sql.ReadProperties(item, gameplayBank);
            iaqs.Add(new ItemAndQuantity { item = item, quantity = 1 });
        }
        
        var inventory = StorageRef.PlayerInventoryWithoutPrimary(playerId);
        await itemStorage.GiveOrTakeItems(Tag.GiveStartupBlueprints(playerId, blueprintNames), inventory, iaqs, new());
        logger.LogInformation("Gave startup blueprints to {player}", playerId);
    }
    private async Task GiveInitialPackage(ulong playerId)
    {
        try
        {
            await GiveStartupBlueprints(playerId, new List<string>{"Outpost Pilot"});
        }
        catch(Exception e)
        {
            logger.LogError(e, "Failed to give blueprints to new player");
        }
    }
    public async Task<LoginResponseOrCreation> GetLoginResponseOrCreation(IIncomingGrainCallContext ctx, AuthUserInfo authInfo)
    {
        ulong playerId = (ulong)((IGrainWithIntegerKey)ctx.Grain).GetPrimaryKeyLong();
        await orleans.GetPlayerGrain(playerId).UpdatePlayerPropertyEx("ftueStep",
            new PropertyValue(0), true);
        var userInfo = await isp.GetRequiredService<ISql>().GetPlayerInfoById(playerId);
        // You can alter res, but GetPlayerLoginState *must* be called
        var res = await orleans.GetPlayerGrain(playerId).GetPlayerLoginState(authInfo);
        if (userInfo.NeverConnected)
        {
            await using var tr = await isp.GetRequiredService<IItemStorageService>().MakeTransaction(
                    Tag.HttpCall("tools") // use a dummy tag, those only serves for logging/tracing
                    );
            await orleans.GetInventoryGrain(playerId).ResetToDefault(tr, true);
            await tr.Commit();
            await GiveInitialPackage(playerId);
            // set initial spawn position to a chair in market 6
            res.spawnState.location = new RelativeLocation
            {
                constructId = 100541,
                position = new Vec3 { x = 262, y = 244, z = 289},
                rotation = Quat.Identity,
            };
            res.spawnState.constructTree = await isp.GetRequiredService<IScenegraph>().GetTree(100541);
        }
        return new LoginResponseOrCreation
            {
                displayName = authInfo.displayName,
                kind = (LoginResponseKind)0,
                optState = res
            };
    }
    public Task Initialize(IServiceProvider isp)
    {
        this.isp = isp;
        this.orleans = isp.GetRequiredService<IClusterClient>();
        this.logger = isp.GetRequiredService<ILogger<MyDuMod>>();
        isp.GetRequiredService<IHookCallManager>().Register(
                "PlayerGrain.GetLoginResponseOrCreation", HookMode.Replace, this,
                "GetLoginResponseOrCreation");
        return Task.CompletedTask;
    }
    public Task<ModInfo> GetModInfoFor(ulong playerId, bool isAdmin)
    {
        return Task.FromResult<ModInfo>(null);
    }
    public Task TriggerAction(ulong playerId, ModAction action)
    {
        return Task.CompletedTask;
    }
}