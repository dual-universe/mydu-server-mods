using Orleans;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Reflection;
using Backend;
using Backend.Business;
using Backend.Database;
using NQutils.Config;
using Backend.Storage;
using Backend.Scenegraph;
using NQ;
using NQ.RDMS;
using NQ.Interfaces;
using NQ.Grains.Core;
using NQutils;
using NQutils.Exceptions;
using NQutils.Net;
using NQutils.Serialization;
using NQutils.Sql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using K4os.Compression.LZ4;

public class ItemShort
{
    public ulong typeId;
    public string name;
    public string displayName;
}
public class AmmoChoices
{
    public ulong localId;
    public List<string> attachedWeaponKinds = new();
}
public class ClientAmmoContainer
{
    public ulong localId;
    public List<string> attachedWeapons = new();
    public List<ItemShort> ammoOptions = new();
}
public class ClientLoadoutShip
{
    public string shipName;
    public List<ClientAmmoContainer> ammos = new();
}
public class ClientLoadoutReplyEntry
{
    public ulong localId;
    public List<ulong> itemTypes = new();
}
public class ClientLoadoutReply
{
    public string shipName;
    public List<ClientLoadoutReplyEntry> loadout = new();
}

public class PlayerStats
{
    public ulong lead; // number of enters as captain
    public ulong join; // number of enters as crewmate
    public ulong selfKills; // number of construct kills player did
    public ulong teamKills; // number of construct kills player was part of
    public ulong deathByWeapon;
    public ulong deathByCore;
    public ulong deathByRespawn;
    public ulong deathByElement;
}
public class MyDuMod: IMod
{
    private IServiceProvider isp;
    private IClusterClient orleans;
    private ILogger logger;
    private IGameplayBank bank;
    private ISql sql;
    private System.Random rnd = new();
    private ulong lobbyConstructId;
    private Vec3 lobbySpawnPosition;
    private Vec3 lobbyUniversePosition;
    private ulong lobbyContainerId;
    private ConcurrentDictionary<ulong, DateTime> debouncer = new();
    private ConcurrentDictionary<ulong, ulong> myCaptain = new();
    private ConcurrentDictionary<ulong, ulong> myCaptainRequests = new();
    private ConcurrentDictionary<ulong, List<ulong>> myLieutnants = new(); 
    private ConcurrentDictionary<string, List<ItemShort>> kindLoadoutChoices = new();
    private ConcurrentDictionary<ulong, List<AmmoChoices>> bpLoadoutCache = new();
    public string GetName()
    {
        return "NQ.FFF";
    }
    public async Task playerEnterBattle(PlayerId pid)
    {
        try
        {
            await DoplayerEnterBattle(pid);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Enter battle failure for {player}", pid);
        }
    }
    private async Task JoinCaptain(ulong pid, ulong captain)
    {
        ulong shipId = 0;
        while (true)
        {
            var ppu = await orleans.GetPlayerGrain(captain).GetPositionUpdate();
            if (ppu.localPosition.constructId != lobbyConstructId)
            {
                shipId = ppu.localPosition.constructId;
                break;
            }
            await isp.GetRequiredService<IPub>().NotifyTopic(Topics.PlayerNotifications(pid),
                    new NQutils.Messages.ModTriggerHudEventRequest(new ModTriggerHudEvent
                        {
                            eventName = "modinjectjs",
                            eventPayload = "CPPHud.addFailureNotification(\"Waiting for captain...\");",
                        }));
            await Task.Delay(5000);
        }
        // get tp location
        var ceg = orleans.GetConstructElementsGrain(shipId);
        var seatIds = await ceg.GetElementsOfType<NQutils.Def.ControlUnit>();
        var pos = (await ceg.GetElement(seatIds[0])).position;
        logger.LogInformation("TPING lieutnant {pid} to battle on {constructId}", pid, shipId);
        // tp player there
        UpdatePlayerStats(pid, s => s.join +=1);
        await orleans.GetPlayerGrain(pid).TeleportPlayer(
            new RelativeLocation
            {
                constructId = shipId,
                position = pos,
                rotation = Quat.Identity,
            });
        logger.LogInformation("TPING lieutnant {pid} done", pid);
    }
    public async Task DoplayerEnterBattle(PlayerId pid)
    {
        if (myCaptain.TryGetValue(pid.id, out var captain))
        {
            await JoinCaptain(pid, captain);
            return;
        }
        ClientLoadoutReply l = null;
        try
        {
            var j = System.IO.File.ReadAllText($"Mods/FFF/loadout-{pid.id}.json");
            l = JsonConvert.DeserializeObject<ClientLoadoutReply>(j);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, $"loading loadout for {pid}");
            await isp.GetRequiredService<IPub>().NotifyTopic(Topics.PlayerNotifications(pid),
                    new NQutils.Messages.ModTriggerHudEventRequest(new ModTriggerHudEvent
                        {
                            eventName = "modinjectjs",
                            eventPayload = "CPPHud.addFailureNotification(\"Configure loadout first\");",
                        }));
            return;
        }
        var bps = await GetBlueprints();
        var hit = bps.Where(x => x.Item2 == l.shipName).FirstOrDefault();
        if (hit.Item1 == 0)
        {
            logger.LogWarning($"Blueprint not found {l.shipName}");
            await isp.GetRequiredService<IPub>().NotifyTopic(Topics.PlayerNotifications(pid),
                    new NQutils.Messages.ModTriggerHudEventRequest(new ModTriggerHudEvent
                        {
                            eventName = "modinjectjs",
                            eventPayload = "CPPHud.addFailureNotification(\"loadout error\");",
                        }));
            return;
        }
        var spawnPos = new Vec3
        {
            x = lobbyUniversePosition.x + rnd.NextDouble()*1000.0,
            y = lobbyUniversePosition.y + rnd.NextDouble()*1000.0,
            z = lobbyUniversePosition.z + rnd.NextDouble()*1000.0,
        };
        var shipId = await orleans.GetBlueprintGrain().UseRaw(
            new BlueprintDeploy
            {
                blueprintId = hit.Item1,
                position = spawnPos,
                rotation = Quat.Identity,
                parentId = 0,
                constructName = (await orleans.GetPlayerGrain(pid).GetPlayerInfo()).name,
            }, pid);
        logger.LogInformation("Created ship {shipId}", shipId);
        var ceg = orleans.GetConstructElementsGrain(shipId);
        // activate shield first thing! :)
        var shids = await ceg.GetElementsOfType<NQutils.Def.ShieldGeneratorUnit>();
        if (shids.Count != 0)
        {
            await orleans.GetConstructFightGrain(shipId).ShieldToggle(pid,
                new PvpShieldToggleRequest
                {
                    constructId = shipId,
                    elementId = shids[0],
                    shieldOn = true,
                });
        }
        
