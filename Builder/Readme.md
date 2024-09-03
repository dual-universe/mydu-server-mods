# Building dll mods on Windows the easy way

This directory contains a script and a Dockerfile allowing to build any Mod DLL
source dir.

It produces the executable DLL you need to put in your server "Mods" directory.


## Usage

Open two file explorer windows, one to this directory, the other to the
parent directory of your mod sources.

Drag and drop the mod source directory on top of the builder.bat file.

Your mod DLL will be put in the Build subdirectory of the mod sources.

Only copy the actual mod dll and not the other files to the server.
