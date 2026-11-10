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

`![caption](img/name.png)` refers to `img/name.light.png` and
`img/name.dark.png`; Help shows the one for the current theme. They are
made from SoftCalc's own windows, never by hand:

    dotnet build tools/HelpShots
    tools/HelpShots/bin/Debug/net10.0-windows/HelpShots.exe help/img

The tool needs `engine/.venv`, shows SoftCalc windows for about 40 s and
uses a scratch data folder. Rerun it after a UI change.
