set tgt=%date:~10,4%-%date:~7,2%-%date:~4,2%-%time:~0,2%-%time:~3,2%-%time:~6,2%
tgt=$(date +%F)
mkdir "%tgt%"
mkdir "%tgt%\user_content"
docker-compose exec postgres pg_dump -U dual dual > %tgt%\postgres-dual.sql
docker-compose exec postgres pg_dump -U dual orleans > %tgt%\postgres-orleans.sql
docker-compose run --entrypoint mongodump --rm -v "%cd%\%tgt%:/output" sandbox -o /output mongodb://mongo:mongo@mongo/
copy  data\user_content\* %tgt%\user_content
