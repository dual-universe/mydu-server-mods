# Enabling the mesh exporter with the Docker MyDU server

Starting with server 1.5.12 a new image is available on docker hub: "dual-server-meshserver".

It contains a service accessible through a webpage allowing to produce .glb textured meshes from blueprint files.

This service requires a running mongodb server, but will not interfere with a live server databases.


## Adding mesh exporter to an existing stack:


Make the following addition to docker-compose.yml:

```yaml
services:
    meshserver:
        image: ${NQREPO}dual-server-meshserver:1.5.12
        ports:
         - 8000:8000
        networks:
          vpcbr:
            ipv4_address: 10.5.0.234
```


Then `docker compose up -d meshserver`.

You can then navigate to http://your-server-domain:8000 .

# Caveats

The mesh generation process is quite CPU and RAM heavy. To prevent your server from asploding the service will limit processing to 3 concurrent requests.