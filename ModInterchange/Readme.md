# ModInterchange

This MyDU mod puts players in control of their blueprints, allowing them to
import and export them.

It provides two new right-clic menu entries:

- export blueprint in first inventory slot: this will send you an URL to
retrieve your blueprint in the chat (DM from Aphelia herself)
- import blueprint: this will spawn a dialog in which you enter the public URL
from which to fetch the blueprint. The blueprint will be imported and put in
your inventory.


# Server setup

A few changes are required server side:

Add the following lines to the nginx and orleans serivices in docker-compose.yml,
in the 'volumes' section for each of them:

    - ./NQInterchange:/NQInterchange

Modify nginx/conf.d/backoffice.conf by adding before the existing 'location':

     location /NQInterchange {
        alias /NQInterchange;
    }

Then copy the ModInterchange dll into the Mods directory of your server, and
restart orleans.

Note that the ModInterchange directory is not garbage-collected, this is left
as an exercise to the reader.

# Configuration

The mod can optionally read a file named "ModInterchange.json" in the same
directory as the installed mod dll. Here is a sample with default values:

    {
      "enforceDRM": true,
      "allowSingleUseBlueprint": false,
      "allowImport": true,
      "allowExport": true
    }