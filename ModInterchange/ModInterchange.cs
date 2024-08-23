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
using NQutils.Config;
using Backend.Storage;
using Backend.Scenegraph;
using NQ;
using NQ.Interfaces;
using NQutils;
using NQutils.Net;
using NQutils.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public class MyDuMod: IMod
{
    private IServiceProvider isp;
    private IClusterClient orleans;
    private ILogger logger;
    private HttpClient client;
    private ConcurrentDictionary<ulong, bool> hasPanel = new();
    public string GetName()
    {
        return "NQ.Interchange";
    }
    public Task Initialize(IServiceProvider isp)
    {
        this.isp = isp;
        this.orleans = isp.GetRequiredService<IClusterClient>();
        this.logger = isp.GetRequiredService<ILogger<MyDuMod>>();
        this.client = isp.GetRequiredService<IHttpClientFactory>().CreateClient();
        return Task.CompletedTask;
    }
    public Task<ModInfo> GetModInfoFor(ulong playerId, bool admin)
    {
        hasPanel.Remove(playerId, out var _);
        var res = new ModInfo
        {
            name = GetName(),
            actions = new List<ModActionDefinition>
            {
                new ModActionDefinition
                {
                    id = 1,
                    label = "Interchange\\Export slot 1 blueprint",
                    context = ModActionContext.Global,
                },
                new ModActionDefinition
                {
                    id = 2,
                    label = "Interchange\\Import blueprint URL",
                    context = ModActionContext.Global,
                },
            }
        };
        return Task.FromResult(res);
    }
    private async Task ExportBlueprint(ulong playerId)
    {
        var pig = orleans.GetInventoryGrain(playerId);
        var si = await pig.Get(playerId);
        var hit = si.content.Where(s => s.position == 0).FirstOrDefault();
        if (hit == null)
            return;
        var bpId = hit.content.id;
        if (bpId == 0)
            return;
        var bin = await isp.GetRequiredService<IDataAccessor>().BlueprintExport((long)bpId);
        var uid = Guid.NewGuid().ToString() + ".json";
        File.WriteAllBytes("/NQInterchange/" + uid, bin);
        var url = Config.Instance.backoffice.public_url + "/nqinterchange/"+uid;
        await orleans.GetChatGrain(2).SendMessage(
            new MessageContent
            {
                channel = new MessageChannel
                {
                    channel = MessageChannelType.PRIVATE,
                    targetId = playerId
                },
                message = "Your blueprint is ready at " + url,
            });
    }
    private async Task ImportBlueprint(ulong playerId, string url)
    {
        var payload = await client.GetRaw(url);
        var bpId = await isp.GetRequiredService<IDataAccessor>().BlueprintImport(payload.Span.ToArray(), new EntityId { playerId = playerId});
        var bpInfo = await orleans.GetBlueprintGrain().GetBlueprintInfo(bpId);
        
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
    }
    public async Task TriggerAction(ulong playerId, ModAction action)
    {
        if (action.actionId == 1)
            await ExportBlueprint(playerId);
        else if (action.actionId == 2)
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
        else if (action.actionId == 100)
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
