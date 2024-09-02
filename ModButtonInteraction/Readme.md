# Highly EXPERIMENTAL way to create new user interactions

This folder illustrates a way to add new player interactions to MyDU.

It creates a new Switch element and install a DLL mod that will detect when
instances of that button are pressed.


## Deployment instructions

### Client side

Copy the three files in the ClientSide folder to the data/resources_generated
folder of your client.
The files must be exactly there and not in a subfolder.

### Backoffice side

Go to the item hierarchy and hit the "Add a new child" button on "ManualSwitchUnit".

Name the new entry "CustomButtonA".

### Server side

Compile the mod in this directory and copy the dll in the Mods folder of your
server.

## Testing

Give yourself through backoffice some CustomButtonA item in your inventory.

Place one on a construct and toggle it. You should see a log
"*** custom button triggered" in Grains_dev.log.

