
if NOT EXIST %1\postgres-dual.sql (echo "Argument must be backup directory" && exit /B)

if NOT EXIST docker-compose.yml (echo "Must be run from toplevel server install directory" && exit /B)

set /p var=This will wipe your current server state and restore backup, are You Sure?[Y/N]: 
if not %var%== Y exit /B


pushd %1
set ABS_PATH=%CD%
popd

docker-compose down
docker-compose up -d postgres mongo redis
@echo "waiting for postgress to boot, be patient..."
:loop
timeout /t 5 /nobreak
docker-compose exec postgres psql -U postgres -c "select 1;"
if ERRORLEVEL 1 goto loop

echo 'Reseting postgres databases...'
docker-compose run -v "%cd%/scripts/init_dbs.sh:/init_dbs.sh" --entrypoint /bin/bash sandbox /init_dbs.sh
echo 'Restoring postgres orleans database...'
docker-compose run -e PGPASSWORD=dual -v "%ABS_PATH%/postgres-orleans.sql:/postgres-orleans.sql" --entrypoint psql sandbox -ab -d orleans -U dual -f /postgres-orleans.sql --host postgres
echo 'Restoring postgres dual database...'
docker-compose run -e PGPASSWORD=dual -v "%ABS_PATH%/postgres-dual.sql:/postgres-dual.sql" --entrypoint psql sandbox -ab -d dual -U dual -f /postgres-dual.sql --host postgres
echo 'Restoring mongo databases...'
docker-compose run -v "%ABS_PATH%:/input" --entrypoint mongorestore sandbox --drop "mongodb://mongo:mongo@mongo:27017/dev_du?authSource=admin" /input/dev_du
docker-compose run -v "%ABS_PATH%:/input" --entrypoint mongorestore sandbox --drop "mongodb://mongo:mongo@mongo:27017/dual?authSource=admin" /input/dual
echo 'Creating indexes....'
docker-compose run --entrypoint /python/redisclear sandbox --cfg /config/dual.yaml
echo 'Restoring user content'
copy /Y %1\user_content\* data\user_content