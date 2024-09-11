# How to setup the 1.4.2 legacy territory scanners server-side

Go to backoffice item hierarchy page.

Search "TerritoryScannerUnit", click on "add child" button, name the child
"LegacyTerritoryScannerUnit" (no you can't change that).

Then return to the item hierarchy list, search for the newly created "LegacyTerritoryScannerUnit",
hit "add child" and name this child "LegacyTerritoryScanner" (can't change that either).

On that new entry you can set displayName, description and duration fields.

duration sets the scan duration in seconds. It is recommended to
not set anything below 180 seconds.