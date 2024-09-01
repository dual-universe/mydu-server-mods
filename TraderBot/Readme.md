# Trader market bot

This program is a bot that will place buy orders for stuff on all markets,
and immediately resell fulfilled orders with a margin.

It's intent is to allow your mining players to always have a place to sell their
ore, without injecting matter in the system.

# Deploying on your server

Refer to the mydu-mod-toolkit documentation pdf.

The bot's player is named 'trader', modify source code to change it.

Adapt to pass a second argument to the program after dual.yaml: trader.json

You will need to give the trader bot a LOT of money (through backoffice) to place orders.

# Configuring

The program reads a json file for instructions. Here is a sample:

    {
      "buyPrices": {},
      "buyRecursivePrices": {
        "Ore1": 2000,
        "Ore2": 4000
      },
      "margin": 1.1,
      "orderQuantity": 100000,
      "orderRefreshRatio": 0.7
    }

It will buy all items by name in buyPrices, and all children item of buyRecursivePrices
at the given price.

