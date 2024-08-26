# Who is this guide for?

Anyone hosting a stack in a state that:

- Works for people connecting remotely.
- Does not work for people connecting on LAN or local host.

# Why doesn't it work locally anymore?

Because your stack is advertising your public router IP (or domain names that
point to it) and your router is not capable of "hairpin routing", which means
sending packet from your PC, to the router, then back the same way to the server.

# What are my options

## Solution 1: VPN

Enable a VPN on the computer running the client. This however has a monetary
cost and will degrade gaming experience by increasing latency and limiting
bandwidth.

## Solution 2: mydu LAN override + hosts

Warning: this is a bit technical and requires editing files in mydu configuration
and your system configuration files.

This solution requires mydu server version 1.4.2 or above

For this to work your stack must be configured with one or multiple hostname.
You must do one of the below two options:

- Enable SSL mode.
- Use a domain and not IP as second argument to config-set-domain script

### What you'll do and how it works

Two things need to be taken care of:

Firstly your client's connection to the front service,
which is done to an IP address advertised by the queueing service.

Secondly all the http(s) connections made by your client to the various services
during game time.

The first one is taken care of by a LAN override configuration in dual.yaml: the system
can detect that an incoming connexion is a LAN one, and change the advertised
front IP address.

The second one cannot be handled by the stack currently. But you can fix that
by using OS DNS overrides in the 'hosts' system file on both Linux and Windows.


### Step 1: configuring lan override

Get a hold of your LAN IP range and netmask, and the IP address where the
front service runs. In the examples below we will assume address is 192.168.0.42
which gives a network 192.168.0.0 and netmask 255.255.255.0.

Open config/dual.yaml in a text editor, and locate the "queueing" section.
Add the following three lines to it, indenting them like the other lines around:


    lan_address: "192.168.0.0"
    lan_netmask: "255.255.255.0"
    front_lan_address: "192.168.0.42"

Then `docker-compose restart queueing`.

### Step 2: configuring domain names override

On Linux edit as root the file '/etc/hosts', on Windows "c:\Windows\System32\Drivers\etc\hosts".

Find the IP of your stack, and assuming 192.168.0.42 add one line of the form

    192.168.0.42 domain.tld

for each "domain.tld" that is used by your stack:

- If non SSL mode that is one entry with the domain passed as second argument of config-set-domain
- If SSL mode that is 5 entries du-voxel.tld, du-queueing.tld, du-usercontent.tld, du-orleans.tld and du-backoffice.tld
replacing tld with your domain.


### Troubleshooting

If you are still sent to the public front IP after this change, check your
queueing service logs for a line with "Player connecting from".
It will show you the IP address detected by the stack. Make sure it falls
within the range of lan_address/lan_netmask.