#! /bin/bash

set -e

if ! test -d $1 || ! test -f $1/postgres-dual.sql ; then
    echo 'First argument is not a directory with backups'
    exit 1
fi

if ! test -d scripts || ! test -f ./docker-compose.yml ; then
    echo 'Must be run from toplevel install directory'
    exit 1
fi

if ! test -x $(which realpath); then
    echo 'The realpath utility is required, please install it'
    exit 1
fi

if test z$2 != zforce; then
read -p "Are you sure you want to wipe your current myDU server and restore backup (y/n)?" choice
case "$choice" in
y)
    ;;
*)
    echo 'Aborting'
    exit 1
    ;;
esac
fi


bdir=$1

docker-compose down
docker-compose up -d postgres mongo redis
sleep 5
echo 'Reseting postgres databases...'
docker-compose run -v $PWD/scripts/init_dbs.sh:/init_dbs.sh --entrypoint /bin/bash sandbox /init_dbs.sh
echo 'Restoring postgres orleans database...'
docker-compose run -e PGPASSWORD=dual -v $(realpath $bdir)/postgres-orleans.sql:/postgres-orleans.sql --entrypoint psql sandbox -ab -d orleans -U dual -f /postgres-orleans.sql --host postgres
echo 'Restoring postgres dual database...'
docker-compose run -e PGPASSWORD=dual -v $(realpath $bdir)/postgres-dual.sql:/postgres-dual.sql --entrypoint psql sandbox -ab -d dual -U dual -f /postgres-dual.sql --host postgres
echo 'Restoring mongo databases...'
docker-compose run -v $(realpath $bdir):/input --entrypoint mongorestore sandbox --drop 'mongodb://mongo:mongo@mongo:27017/dev_du?authSource=admin' /input/dev_du
docker-compose run -v $(realpath $bdir):/input --entrypoint mongorestore sandbox --drop 'mongodb://mongo:mongo@mongo:27017/dual?authSource=admin' /input/dual
echo 'Creating indexes....'
docker-compose run --entrypoint /python/redisclear sandbox --cfg /config/dual.yaml
echo 'Restoring user content'
cp -r $bdir/user_content ./data