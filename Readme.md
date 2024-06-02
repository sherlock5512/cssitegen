# CsSiteGen
A relatively simple (for now) static site "generator".
The heavy lifting of this project is actually performed by `pandoc` so you
better make sure you install it on your system.

## How to use
The configuration for a site is stored in a file called `cssitegen.json`
this file can technically be located anywhere but the recommended directory
structure is as follows:
```
-Project-Root
 |
 |-Source_Directory
 | |
 | |- [source files...]
 |
 |-Destination_Directory
 |
 |-cssitegen.json
```
This makes the file much simpler to write, However with fully specified
paths you can actually place the files anywhere.

### Structure of `cssitegen.json`
All `cssitegen.json` files must specify at minimum the source, and
destination directories, If the baseurl is omitted it will simply be null.
You must also ensure that you follow the JSON format properly.
The following fields are used in a file
- Source - a string that locates the source files. This can either be the
full path or a relative path.
- Destination - As above but this is where the output files will go.
- BaseUrl - a string or `null` this will be used to replace the string
`%BASEURL%` in supported files. (Depending on your usage you may need to
specify the protocol)

#### Example file
```json
{
    "Source" : "./Source",
    "Destination" : "./Destination",
    "BaseUrl" : "https://example.org"
}
```

### Executing the program
Currently the program has two subcommands that can be used, and an optional
project argument, if not specified the program assumes that the project is
the current directory.

The program is called like this
```sh
cssitegen SUBCOMMAND [--project "directory"]
```
#### convert
This subcommand performs conversion of the files.
It only considers source files for conversion if they are new or have been
changed since the last run of the program.
Therefore if you change the `BaseUrl` or the template file you will need to
clean and convert your entire project.
Convert also does not detect deleted source files at the moment, so you
will also need to clean for that,

#### clean
This subcommand purges the output directory, You can of course do this
manually but for convenience this is implemented here.

### Templating
The templating system is managed by `pandoc` so please check the
documentation for `pandoc` to learn about the format.

As for where to place the template files,
for every file converted using `pandoc` the program will attempt to locate
the appropriate template named as `.template`, it first looks in the
directory of the source file, and then works its way up the directory tree
until it hits the root directory, using the first template it finds.
This means that if you want different templates for some sections of your
site you can do that, and other sections will fallback to your root
template.
_If you don't specify a template you will get the `pandoc` defaults_

### BaseUre replacement
Currently the string `%BASEURL%` will be replaced in `.md` `.html` and
template files. This feature is something you will definitely want to use,
unless you want to structure everything to the point you only need relative
links. (I personally found this almost impossible)

## Future plans
- Automatic page generation using pre-process steps and temporary files.
E.G Contents pages, index pages etc...(DIFFICULT)
- Detection of changes to the `BaseUrl` and any templates. (MEDIUM)
- Detection of deleted source files and removal of the destination
files.(MEDIUM)
- Allow expansion of conversion operations (DIFFICULT)
- Allow customisation of filetypes that can have `%BASEURL%` replaced
(EASY-MEDIUM)
