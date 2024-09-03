set moddir=%1
set selfdir=%~dp0
echo %selfdir%
cd %moddir%

docker run --rm -v "%cd%:/output" --entrypoint bash novaquark/dual-server-orleans -c "for f in NQutils Backend Backend.PubSub Backend.Telemetry Interfaces BotLib Router.Orleans Router;do cp /OrleansGrains/$f.dll /output;done"

docker build -t mydu_mod_tmp -f "%selfdir%\Dockerfile.mod" .

mkdir Build
docker run --rm --entrypoint bash -v "%cd%\Build:/output" mydu_mod_tmp -c "cp /install/Mod/* /output/"
pause