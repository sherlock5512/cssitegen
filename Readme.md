# CsSiteGen
A relatively simple (for now) static site "generator".
The heavy lifting of this project is actually performed by `Pandoc` so you
better make sure you install it on your system.

## How to use
This project is nowhere near a finished thing so I wouldn't recommend
relying it on it at the moment.

But the general method of operation (for the moment) is to run it like
this:
```sh
cssitegen convert [INPUT_DIRECTORY] [OUTPUT_DIRECTORY]
```

At the moment the basic Markdown to HTML conversion is implemented, but
future plans include automatic conversion of images to `webp` and other
optimisations.

## Future plans
Automatic page generation using pre-process steps and temporary files.
E.G Contents pages, index pages etc...