        // Fill fuel containers
        var fcids = await ceg.GetElementsOfType<NQutils.Def.SpaceFuelContainer>();
        foreach (var fcid in fcids)
        {
            var el = await ceg.GetElement(fcid);
            var cg = orleans.GetContainerGrain(fcid);
            double maxVolume = bank.GetBaseObject<NQutils.Def.ContainerUnit>(el.elementType).maxVolume;
            if (el.properties.TryGetValue("maxVolume", out var pmv))
                maxVolume = pmv.doubleValue;
            var kerg = bank.GetDefinition("Kergon1");
            var uv = ((NQutils.Def.BaseItem)kerg.BaseObject).unitVolume;
            var itemStorage = isp.GetRequiredService<IItemStorageService>();
            await using var transaction = await itemStorage.MakeTransaction(Tag.HttpCall("fill"));
                logger.LogInformation("acquired transaction");
                await cg.GiveOrTakeItems(transaction,
                    new List<ItemAndQuantity> {
                        new ItemAndQuantity
                        {
                            item = new ItemInfo
                            {
                                type = kerg.Id,
                            },
                            quantity = (1<<24)*(long)(maxVolume / uv),
                        }
                    }, new());
                await transaction.Commit();
        }
        // Fill ammo containers
        var contIds = await ceg.GetElementsOfType<NQutils.Def.AmmoContainerUnit>();
        logger.LogInformation("Found {count} ammo containers", contIds.Count);
        foreach (var contId in contIds)
        {
            var el = await ceg.GetElement(contId);
            var load = l.loadout.Where(x=>x.localId == el.localId).FirstOrDefault();
            if (load == null)
            {
                logger.LogWarning("Failed to locate loadout for container {localId}", el.localId);
                continue;
            }
            var loadCount = load.itemTypes.Count;
            double maxVolume = bank.GetBaseObject<NQutils.Def.ContainerUnit>(el.elementType).maxVolume;
            if (el.properties.TryGetValue("maxVolume", out var pmv))
                maxVolume = pmv.doubleValue;
            double vpi = maxVolume / loadCount;
            var cg = orleans.GetContainerGrain(el.elementId);
            var itemStorage = isp.GetRequiredService<IItemStorageService>();
            foreach (var a in load.itemTypes)
            {
                var uv = bank.GetBaseObject<NQutils.Def.BaseItem>(a).unitVolume;
                var count = (long)(vpi / uv);
                logger.LogInformation("Giving {count} of {item}", count, a);
                await using var transaction = await itemStorage.MakeTransaction(Tag.HttpCall("fill"));
                logger.LogInformation("acquired transaction");
                await cg.GiveOrTakeItems(transaction,
                    new List<ItemAndQuantity> {
                        new ItemAndQuantity
                        {
                            item = new ItemInfo
                            {
                                type = a,
                            },
                            quantity = count,
                        }
                    }, new());
                logger.LogInformation("commiting...");
                await transaction.Commit();
                logger.LogInformation("...done");
            }
        }
        // Fix RDMS for lieutnants
        if (myLieutnants.TryGetValue(pid, out var lts))
        {
            var reg = orleans.GetRDMSRegistryGrain(pid);
            var ctag = await reg.CreateTag(new TagData
                {
                    owner = new EntityId { playerId = pid},
                    name = $"Lieutnants on {shipId}",
                });
            var polId = await reg.CreatePolicy(new PolicyData
                {
                    owner = new EntityId { playerId = pid},
                    name = $"Lieutnants on {shipId}",
                    actors = lts.Select(lid => new ActorId { actorId = lid, type = ActorType.Player}).ToList(),
                    rights = new List<Right> { Right.ConstructParent, Right.ConstructBoard, Right.ConstructRepair, Right.ConstructUseJetpack, Right.ElementUse, Right.ContainerView, Right.ContainerPut},
                    tags = new List<TagId> { ctag },
                });
            await orleans.GetRDMSAssetGrain(new AssetId {type = AssetType.Construct, construct = shipId}).UpdateTags(pid,
                new AssetUpdateTags
                {
                    asset = new AssetId { type = AssetType.Construct, construct = shipId},
                    tags = new List<TagId>{ctag},
                });
        }
        // get tp location
        var seatIds = await ceg.GetElementsOfType<NQutils.Def.ControlUnit>();
        var pos = (await ceg.GetElement(seatIds[0])).position;
        // tag it for GC
        await orleans.GetConstructElementsGrain(shipId).UpdateElementProperty(
            new ElementPropertyUpdate
            {
                constructId = shipId,
                elementId = seatIds[0],
                name = "gameplayTag",
                value = new PropertyValue("fff_created_ship"),
                timePoint = TimePoint.Now(),
            });
        logger.LogInformation("TPING player {playerId} to battle on {constructId}", pid, shipId);
        // tp player there
        UpdatePlayerStats(pid, s => s.lead +=1);
        await orleans.GetPlayerGrain(pid).TeleportPlayer(
            new RelativeLocation
            {
                constructId = shipId,
                position = pos,
                rotation = Quat.Identity,
            });
        logger.LogInformation("TPING player {playerId} done", pid);
    }
    public async Task<List<(ulong, string)>> GetBlueprints()
    {
        var cg = orleans.GetContainerGrain(lobbyContainerId);
        var storage = await cg.Get(2);
        var res = new List<(ulong, string)>();
        foreach (var slot in storage.content)
        {
            if (slot.content.type != bank.GetDefinition<NQutils.Def.Blueprint>().Id)
                continue;
            res.Add((slot.content.id, slot.content.properties["name"].stringValue));
        }
        return res;
    }
    public async Task<List<ClientLoadoutShip>> MakeLoadoutChoices()
    {
        var res = new List<ClientLoadoutShip>();
        var available = await GetBlueprints();
        foreach (var bp in available)
        {
            var lc = await GetBlueprintLoadout(bp.Item1);
            var ship = new ClientLoadoutShip
            {
                shipName = bp.Item2,
            };
            foreach (var a in lc)
            {
                var cac = new ClientAmmoContainer
                {
                    localId = a.localId,
                    attachedWeapons = a.attachedWeaponKinds,
                };
                var present = new HashSet<string>();
                foreach (var awk in a.attachedWeaponKinds)
                {
                    if (present.Contains(awk))
                        continue;
                    present.Add(awk);
                    cac.ammoOptions.AddRange(kindLoadoutChoices[awk]);
                }
                ship.ammos.Add(cac);
            }
            res.Add(ship);
        }
        return res;
    }
    public async Task playerConfigureLoadout(PlayerId pid)
    {
        var choices = await MakeLoadoutChoices();
        var ser = JsonConvert.SerializeObject(choices);
        var serser = JsonConvert.SerializeObject(ser);
        var payload = System.IO.File.ReadAllText("Mods/fff.js");
        await isp.GetRequiredService<IPub>().NotifyTopic(Topics.PlayerNotifications(pid),
                    new NQutils.Messages.ModTriggerHudEventRequest(new ModTriggerHudEvent
                        {
                            eventName = "modinjectjs",
                            eventPayload = payload,
                        }));
        await Task.Delay(2000);
        await isp.GetRequiredService<IPub>().NotifyTopic(Topics.PlayerNotifications(pid),
            new NQutils.Messages.ModTriggerHudEventRequest(new ModTriggerHudEvent
                {
                    eventName = "modinjectjs",
                    eventPayload = "document.fff.showLoadout(" + serser +");",
                }));
    }
    private void CacheRecurse(IGameplayDefinition gd)
    {
        if (gd.GetChildren().Count() == 0)
        {
            var bo = (NQutils.Def.Ammo)gd.BaseObject;
            var key = bo.weaponType.ToString().ToLower() + " " + bo.scale.ToLower();
            if (!kindLoadoutChoices.ContainsKey(key))
                kindLoadoutChoices.TryAdd(key, new List<ItemShort>());
            kindLoadoutChoices[key].Add(new ItemShort
                {
                    typeId = gd.Id,
                    name = gd.Name,
                    displayName = bo.displayName,
                });
        }
        else foreach (var c in gd.GetChildren())
            CacheRecurse(c);
    }
    public void CacheLoadoutChoices()
    {
        CacheRecurse(bank.GetDefinition<NQutils.Def.Ammo>());
    }
    public async Task CacheBlueprintLoadout(ulong bpid)
    {
        var data = await sql.ReadBlueprintData(bpid);
        var res = new List<AmmoChoices>();
        foreach (var e in data.Elements)
        {
            var et = e.elementType;
            if (!bank.GetDefinition(et).Is<NQutils.Def.AmmoContainerUnit>())
                continue;
            var wtypes = new List<string>();
            foreach (var l in data.Links)
            {
                if (l.fromElementId != e.elementId)
                    continue;
                var we = data.Elements.Where(x=>x.elementId == l.toElementId).FirstOrDefault();
                if (we == null)
                    continue;
                var wd = bank.GetBaseObject<NQutils.Def.WeaponUnit>(we.elementType);
                wtypes.Add(wd.weaponType.ToString().ToLower() + " " + wd.scale.ToLower());
            }
            res.Add(new AmmoChoices
                {
                    localId = e.localId,
                    attachedWeaponKinds = wtypes,
                });
        }
        bpLoadoutCache.TryAdd(bpid, res);
    }
    public async Task<List<AmmoChoices>> GetBlueprintLoadout(ulong bpid)
    {
        if (bpLoadoutCache.TryGetValue(bpid, out var res))
            return res;
        await CacheBlueprintLoadout(bpid);
        return bpLoadoutCache[bpid];
    }
    public async Task ElementPropertyUpdate(IIncomingGrainCallContext ctx, PlayerId pid, ElementPropertyUpdate epu)
    {
        if (epu.name != "element_state" && epu.name != "zone_state" && epu.name != "button_on" && epu.name != "tile_enabled")
        {
            await ctx.Invoke();
            return;
        }
        var ei = await orleans.GetConstructElementsGrain(epu.constructId).GetElement(epu.elementId);
        if (!ei.properties.TryGetValue("gameplayTag", out var gt))
        {
            await ctx.Invoke();
            return;
        }
        if (gt.stringValue == "lobby_startbattle" && epu.value.boolValue)
        {
            var useless = playerEnterBattle(pid);
            return; // no invocation to allow multiple players
        }
        else if (gt.stringValue == "lobby_loadout" && ei.properties["button_on"].boolValue)
            await playerConfigureLoadout(pid);
        else if (gt.stringValue == "lobby_plate")
        {
            await ShowPlayerStats(pid, epu.value.boolValue);
        }
        else if (gt.stringValue == "lobby_gc" && epu.value.boolValue)
            await ConstructGC();
        logger.LogInformation("*** custom interaction triggered on " + gt.stringValue);
        await ctx.Invoke();
    } 
    public async Task ConstructGC()
    {
        var ships = await sql.GetConstructLocation(
            new LocationDescriptor
            {
                propertyName = "gameplayTag",
                propertyValue = "fff_created_ship",
            });
        logger.LogInformation("Garbage collecting check on {count} ships", ships.Count);
        foreach (var ship in ships)
        {
            var ci = await orleans.GetConstructInfoGrain(ship.Item2).Get();
            var isince = ci.mutableData.idleSince.ToDateTime();
            if (DateTimeOffset.Now - isince > TimeSpan.FromHours(4))
            {
                var pl = await orleans.GetConstructGrain(ship.Item2).GetPlayersInConstruct();
                if (pl.ids.Count == 0)
                {
                    await isp.GetRequiredService<IDataAccessor>().HardDeleteConstructAsync(ship.Item2);
                }
            }
        }
    }
    public async Task RefreshLeaderboard()
    {
        var screen = await isp.GetRequiredService<ISql>().GetConstructLocation(
            new LocationDescriptor
            {
                propertyName = "gameplayTag",
                propertyValue = "lobby_leaderboard",
            });
        var seid = screen[0].Item1;
        var stats = new List<(ulong, PlayerStats)>();
        var statfiles = Directory.GetFiles("/OrleansGrains/Mods/FFF", "stats-*");
        foreach (var fn in statfiles)
        {
            var j = System.IO.File.ReadAllText(fn);
            var s = JsonConvert.DeserializeObject<PlayerStats>(j);
            var re = new System.Text.RegularExpressions.Regex("[0-9]+");
            var match = re.Match(fn);
            var pid = UInt64.Parse(match.Groups[0].Captures[0].Value);
            stats.Add((pid, s));
        }
        var ostats = stats.OrderBy(s=>-(long)(s.Item2.teamKills+s.Item2.selfKills)).Take(5).ToList();
        var res = "";
        foreach (var os in ostats)
        {
            var pname = (await orleans.GetPlayerGrain(os.Item1).GetPlayerInfo()).name;
            res = res + pname + ": " + (os.Item2.teamKills+os.Item2.selfKills) + "\\n";
        }
        var payload = @"<NGUI>
local rslib=require(""rslib"")
local text=""PAYLOAD""
local config={ fontSize = 50}
rslib.drawQuickText(text, config)
".Replace("PAYLOAD", res);
        var bytes = System.Text.Encoding.UTF8.GetBytes(payload);
        var comp = new byte[bytes.Length+64];
        var sz = LZ4Codec.Encode(bytes, 0, bytes.Length, comp, 0, comp.Length);
        var final = new byte[sz+4];
        System.Buffer.BlockCopy(comp, 0, final, 4, sz);
        System.Buffer.BlockCopy(BitConverter.GetBytes((int)bytes.Length), 0, final, 0, 4);
        await orleans.GetConstructElementsGrain(lobbyConstructId).UpdateElementProperty(
            new ElementPropertyUpdate
            {
                constructId = lobbyConstructId,
                elementId = seid,
                name = "content_2",
                value = new PropertyValue(final),
                timePoint = TimePoint.Now(), 
            });
    }
    public async Task ShowPlayerStats(ulong pid, bool show)
    {
        var screen = await isp.GetRequiredService<ISql>().GetConstructLocation(
            new LocationDescriptor
            {
                propertyName = "gameplayTag",
                propertyValue = "lobby_playerstats",
            });
        var seid = screen[0].Item1;
        string payload = @"<NGUI>
local rslib=require(""rslib"")
local text=""Step on plate""
local config={ fontSize = 50}
rslib.drawQuickText(text, config)
";
        if (show)
        {
            PlayerStats s = null;
            try
            {
                var j = System.IO.File.ReadAllText($"Mods/FFF/stats-{pid}");
                s = JsonConvert.DeserializeObject<PlayerStats>(j);
            }
            catch (Exception)
            {}
            if (s == null)
                payload = @"<NGUI>
local rslib=require(""rslib"")
local text=""System error: Unknown""
local config={ fontSize = 50}
rslib.drawQuickText(text, config)
";
            else
            {
                var res = $"Battles: {s.lead+s.join}\\nKills: {s.teamKills+s.selfKills}\\nDeath: {s.deathByWeapon+s.deathByCore+s.deathByElement}";
                payload = @"<NGUI>
local rslib=require(""rslib"")
local text=""PAYLOAD""
local config={ fontSize = 50}
rslib.drawQuickText(text, config)
".Replace("PAYLOAD", res);
            }
        }
        var bytes = System.Text.Encoding.UTF8.GetBytes(payload);
        var comp = new byte[bytes.Length+64];
        var sz = LZ4Codec.Encode(bytes, 0, bytes.Length, comp, 0, comp.Length);
        var final = new byte[sz+4];
        System.Buffer.BlockCopy(comp, 0, final, 4, sz);
        System.Buffer.BlockCopy(BitConverter.GetBytes((int)bytes.Length), 0, final, 0, 4);
        await orleans.GetConstructElementsGrain(lobbyConstructId).UpdateElementProperty(
            new ElementPropertyUpdate
            {
                constructId = lobbyConstructId,
                elementId = seid,
                name = "content_2",
                value = new PropertyValue(final),
                timePoint = TimePoint.Now(), 
            });
    }
    public async Task<LoginResponseOrCreation> GetLoginResponseOrCreation(IIncomingGrainCallContext ctx, AuthUserInfo authInfo)
    {
        ulong playerId = (ulong)((IGrainWithIntegerKey)ctx.Grain).GetPrimaryKeyLong();
        await orleans.GetPlayerGrain(playerId).UpdatePlayerPropertyEx("ftueStep",
            new PropertyValue(0), true);
        var userInfo = await isp.GetRequiredService<ISql>().GetPlayerInfoById(playerId);
        var res = await orleans.GetPlayerGrain(playerId).GetPlayerLoginState(authInfo);
        res.spawnState.location = new RelativeLocation
        {
            constructId = lobbyConstructId,
            position = lobbySpawnPosition,
            rotation = Quat.Identity,
        };
        res.spawnState.constructTree = await isp.GetRequiredService<IScenegraph>().GetTree(lobbyConstructId);
        return new LoginResponseOrCreation
            {
                displayName = authInfo.displayName,
                kind = (LoginResponseKind)0,
                optState = res
            };
    }
    public void UpdatePlayerStats(ulong pid, Action<PlayerStats> cb)
    {
        PlayerStats s = new();
        try
        {
            var j = System.IO.File.ReadAllText($"Mods/FFF/stats-{pid}");
            s = JsonConvert.DeserializeObject<PlayerStats>(j);
        }
        catch(Exception)
        {}
        cb(s);
        var jo = JsonConvert.SerializeObject(s);
        System.IO.File.WriteAllText($"Mods/FFF/stats-{pid}", jo);
    }
    public bool ShouldDebounce(ulong pid)
    {
        if (debouncer.TryGetValue(pid, out var tstamp) && DateTime.Now - tstamp < TimeSpan.FromSeconds(20))
            return true;
        debouncer[pid] = DateTime.Now;
        return false;
    }
    public Task PlayerDieOperation(string spid, PlayerDeathInfo deathInfo)
    {
        var pid = UInt64.Parse(spid);
        if (ShouldDebounce(pid))
            return Task.CompletedTask;
        UpdatePlayerStats(pid, s => {
                var r = deathInfo.reason;
                switch (r) {
                case DeathReason.WeaponShot:
                    s.deathByWeapon += 1;
                    break;
                case DeathReason.CoreUnitDestructionKinetic:
                case DeathReason.CoreUnitDestructionPVP:
                    s.deathByCore += 1;
                    break;
                case DeathReason.ForceRespawn:
                    s.deathByRespawn += 1;
                    break;
                default:
                    s.deathByElement += 1;
                    break;
                }
        });
        // reset teaming
        // TODO: this will fsck up stats for remaining crewmembers, kill them?
        myCaptainRequests.TryRemove(pid, out _);
        myCaptain.TryRemove(pid, out _);
        myLieutnants.TryRemove(pid, out _);
        return Task.CompletedTask;
    }
    public Task AddNewNotification(string spid, NotificationMessage notif)
    {
        ulong pid = UInt64.Parse(spid);
        if (ShouldDebounce(pid))
            return Task.CompletedTask;
        if (notif.notificationCode == EnumNotificationCode.PvPConstructDestroyed)
        {
            var kpid = notif.parameters[1].value;
            UpdatePlayerStats(kpid, s => s.selfKills += 1);
            var captain = kpid;
            if (myCaptain.TryGetValue(kpid, out var real))
                captain = real;
            if (myLieutnants.TryGetValue(captain, out var lts))
            {
                foreach (var id in lts)
                {
                    if (id != kpid)
                        UpdatePlayerStats(id, s=>s.teamKills += 1);
                }
            }
            if (captain != kpid)
                UpdatePlayerStats(captain, s=>s.teamKills += 1);
        }
        return Task.CompletedTask;
    }
    public async Task Initialize(IServiceProvider isp)
    {
        // Workaround for mydu orleans mod loader glitch
        Assembly.LoadFrom("/OrleansGrains/Mods/K4os.Compression.LZ4.dll");
        this.isp = isp;
        this.orleans = isp.GetRequiredService<IClusterClient>();
        this.logger = isp.GetRequiredService<ILogger<MyDuMod>>();
        this.bank = isp.GetRequiredService<IGameplayBank>();
        this.sql = isp.GetRequiredService<ISql>();
        CacheLoadoutChoices();
        var hooks = isp.GetRequiredService<IHookCallManager>();
        hooks.Register(
                "ElementManagementGrain.ElementPropertyUpdate", HookMode.Replace, this,
                "ElementPropertyUpdate");
        hooks.Register(
                "PlayerGrain.PlayerDieOperation", HookMode.PreCall, this,
                "PlayerDieOperation");
        hooks.Register(
                "PlayerGrain.GetLoginResponseOrCreation", HookMode.Replace, this,
                "GetLoginResponseOrCreation");
        hooks.Register(
            "NotificationGrain.AddNewNotification", HookMode.PreCall, this,
            "AddNewNotification");
        // fetch lobby info
        var res = await isp.GetRequiredService<ISql>().GetConstructLocation(
            new LocationDescriptor
            {
                propertyName = "gameplayTag",
                propertyValue = "lobby_entrypoint",
            });
        lobbyConstructId = res[0].Item2;
        lobbySpawnPosition = new Vec3 { x = res[0].Item4, y = res[0].Item5, z = res[0].Item6};
        lobbyUniversePosition = (await orleans.GetConstructInfoGrain(lobbyConstructId).Get()).rData.position;
        res = await isp.GetRequiredService<ISql>().GetConstructLocation(
            new LocationDescriptor
            {
                propertyName = "gameplayTag",
                propertyValue = "lobby_ships",
            });
        lobbyContainerId = res[0].Item1;
    }
    public async Task<ModInfo> GetModInfoFor(ulong playerId, bool admin)
    {
        try
        {
            await RefreshLeaderboard(); 
        }
        catch(Exception e)
        {
            logger.LogError(e, "refresh leaderboard");
        }
        myCaptainRequests.TryRemove(playerId, out _);
        myCaptain.TryRemove(playerId, out _);
        myLieutnants.TryRemove(playerId, out _);
        var rnIds = await orleans.GetConstructElementsGrain(lobbyConstructId).GetElementsOfType<NQutils.Def.ResurrectionNodeUnit>();
        try
        {
            await orleans.GetResurrectionNodeGrain().AddResurrectionNode(playerId, rnIds[0]);
        }
        catch (Exception)
        {
        }
        var res = new ModInfo
        {
            name = GetName(),
            actions = new List<ModActionDefinition>(),
        };
        res.actions.Add(
            new ModActionDefinition
            {
                id = 1,
                label = "Request as my captain",
                context = ModActionContext.Avatar,
            });
        res.actions.Add(
            new ModActionDefinition
            {
                id = 2,
                label = "Confirm as my crewmember",
                context = ModActionContext.Avatar,
            });
        return res;
    }
    public async Task TriggerAction(ulong playerId, ModAction action)
    {
        try
        {
            System.IO.Directory.CreateDirectory("Mods/FFF");
        }
        catch(Exception)
        {}
        if (action.actionId == 1)
        {
            var myName = (await orleans.GetPlayerGrain(playerId).GetPlayerInfo()).name;
            if (myCaptain.TryGetValue(playerId, out var curCaptain))
            {
                if (curCaptain == action.playerId)
                    return; // dup
                myCaptain.TryRemove(playerId, out _);
            }
            myCaptainRequests.TryAdd(playerId, action.playerId);
            await isp.GetRequiredService<IPub>().NotifyTopic(Topics.PlayerNotifications(action.playerId),
                    new NQutils.Messages.ModTriggerHudEventRequest(new ModTriggerHudEvent
                        {
                            eventName = "modinjectjs",
                            eventPayload = "CPPHud.addFailureNotification(\"A candidate crewmember is waiting approval: " + myName + "\");",
                        }));
        }
        if (action.actionId == 2)
        {
            if (myCaptain.TryGetValue(action.playerId, out var curCaptain))
            {
                if (curCaptain == playerId)
                    return; // dup
                await isp.GetRequiredService<IPub>().NotifyTopic(Topics.PlayerNotifications(action.playerId),
                    new NQutils.Messages.ModTriggerHudEventRequest(new ModTriggerHudEvent
                        {
                            eventName = "modinjectjs",
                            eventPayload = "CPPHud.addFailureNotification(\"Player already teamed up\");",
                        }));
                return;
            }
            if (myCaptainRequests.TryGetValue(action.playerId, out var curRequest) && curRequest == playerId)
            {
                myCaptain.TryAdd(action.playerId, playerId);
                if (myLieutnants.TryGetValue(playerId, out var lts))
                    lts.Add(action.playerId);
                else
                    myLieutnants.TryAdd(playerId, new List<ulong>{action.playerId});
                await isp.GetRequiredService<IPub>().NotifyTopic(Topics.PlayerNotifications(action.playerId),
                    new NQutils.Messages.ModTriggerHudEventRequest(new ModTriggerHudEvent
                        {
                            eventName = "modinjectjs",
                            eventPayload = "CPPHud.addFailureNotification(\"Teamup successful\");",
                        }));
                await isp.GetRequiredService<IPub>().NotifyTopic(Topics.PlayerNotifications(playerId),
                    new NQutils.Messages.ModTriggerHudEventRequest(new ModTriggerHudEvent
                        {
                            eventName = "modinjectjs",
                            eventPayload = "CPPHud.addFailureNotification(\"Teamup successful\");",
                        }));
            }
            else
            {
                await isp.GetRequiredService<IPub>().NotifyTopic(Topics.PlayerNotifications(playerId),
                    new NQutils.Messages.ModTriggerHudEventRequest(new ModTriggerHudEvent
                        {
                            eventName = "modinjectjs",
                            eventPayload = "CPPHud.addFailureNotification(\"No request pending\");",
                        }));
            }
        }
        if (action.actionId == 100)
        {
            var res = JsonConvert.DeserializeObject<ClientLoadoutReply>(action.payload);
            System.IO.File.WriteAllText($"Mods/FFF/loadout-{playerId}.json", JsonConvert.SerializeObject(res));
        }
    }
}