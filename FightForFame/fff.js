console.warn("fff injection");
class FFF extends MousePage
{
    constructor()
    {
        console.warn("fff constructor");
        super();
        this._createHTML();
        this.wrapperNode.classList.add("hide");
        engine.on("fff.show", this.showLoadout, this);
    }
    showIt(iviz)
    {
        if (iviz)
        {
            hudManager.toggleEnhancedMouse();
            this.show(true);
        }
        else
            this.show(false);
    }
    show(isVisible)
    {
        super.show(isVisible);
    }
    _onVisibilityChange()
    {
        super._onVisibilityChange();
        this.wrapperNode.classList.toggle("hide", !this.isVisible);
        var that = this;
        if (!!this.isVisible)
        {
            hudManager.toggleEnhancedMouse();
            //inputCaptureManager.captureText(true, ()=>that._close());
        }
        if (!this.isVisible)
        {
            inputCaptureManager.captureText(false);
        }
    }
    _close()
    {
        this.show(false);
    }
    injectCSS(css) {
        const style = document.createElement('style');
        style.type = 'text/css';
        style.appendChild(document.createTextNode(css));
        document.head.appendChild(style);
    }
    _createHTML()
    {
        this.injectCSS(`
@keyframes wiggle {
  0%, 100% {
    transform: rotate(-5deg);
  }
  50% {
    transform: rotate(5deg);
  }
}
.wiggle {
  text-align: center;
  align: center;
  animation: wiggle 0.5s ease-in-out;
  animation-iteration-count: 3;
}
`);
        
        this.HTMLNodes = {};
        this.wrapperNode = createElement(document.body, "div", "mining_unit_panel");
        let header = createElement(this.wrapperNode, "div", "header");
        this.HTMLNodes.panelTitle = createElement(header, "div", "panel_title");
        this.HTMLNodes.panelTitle.innerText = "Configure ship and loadout";
        this.HTMLNodes.closeIconButton = createElement(header, "div", "close_button");
        this.HTMLNodes.closeIconButton.addEventListener("click", () => this._close());
        
        createSpriteSvg("icon_close", "icon_close", this.HTMLNodes.closeIconButton);
        let content = createElement(this.wrapperNode, "div", "content");
        content.style.display = 'block';
        let wrapper = createElement(content, "div", "content_wrapper");
        wrapper.style.display = 'block';
        this.HTMLNodes.wrapper = wrapper;
    }
    showLoadout(j)
    {
        this.loadout = JSON.parse(j);
        this.showShips()
    }
    showShips()
    {
        this.HTMLNodes.wrapper.innerHTML = '';
        for (var idx in this.loadout)
        {
            let e = this.loadout[idx];
            let button = createElement(this.HTMLNodes.wrapper, "div", "generic_button");
            button.innerText = e.shipName;
            button.style.border = '1px black solid';
            button.style.padding = '10px';
            let cidx = idx;
            button.addEventListener("click", ()=>this.selectShip(cidx));
        }
        this.show(true);
    }
    selectShip(idx)
    {
        let ship = this.loadout[idx];
        this.shipName = ship.shipName;
        this.HTMLNodes.wrapper.innerHTML = '';
        let localIds = [];
        let alists = [];
        for (var widx in ship.ammos)
        {
            let cwidx = widx;
            let a = ship.ammos[widx];
            let div = createElement(this.HTMLNodes.wrapper, "span");
            div.style.display = 'flex';
            let label = createElement(div, "p");
            label.innerText = a.attachedWeapons.join('+');
            label.style.verticalAlign = 'center';
            label.style.padding = '20px';
            let selector = new DropdownComponent("ammo");
            let labels = {};
            for (var aidx in a.ammoOptions)
            {
                let ao = a.ammoOptions[aidx];
                selector.addListElement(ao.typeId, ao.displayName);
                labels[ao.typeId] = ao.name;
            }
            div.appendChild(selector.wrapperNode);
            let badd = createElement(div, "div", "generic_button");
            badd.style.verticalAlign = 'center';
            badd.innerText = "add";
            badd.style.padding = '20px';
            let alist = createElement(div, "div");
            alist.style.padding = '20px';
            alists.push([]);
            localIds.push(a.localId);
            badd.addEventListener("click", ()=> {
                    let aentry = createElement(alist, "div");
                    aentry.style.border = '1px black solid';
                    aentry.style.padding = '10px';
                    let selId = selector.currentSelectedListElement;
                    alists[cwidx].push(selId);
                    aentry.innerText = labels[selId];
                    aentry.addEventListener("click", () => {
                            alist.removeChild(aentry);
                            alists[cwidx].remove(curval);
                    });
            });
        }
        let val = createElement(this.HTMLNodes.wrapper, "div", "generic_button");
        val.style.textAlign = 'center';
        val.style.padding = '5px';
        val.innerText = "VALIDATE";
        val.addEventListener("click", () => {
                var res = [];
                var valid = true;
                for (var idx in localIds)
                {
                    var e = {};
                    e['localId'] = localIds[idx];
                    e['itemTypes'] = alists[idx];
                    valid = valid && (alists[idx].length > 0);
                    res.push(e);
                }
                if (!valid)
                {
                    val.classList.add('wiggle');
                    val.addEventListener('animationend', function handleAnimationEnd() {
                            val.classList.remove('wiggle');
                            val.removeEventListener('animationend', handleAnimationEnd);
                    });
                    return;
                }
                var payload = JSON.stringify({shipName: this.shipName, loadout: res});
                CPPMod.sendModAction("NQ.FFF", 100, [], payload);
                this.show(false);
        });
    }
}
console.warn("fff construction");
document.fff = new FFF();
console.warn("fff all good");