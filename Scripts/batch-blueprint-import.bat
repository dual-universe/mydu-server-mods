:: USAGE
:: Put me in the server install directory
:: Create u subdirectory named "blueprints" and put your blueprint files (and only that) in it
:: Double-click me in file explorer to import
:: Edit the '2' playerId to set creator player id to something else than Aphelia

docker-compose run --entrypoint bash --rm -v "%cd%/blueprints:/blueprints" sandbox  -c "for f in $(ls /blueprints/*); do cat $f | base64 -w 0 | sed 's/.*/@&@/' | tr @  $(echo -e '\x22') | curl  -o /dev/null  -H Content-Type:\ application/json -d  '@-' 'http://orleans:10111/blueprint/import?creatorPlayerId=2&creatorOrganizationId=0'  ; done"
pause
