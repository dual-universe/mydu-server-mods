# I'm on a dynamic IP address, how can I host a MyDU server?

## Pick a dynamic DNS provider and set up your hostname

Many providers allow you to setup a "dynamic DNS" entry: a domain name
that points back to your IP address and adjusts itself when it changes.

So pick one and set it up.

## Configure the server

Let's assume your domain is roger.dynamicdnsprovider.com. Replace it by your
actual domain in the following instructions.

Run the config-set-domain step from the documentation by using your domain:

    scripts\config-set-domain.bat config/dual.yaml http://roger.dynamicdnsprovider.com 127.0.0.1

Then open config/dual.yaml in a text editor, locate the "queueing:" section
and add the following three lines so that it looks like:

    queueing:
        lan_address: 0.0.0.0
        lan_netmask: 0.0.0.0
        front_lan_address: roger.dynamicdnsprovider.com

Leave the other lines like they are in the queueing section.

## Setup port forwarding

Follow the server documentation for this step

## Restart the server

It should work fine now.

If it works remotely but no longer from local computer or LAN, follow the 'hosts'
step of the RouterWithoutHairpinSupport guide.