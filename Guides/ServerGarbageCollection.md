# MyDU server garbage collection

Two resources will accumulate and grow forever in a myDU server:

- unreferenced blueprints
- unreferenced user content


## Garbage collecting user content

User content is stored in immutable files in data/user_content. It contains
dynamic properties that are too big to be stored in postgresql, mostly
lua scripts in control units and screen units.

The following sequence of commands will make a list of entries referenced
in database, and delete entries in the usercontent folder not present in that list.

Making a backup of data/user_content beforehand is recommended.

It must be run with postgresql service up, and no player connected to avoid
race conditions.

Open a terminal in the toplevel server install directory and run:

    mkdir gc

to create a directory where to store the temporary files. Then:

    docker compose run --rm --entrypoint bash -v "%cd%\gc:/output" sandbox
    cd /python
    
This last commands will drop you into a bash shell in the sandbox container.

    ./DualSQL/DualSQL /config/dual.yaml --user-content-collect /output/user-content-live.txt

This last command will list all live entries in a file.

    ./DualSQL/DualSQL /config/dual.yaml --user-content-cleanup /output/user-content-live.txt --user-content-cleanup-delay 0 --user-content-cleanup-log-path /output/user-content-gc-log.txt

Finally this command above will delete all entries not listed in the manifest file created beforehand.

## Garbage collecting dereferenced blueprints

A blueprint can exist in the databases but be entirely unreachable ingame, if it
is not present in any inventory, container, or package.

The cleanup process is a two-step sequence. First flag in database (blueprint.dereferenced_at)
blueprints which are no longer used,

    docker compose run --rm --entrypoint bash sandbox
    cd /python
    ./DualSQL/DualSQL /config/dual.yaml --garbage-collect dereference_blueprint

This command above will set the dereferenced_at date for all newly dereferenced blueprints

To actually delete dereferenced blueprints, run

    docker compose run --rm --entrypoint bash -v "%cd%\gc:/output" sandbox
    cd /python
    ./DualSQL/DualSQL /config/dual.yaml --garbage-collect blueprint

This will export and fully delete bluperints dereferenced more than one month ago.

The export location can be configured by an 's3' kind entry in dual.yaml, named 'dump_gc'.
For exemple add the following to dual.yaml:

    dump_gc:
        override_path: /output