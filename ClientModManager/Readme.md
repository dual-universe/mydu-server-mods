# *EXPERIMENTAL* Server-driven Mod manager for MyDU

The goal of this project is to facilitate the distribution of client mods
like new assets by server administrators.


## Installation and usage

### Installing the mod manager clientside

Download the last version from the GitHub releases and place the executable
one level below the MyDU 'Game' directory, which means by default
"c:\ProgramData\My Dual Universe".

Alternatively if you have python installed and the wxPython and requests modules
(python3 -m pip install wxPython requests)
you can simply download the source file and run it through python.


## Using the mod manager

In the text field, type the server URL of the server you want to sync mods for,
exactly as it is displayed from the serverlist.

Press the button to sync.

To disable all mods press the button with an empty value in the text field.

## Server-side setup

### First-time setup

Edit 'nginx/conf.d/queueing.conf' and add before the existing locations block:

    location /clientmods {
         alias /clientmods;
    }

Edit docker-compose.yml and add "- ./clientmods:/clientmods" to the nginx volumes.

It should look like:

      nginx:
        image: nginx
        volumes:
          - ./nginx:/etc/nginx 
          - ./data:/data   # need access to user_content
          - ./letsencrypt:/etc/letsencrypt 
          - ./clientmods:/clientmods

## Adding server-driven client mods to your server

Pack each mod as a zip file. You can use files at the root directory in the archive.
You may not:
  - escape the root directory ('../' entries)
  - add executable files

It is recommended to name your mod REVERSEDDOMAIN-VERSION.zip.

Where REVERSEDDOMAIN is the reverse-order of a domain you own (for instance
org.acme.du if you own du.acme.org) and VERSION is a semver or a number.

Mod zip must be immutable, do not ever modify an existing version, if changes are
made you must bump the version number to a new value.

Put the zip files in the clientmods directory.

Finally put in clientmods a file named 'manifest.txt' with the list of mod names
your server enables.

In manifest.txt:

- You can have empty lines or lines starting with '#' for comments
- Entries cannot have path separators or characters invalid in a filesystem.
- You may ommit the '.zip' extension.
