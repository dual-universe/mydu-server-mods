FROM debian:bookworm AS nq_server_build

ADD https://packages.microsoft.com/config/debian/10/packages-microsoft-prod.deb /tmp/packages-microsoft-prod.deb
RUN apt update && apt install -y ca-certificates
RUN dpkg -i /tmp/packages-microsoft-prod.deb
RUN apt update && apt install -y dotnet-sdk-7.0

COPY . /source
RUN cd /source && dotnet publish --no-self-contained \
    /nodeReuse:false -r linux-x64 \
    -p:UseSharedCompilation=false -c Release -o /install/Mod


