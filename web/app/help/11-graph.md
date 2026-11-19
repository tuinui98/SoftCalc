# Graph

⋮ > Graph draws functions with one parameter, such as `f(x)=x^2-2`, and
equations in `x` and `y`, such as `x^2+y^2=9`. Done, or the phone's
Back, goes back to the calculator.

SoftCalc starts in degrees, so on a first run the graph shows
`y=sin(x)` and `y=x/360` from -360 to 360: a full sine wave each way.

## Adding lines

Above the list of lines:

- **+ Function** takes a function of one parameter, such as
  `g(x)=x^3-x`. A name already in use is refused; a function you define
  in Input, such as `f(x)=x^2`, shows in the list too.
- **+ Equation** takes an equation in `x` and `y`, such as `y^2=x^3-x`.
  Other names in it are variables, read when the line is drawn, so
  `x^2+y^2=r^2` follows `r`.
- **Examples** shows classic curves, each with a small picture:
  parabola, sine, bell curve, circle, heart, lemniscate and more. Tap
  one: it opens with its constants written in as numbers, such as
  `(x-0)^2+(y-0)^2=2^2`, and its form and what each constant changes
  under it. Change the numbers if you like, then OK adds the line.

The pictures are drawn in radians; in Degrees, sin, cos and tan draw
differently in the graph, and Examples says so. Adding a line does not
add a History line.

## The list

- Tap the square to draw or hide a line; tap the line to select it. The
  selected line is drawn thicker.
- **Press and hold** a line for:
  - **Insert**: a function as `f(` at Input's caret, an equation as it is;
  - **Edit**: change it; a function given a new name drops the old one;
  - **Colour**: one of twelve colours, or the default;
  - **Delete**, asked once more.

Lines without a colour of their own take one by their place in the
list, so adding a line can move the colours of those below it.

## Moving around

| do | how |
|---|---|
| read a line's value | drag one finger along it |
| move | two fingers, or one with **Pan** on |
| zoom | spread or pinch two fingers |
| zoom in on a point | double tap |
| fit y to the lines | **Fit** |
| same scale on x and y | **1:1** |
| back to the start range | **Reset** |

- Two fingers side by side zoom x only; one above the other zoom y only;
  at a slant both.
- **1:1** makes one unit as long across as up, so circles are round. A
  one-axis zoom, Fit or Reset turns it off.
- **Pan** lets one finger move the graph instead of reading values,
  handy with one hand.
- **Polar** draws `r = f(theta)`; tap it again for x and y. **Grid**
  shows or hides the grid.
- With a mouse: hover reads values, drag moves, the wheel zooms.

The graph keeps its range, lines and colours for next time, and Export
writes the equations to the `.pcf` (see *Getting started*).

## Reading values

A dot follows the line nearest the finger, with its value above it; the
status line shows it at full precision, such as `g(1.43) = 2.0449`.
After the finger lifts the value stays: **Copy** copies it, **Insert**
puts it in Input. On an equation SoftCalc keeps x where the line is
flat and y where it is steep, and solves for the other.
