# Fight For Fame : full combat PvP mod

Work in progress!

This dll mod turns a MyDU server into a PvP combat server.

# Player guide

You need to "enable mod javascript" in client general settings.

When connecting you are sent into the Lobby.

## Ship and loadout

You need to first select your ship and loadout (which will be remembered across
sessions).

Hit the button above the "loadout" screen. In the interface that pops up
click on the name of the ship you want.

You will then see a dropdown list per ammo container.

You need to pick at least one ammo type per container: find it in the list,
and then click the "add" button. To remove a selection click on it.

When done click the "validate" button and close the window.

## Teaming up

This step is not required if you want to man a ship alone.

If you want to crew a single ship with other people, you need to decide one
that will be the captain.

All other players need to right-click on their wanted captain and click on
"Request as my captain".

Then and only then the captain can validate by right-clicking on each player
and select "Confirm as my crewmember".

## Fighting

Go across the door below the sign that says "enter battle" and wait here.

Captain must enter first (crewmembers will be delayed until captain enters).

You will be teleported to a ship of the model you selected, fully fueled and
loaded, shields on, ready for action.

# Administrator guide

## Quick setup

Import the "Lobby.json" construct (not a blueprint, use the import form on the constructs page) onto your server.

Set it's owner to an account you control.

Build the dll mod, place it in "Mods" subdirectory, and restart orleans (or the
whole server).
Also copy fff.js and K4os.Compression.LZ4.dll (do a dotnet publish to get it) in the Mods directory.


Login and share the following elements with all players (share element->publicly for all):

- Detection zone behind the door
- Button above "loadout" screen
- The door below "enter battle" sign
- Resurection node
- Pressure plate in front of right screen

Activate the base shild in case it's down.


## Configuring available ships

Put ship blueprints in the container near the core unit. They must be core blueprints,
it doesn't matter if they are magic/freeDeploy or not.
