# SoftCalc Help - source

One Markdown file per topic, `NN-name.md`, in reading order. The text is
SoftCalc's own; nothing comes from the original program's help.

## Examples

Examples sit in fenced blocks marked `calc` (or `rpn` for RPN mode):

    ```calc
    2^3^2                 => 512
    now [longdatetime]
    ```

- `line => result`: `engine/tests/test_help_examples.py` checks that the
  engine gives exactly this result. Each file runs in a fresh engine at
  default settings, top to bottom.
- A line without `=>` is shown but not checked (dates, currency).
- In the Help window a click on an example puts the line in Input.

## Pictures

Write `![caption](img/name.light.png)`, so the picture shows in any
Markdown viewer; in dark theme Help shows `img/name.dark.png` instead. They are
made from SoftCalc's own windows, never by hand:

    dotnet build tools/HelpShots
    tools/HelpShots/bin/Debug/net10.0-windows/HelpShots.exe help/img

The tool needs `engine/.venv`, shows SoftCalc windows for about 40 s and
uses a scratch data folder. Rerun it after a UI change.

`HelpShots.exe <folder> light help` (or `dark`) only checks the Help
window: it saves `check-help-*.png` to the folder (not for `help/img`)
and prints Input after a simulated example click.

## Desktop and web app

The web app (web/README.md) shows topics 01 to 09, 16 and 18 too. Where
the two differ, mark whole paragraphs or lists, each marker on a line of
its own:

    <!-- desktop -->
    View > Word Size is used.
    <!-- /desktop -->

    <!-- web -->
    Settings > Word size is used.
    <!-- /web -->

The desktop Help leaves out `web` blocks, the web app `desktop` blocks
and all pictures. Leave a blank line after a block that ends a list.
`web/app/help/` holds the web's own version of a topic (Getting
started), checked by the same example test.

## Links out

A `More:` line may link to a reference page, `[DLMF §4.2](https://dlmf.nist.gov/4.2)`,
on these sites only (owner 2026-11-26): dlmf.nist.gov, www.itl.nist.gov
(the NIST/SEMATECH e-Handbook), mathworld.wolfram.com, oeis.org and
mpmath.org. They open in the browser; any other link shows as plain
text (`HelpMarkdown.WebHosts`, `make_help.mjs` WEB_HOSTS, both tested).
Link only: nothing is copied from those pages.
