# CLI and backoffice Item Bank patch system

Starting from server version 1.4.8, a system of item bank patching was added.

It allows creating and applying item bank patch files for easier sharing of
item bank modifications.

## Backoffice interface

### Making a patch

A patch is always made from a backup file to current Item Hierarchy version.

Open backoffice Item Hierarchy page in a browser.

Select the "from" itemBank from a file on your computer, and hit the "Make Patch"
button.

The resulting computed patch will appear on screen. Use the "copy to clipboard"
button to save it.

### Applying a patch

Always make a backup using the "Download" button before applying a patch.

Select a patch file (or multiple patch files) and hit the "Verify Patch" button.

If multiple files are given they are sorted by ascending file name alphabetically.

Your browser will show eventual warning or errors, and then the resulting diff
that would be applied to current item bank.

If you are happy with the result, repeat the process but this time press the
"Apply patch" button.

## CLI interface

The DualSQL program in the sandbox image has a CLI interface to manipulate patches.

### Calling DualSQL

Replace ARGUMENTS with your actual arguments explained below.

On windows:

    docker-compose run --rm -v "%cd%:/input" --entrypoint /python/DualSQL/DualSQL sandbox ARGUMENTS

On Linux:

    docker-compose run --rm -v "$PWD:/input" --entrypoint /python/DualSQL/DualSQL sandbox ARGUMENTS

### Making a patch

    DualSQL [--bank-from FILE_TARGET] --bank-output PATCH_RESULT_FILE --bank-make-patch-from FILE_SOURCE

Will generate a patch from FILE_SOURCE to current item bank (or FILE_TARGET if argument is given).

Will write output to PATCH_RESULT_FILE.

### Applying a patch

    DualSQL [--bank-from FILE_BANK] --bank-output BANK_RESULT_FILE --bank-patch PATCH_FILE

Will compute application of a patch found in PATCH_FILE from current item bank state
(or FILE_BANK if given). Will write resulting importable item bank to BANK_RESULT_FILE.

