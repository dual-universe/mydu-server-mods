# Configuring teleporters

From 1.4.4 server version teleporters can be configured through the Backoffice.

Here is the process to do so:


## Source

The teleportation origin point must be a TeleportationNode.

Once it is placed in game in a construct, go to the backoffice, constructs, open the page for the
construct containing your teleporter, go to elements tab, locate the teleporter,
and go to the element properties tab.

Next pick an unique name for your source<->destination pair, say "foo", and use
the "add property" form at the bottom of the page:

Select "teleporter_destination" from the dropdown, enter "foo" without the quotes
in the text field, and hit "Submit". The new property should appear in the list.

## Destination

Pick any in game element, go to it's backoffice element properties tab, and this
time select "gameplayTag" in the dropdown, and enter "foo" in the text field, then
Submit.

# Pitfalls

If you unhide the TeleportationNode in item hierarchy, be sure to also unhide
the parent node or you will break the client item bank.

Be careful in chosing your teleporter_destination/gameplayTag names are they are
not scoped.

# Exposing it to players

One could use a mod dll to allow players to set this two properties. If you
implement this adding a prefix to the inserted property values is recommended
so players can't use each other's tags.
