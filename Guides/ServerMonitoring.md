# Monitoring a MyDU server

## Enabling orleans dashboard

Orleans has a dashboard web interface that exposes useful orleans metrics like
call rate and latency, active grains, exception rate...

It listens on orleans service port 8099.

To expose it to locahost add an entry " - 127.0.0.1:8099:8099 " to the ports
section of orleans service of docker-compose.yml.

Then reload orleans config "docker-compose up -d orleans" and ponit your browser
to http://localhost:8099 .


# Enabling grafala

The MyDU service use the prometheus system to expose a lot of usefull metrics.

## Enabling orleans prometheus metrics

Edit "config/dual.yaml" and change entry "influx.enabled" to true.

Restart orleans service.


## Adding grafana service

Add the following to docker-compose.yml:

    grafana:
      image: grafana/grafana-enterprise
      restart: unless-stopped
      ports:
       - '127.0.0.1:3000:3000'
      volumes:
        - ./grafana:/var/lib/grafana
      networks:
          vpcbr:
            ipv4_address: 10.5.0.66

then do a "docker-compose up -d grafana" to start it.

Note: you might need to change the user of the ./grafana folder by doing on Linux
a "sudo chown 472 grafana".

## Setting up grafana

Point your browser to http://localhost:3000 and login as admin/admin. Change the
admin password as you are asked to do.

Configure a datasource of type prometheus with URL "http://prometheus:9090".

Then import the dashboards provided in the "GrafanaDashboards" subdirectory of this file.

