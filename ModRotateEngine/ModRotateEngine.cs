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
using NQ.Interfaces;
using NQutils;
using NQutils.Net;
using NQutils.Serialization;
using NQutils.Sql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using MathNet.Spatial.Euclidean;

public class RotState
{
    public List<ElementInfo> engines = new();
    public double target;
    public double current;
}
public class MyDuMod: IMod
{
    private const double STEP_SIZE = 1.0/25.0;
    private IServiceProvider isp;
    private IClusterClient orleans;
    private ILogger logger;
    private ConcurrentDictionary<ulong, RotState> state = new();
    public string GetName()
    {
        return "NQ.RotateEngine";
    }
    private async Task Step()
    {
        List<ulong> toRemove = new();
        foreach (var (k, v) in state)
        {
            bool stop = false;
            if (v.target < v.current)
            {
                v.current -= STEP_SIZE;
                stop = v.target >= v.current;
            }
            else
            {
                v.current += STEP_SIZE;
                stop = v.target <= v.current;
            }
            var ceg = orleans.GetConstructElementsGrain(k);
            foreach (var eng in v.engines)
            {
                await ceg.MoveElement(new ElementLocation
                    {
                        elementId = eng.elementId,
                        location = new RelativeLocation
                        {
                            constructId = k,
                            position = eng.position,
                            rotation = Quat.Rotation(new Vec3{x=1}, v.current),
                        }
                    });
            }
            if (stop)
                toRemove.Add(k);
        }
        foreach (var tr in toRemove)
        {
            state.TryRemove(tr, out var _);
        }
    }
    private async Task Loop()
    {
        while (true)
        {
            try
            {
                await Step();
                await Task.Delay(100);
            }
            catch (Exception e)
            {
                logger.LogError(e, "stepping...");
                await Task.Delay(1000);
            }
        }
    }
    public Task Initialize(IServiceProvider isp)
    {
        this.isp = isp;
        this.orleans = isp.GetRequiredService<IClusterClient>();
        this.logger = isp.GetRequiredService<ILogger<MyDuMod>>();
        var _ = Loop();
        return Task.CompletedTask;
    }
    public Task<ModInfo> GetModInfoFor(ulong playerId, bool admin)
    {
        var res = new ModInfo
        {
            name = GetName(),
            actions = new List<ModActionDefinition>(),
        };
        res.actions.Add(
            new ModActionDefinition
            {
                id = 1,
                label = "Engine\\Set as rotating",
                context = ModActionContext.Element,
            });
        res.actions.Add(
            new ModActionDefinition
            {
                id = 2,
                label = "Engine\\Set as not rotating",
                context = ModActionContext.Element,
            });
        res.actions.Add(
            new ModActionDefinition
            {
                id = 3,
                label = "Activate rotation control",
                context = ModActionContext.Element,
            });
        return Task.FromResult(res);
    }
    
    public async Task TriggerAction(ulong playerId, ModAction action)
    {
        if (action.actionId == 1)
        {
            await orleans.GetConstructElementsGrain(action.constructId)
                .UpdateElementProperty(new ElementPropertyUpdate
                    {
                        constructId = action.constructId,
                        elementId = action.elementId,
                        name = "gameplayTag",
                        value = new PropertyValue("can_rotate"),
                        timePoint = TimePoint.Now(),
                    });
        }
        if (action.actionId == 2)
        {
             await orleans.GetConstructElementsGrain(action.constructId)
                .UpdateElementProperty(new ElementPropertyUpdate
                    {
                        constructId = action.constructId,
                        elementId = action.elementId,
                        name = "gameplayTag",
                        value = new PropertyValue(""),
                        timePoint = TimePoint.Now(),
                    });
        }
        if (action.actionId == 3)
        {
            await isp.GetRequiredService<IPub>().NotifyTopic(Topics.PlayerNotifications(playerId),
                    new NQutils.Messages.ModTriggerHudEventRequest(new ModTriggerHudEvent
                        {
                            eventName = "modinjectjs",
                            eventPayload = panel,
                        }));
        }
        if (action.actionId == 1000000)
        {
            var cid = action.constructId;
            if (cid == 0)
                cid = (await orleans.GetPlayerGrain(playerId).GetPositionUpdate()).localPosition.constructId;
            var angle = double.Parse(action.payload) * 3.14159265 / 100.0;
            if (state.TryGetValue(cid, out var s))
                s.target = angle;
            else
            {
                var hasCurrent = false;
                var rs = new RotState();
                rs.target = angle;
                var ceg = orleans.GetConstructElementsGrain(cid);
                var eids = await ceg.GetElementsOfType<NQutils.Def.EngineUnit>();
                foreach (var eid in eids)
                {
                    var el = await ceg.GetElement(eid);
                    if (el.properties.TryGetValue("gameplayTag", out var gt) && gt.stringValue == "can_rotate")
                    {
                        if (!hasCurrent)
                        {
                            Quaternion q = (Quaternion)el.rotation;
                            var ang = q.ToEulerAngles();
                            rs.current = ang.Alpha.Radians;
                            hasCurrent = true;
                            logger.LogInformation("Setting current to {angle}", rs.current*180.0/3.14159);
                        }
                        rs.engines.Add(el);
                    }
                }
                state.TryAdd(cid, rs);
            }
        }
    }
    private readonly string panel = @"
    document.rotator = new NotificationIconComponent(""icon_missing"", ""rotator"");
    document.rotator.scroller = new ScrollBarComponent();
    document.rotator.wrapperNode.appendChild(document.rotator.scroller.wrapperNode);
    document.rotator.scroller.events.onMoveScrollbar.subscribe(() => {
            let pos = document.rotator.scroller.currentPosition;
            CPPMod.sendModAction(""NQ.RotateEngine"",  1000000, [], pos.toString());
    });
    document.rotator.scroller.wrapperNode.classList.toggle(""hide"", true);
    document.rotator.onClickEvent.subscribe(() => {
            document.rotator.scroller.wrapperNode.classList.toggle(""hide"", false);
    });
    hudManager.addNotificationIcon(document.rotator);
    ";
}