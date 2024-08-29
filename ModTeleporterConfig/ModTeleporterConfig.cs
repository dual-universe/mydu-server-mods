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
using NQ.RDMS;
using NQ.Interfaces;
using NQutils;
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
    private readonly List<string> names = new List<string>{"Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Yota", "Omega"};
    public string GetName()
    {
        return "NQ.TeleporterConfig";
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
        ulong idx = 0;
        foreach (var n in names)
        {
            res.actions.Add( new ModActionDefinition
                {
                    id = 100+idx,
                    label = "Telepoter\\Set this TP target\\" + names[(int)idx],
                    context = ModActionContext.Element,
                });
             res.actions.Add( new ModActionDefinition
                {
                    id = 200+idx,
                    label = "Telepoter\\Make as target\\" + names[(int)idx],
                    context = ModActionContext.Element,
                });
             idx += 1;
        }
        return Task.FromResult(res);
    }
    public async Task TriggerAction(ulong playerId, ModAction action)
    {
        var cid = action.constructId;
        var eid = action.elementId;
        var right = await orleans.GetRDMSRightGrain(playerId).GetRightsForPlayerOnAsset(
            playerId,
            new AssetId
            {
                type = AssetType.Element,
                construct = cid,
                element = eid,
            },
            true);
        if (!right.rights.Contains(Right.ElementEdit))
        {
            await isp.GetRequiredService<IPub>().NotifyTopic(Topics.PlayerNotifications(playerId),
                new NQutils.Messages.ModTriggerHudEventRequest(new ModTriggerHudEvent
                    {
                        eventName = "modinjectjs",
                        eventPayload = "CPPHud.addFailureNotification(\"You do not have permissions on this element to configure teleporter\");",
                    }));
            return;
        }
        var key = ((action.actionId / 100) == 1) ? "teleport_destination" : "gameplayTag";
        var value = $"mod_teleporter_{playerId}_" + names[(int)action.actionId % 100];
        await orleans.GetConstructElementsGrain(cid).UpdateElementProperty(
            new ElementPropertyUpdate
            {
                constructId = cid,
                elementId = eid,
                name = key,
                value = new PropertyValue(value),
                timePoint = TimePoint.Now(),
            });
        await isp.GetRequiredService<IPub>().NotifyTopic(Topics.PlayerNotifications(playerId),
                new NQutils.Messages.ModTriggerHudEventRequest(new ModTriggerHudEvent
                    {
                        eventName = "modinjectjs",
                        eventPayload = "CPPHud.addFailureNotification(\"Teleportation configuration successful\");",
                    }));
    }
}