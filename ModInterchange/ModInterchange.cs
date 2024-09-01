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

public class ICConfig
{
    public bool enforceDRM { get; set; } = true;
    public bool allowSingleUseBlueprint { get; set; } = false;
    public bool allowImport { get; set;} = true;
    public bool allowExport { get; set;} = true;
}

public class MyDuMod: IMod
{
    private IServiceProvider isp;
    private IClusterClient orleans;
    private ILogger logger;
    private HttpClient client;
    private ConcurrentDictionary<ulong, bool> hasPanel = new();
    private ICConfig config = new();
    public string GetName()
    {
        return "NQ.Interchange";
    }
    public Task Initialize(IServiceProvider isp)
    {
        this.isp = isp;
        this.orleans = isp.GetRequiredService<IClusterClient>();
        this.logger = isp.GetRequiredService<ILogger<MyDuMod>>();
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
        this.client = new HttpClient(handler);
        if (File.Exists("/OrleansGrains/Mods/ModInterchange.json"))
        {
            var jdata = File.ReadAllText("/OrleansGrains/Mods/ModInterchange.json");
            config = JsonConvert.DeserializeObject<ICConfig>(jdata);
            logger.LogInformation("ModInterchange initialized from config");
        }
        else
            logger.LogInformation("ModInterchange initialized with default config");
        return Task.CompletedTask;
    }
    public Task<ModInfo> GetModInfoFor(ulong playerId, bool admin)
    {
        hasPanel.Remove(playerId, out var _);
        var res = new ModInfo
        {
            name = GetName(),
            actions = new List<ModActionDefinition>(),
        };
        if (config.allowExport)
        {
            res.actions.Add(
                new ModActionDefinition
                {
                    id = 1,
                    label = "Interchange\\Export slot 1 blueprint",
                    context = ModActionContext.Global,
                });
            res.actions.Add(
                new ModActionDefinition
                {
                    id = 2,
                    label = "Interchange\\Export last slot blueprint",
                    context = ModActionContext.Global,
                });
        }
        if (config.allowImport)
        {
            res.actions.Add(
                new ModActionDefinition
                {
                    id = 3,
                    label = "Interchange\\Import blueprint URL",
                    context = ModActionContext.Global,
                });
        };
        return Task.FromResult(res);
    }
    private async Task ExportBlueprint(ulong playerId, bool firstSlot)
    {
        var pig = orleans.GetInventoryGrain(playerId);
        var si = await pig.Get(playerId);
        StorageSlot hit = null;
        if (firstSlot)
        {
            hit = si.content.Where(s => s.position == 0).FirstOrDefault();
        }
        else
        {
            hit = si.content.Aggregate((agg, next)=> agg.position > next.position ? agg:next);
        }
        if (hit == null)
        {
            await Notify(playerId, "Slot not found");
            return;
        }
        var bpId = hit.content.id;
        if (bpId == 0)
        {
            await Notify(playerId, "Bad item type in target slot");
            return;
        }
        var type = hit.content.type;
        var bank = isp.GetRequiredService<IGameplayBank>();
        var sql = isp.GetRequiredService<ISql>();
        var bd = bank.GetDefinition(type);
        var valid = false;
        if (bd.Is<NQutils.Def.Blueprint>())
            valid = true;
        else if (config.allowSingleUseBlueprint && bd.Is<NQutils.Def.SingleUseBlueprint>())
            valid = true;
        if (!valid)
        {
            await Notify(playerId, "Bad item type in target slot");
            return;
        }
        bool drmProtect = false;
        if (config.enforceDRM)
        {
            ulong coreType = 0;
            var bpModel = await sql.Read(bpId);
            var creator = bpModel.JsonProperties.serverProperties.creatorId;
            bool isMe = false;
            if (creator.playerId == 0 && creator.organizationId == 0)
                isMe = true;
            if (creator.playerId != 0 && creator.playerId == playerId)
                isMe = true;
            if (creator.organizationId != 0)
            {
                isMe = await orleans.GetOrganizationGrain(creator.organizationId).IsLegate(new EntityId{playerId = playerId}); 
            }
            if (!isMe)
            {
                drmProtect = true;
                var requiredItems = await sql.GetIngredients(bpId);
                foreach (var ri in requiredItems)
                {
                    if (bank.GetBaseObject<NQutils.Def.CoreUnit>(ri.id) != null)
                    {
                        coreType = ri.id;
                        break;
                    }
                }
                var coreDrm = await sql.BlueprintCoreDRMGet(bpId, coreType);
                if (coreDrm)
                {
                    await Notify(playerId, "core DRM is enabled and you are not the creator");
                    return;
                }
            }
        }
        var exp = await orleans.GetBlueprintGrain().Export(bpId, drmProtect);
        string res = Newtonsoft.Json.JsonConvert.SerializeObject(exp);
        var bin = System.Text.Encoding.UTF8.GetBytes(res);
        var uid = Guid.NewGuid().ToString() + ".json";
        File.WriteAllBytes("/NQInterchange/" + uid, bin);
        var url = Config.Instance.backoffice.public_url + "/nqinterchange/"+uid;
        await Notify(playerId, "Your blueprint is ready at " + url);
        logger.LogInformation("ModInterchange export of {blueprintId} by {playerId} at {url}", bpId, playerId, url);
    }
    private async Task Notify(ulong playerId, string message)
    {
        await orleans.GetChatGrain(2).SendMessage(
            new MessageContent
            {
                channel = new MessageChannel
                {
                    channel = MessageChannelType.PRIVATE,
                    targetId = playerId
                },
                message = message,
            });
    }
    private async Task ImportBlueprint(ulong playerId, string url)
    {
        System.Memory<byte> payload = null;
        try {
             payload = await client.GetRaw(url);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Blueprint download failure on {url}", url);
            await Notify(playerId, "Download failure at " + url);
            throw;
        }
        BlueprintId bpId = 0;
        try {
             bpId = await isp.GetRequiredService<IDataAccessor>().BlueprintImport(payload.Span.ToArray(), new EntityId { playerId = playerId});
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Blueprint import from {url} failure", url);
            await Notify(playerId, "Import failure at " + url);
            throw;
        }
        var bpInfo = await orleans.GetBlueprintGrain().GetBlueprintInfo(bpId);
        var bpModel = await isp.GetRequiredService<ISql>().Read(bpId);
        if (bpModel.FreeDeploy && ! await orleans.GetPlayerGrain(playerId).IsAdmin())
        {
            logger.LogWarning("Non-admin player {playerId} imported magic blueprint {bpId}, refusing to give it", playerId, bpId);
            await Notify(playerId, "You are not allowed to import free deploy blueprints");
        }
        var pig = orleans.GetInventoryGrain(playerId);
        var itemStorage = isp.GetRequiredService<IItemStorageService>();
        await using var transaction = await itemStorage.MakeTransaction(
            Tag.HttpCall("givebp") // use a dummy tag, those only serves for logging/tracing
            );
        var item =new ItemInfo
        {
            type = isp.GetRequiredService<IGameplayBank>().GetDefinition("Blueprint").Id,
            id = bpId
        };
        item.properties.Add("name", new PropertyValue { stringValue = bpInfo.name});
        item.properties.Add("size", new PropertyValue { intValue = (long)bpInfo.size.x });
        item.properties.Add("static", new PropertyValue { boolValue = bpInfo.kind != ConstructKind.DYNAMIC });
        item.properties.Add("kind", new PropertyValue { intValue = (int)bpInfo.kind });

        await pig.GiveOrTakeItems(transaction,
                    new List<ItemAndQuantity>() {
                        new ItemAndQuantity
                        {
                            item = item,
                            quantity = 1,
                        },
                    },
                    new());
        await transaction.Commit();
        await Notify(playerId, "Blueprint '"+bpInfo.name+"' imported");
        logger.LogInformation("Imported blueprint {bpId} from {url} by player {playerId}", bpId, url, playerId);
    }
    public async Task TriggerAction(ulong playerId, ModAction action)
    {
        if (action.actionId == 1 && config.allowExport)
            await ExportBlueprint(playerId, true);
        else if (action.actionId == 2 && config.allowExport)
            await ExportBlueprint(playerId, false);
        else if (action.actionId == 3 && config.allowImport)
        {
            if (!hasPanel.ContainsKey(playerId))
            {
                await isp.GetRequiredService<IPub>().NotifyTopic(Topics.PlayerNotifications(playerId),
                    new NQutils.Messages.ModTriggerHudEventRequest(new ModTriggerHudEvent
                        {
                            eventName = "modinjectjs",
                            eventPayload = panel,
                        }));
                await Task.Delay(1000);
                hasPanel[playerId] = true;
            }
            
            await isp.GetRequiredService<IPub>().NotifyTopic(Topics.PlayerNotifications(playerId),
            new NQutils.Messages.ModTriggerHudEventRequest(new ModTriggerHudEvent
                {
                    eventName = "NQInterchangePanel.show",
                    eventPayload = "1",
                }));
        }
        else if (action.actionId == 100 && config.allowImport)
            await ImportBlueprint(playerId, action.payload);
    }
    private readonly string panel = @"
    class NQInterchangePanel extends MousePage
    {
      constructor()
      {
        super();
        this._createHTML();
        this.wrapperNode.classList.add(""hide"");
        engine.on(""NQInterchangePanel.show"", this.show, this);
      }
      show(isVisible)
      {
          super.show(isVisible);
      }
      _onVisibilityChange()
      {
        super._onVisibilityChange();
        this.wrapperNode.classList.toggle(""hide"", !this.isVisible);
        CPPInput.setCaptureKeyboard(!!this.isVisible);
      }
      _close()
      {
          this.show(false);
      }
      _createHTML()
      {
          this.HTMLNodes = {};
          this.wrapperNode = createElement(document.body, ""div"", ""mining_unit_panel"");

          let header = createElement(this.wrapperNode, ""div"", ""header"");
          this.HTMLNodes.panelTitle = createElement(header, ""div"", ""panel_title"");
          this.HTMLNodes.panelTitle.innerText = ""Type in blueprint URL"";
          this.HTMLNodes.closeIconButton = createElement(header, ""div"", ""close_button"");
          this.HTMLNodes.closeIconButton.addEventListener(""click"", () => this._close());

          createSpriteSvg(""icon_close"", ""icon_close"", this.HTMLNodes.closeIconButton);
          let content = createElement(this.wrapperNode, ""div"", ""content"");
          content.style.display = 'block';
          let wrapper = createElement(content, ""div"", ""content_wrapper"");
          this.HTMLNodes.qinput = createElement(wrapper, ""input"");
          this.HTMLNodes.qinput.type = ""text"";
          let button = createElement(wrapper, ""div"", ""generic_button"");
          button.innerText = ""import"";

          button.addEventListener(""click"", ()=>this.blueprint());
      }
      blueprint()
      {
          let value = this.HTMLNodes.qinput.value;
          CPPMod.sendModAction(""NQ.Interchange"", 100, [], value);
      }
    }
    let nqInterchangePanel = new NQInterchangePanel();
    ";
}
