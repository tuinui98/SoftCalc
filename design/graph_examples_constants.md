# Graph Examples with constants (proposal 2026-11-10)

Owner decisions: Use... puts the form with the defaults written in as
numbers (`(x-a)^2+(y-b)^2=r^2` becomes `(x-0)^2+(y-0)^2=2^2`); circle
uses `(x-a)`. Thumbnails are drawn with the defaults. Constant names
avoid the engine constants e, i, j, pi. Notes checked by hand; each form
is drawn through the engine before it goes in. After Use a grey label
shows the form and the note below the input: in the Functions editor in
the empty space under the lowest input, in Add Equation a window 3-4
lines taller.

## Functions

| title | form | note | defaults |
|---|---|---|---|
| Parabola | `a*(x-h)^2+k` | vertex at (h, k); a = width, a<0 opens down | a=1 h=0 k=0 |
| Cubic | `x^3-b*x+k` | b>0: two bumps at x=±√(b/3); b≤0: none; k = y offset | b=3 k=0 |
| Absolute value | `a*abs(x-h)+k` | corner at (h, k); a = slope of the arms | a=1 h=0 k=0 |
| Square root | `a*sqrt(x-h)+k` | starts at (h, k); a = stretch | a=1 h=0 k=0 |
| Hyperbola 1/x | `a/(x-h)+k` | asymptotes x=h, y=k; a<0 flips quadrants | a=1 h=0 k=0 |
| Sine | `a*sin(b*(x-c))+d` | a = amplitude, period 2π/b, c = phase shift, d = mid line | a=1 b=1 c=0 d=0 |
| Cosine | `a*cos(b*(x-c))+d` | as Sine | a=1 b=1 c=0 d=0 |
| Tangent | `a*tan(b*(x-c))` | period π/b, c = shift, a = stretch | a=1 b=1 c=0 |
| Sinc | `sin(b*x)/(b*x)` | zeros at multiples of π/b | b=1 |
| Exponential | `a*exp(b*x)+k` | y=a+k at x=0; b<0 decays; asymptote y=k | a=1 b=1 k=0 |
| Natural log | `a*ln(x-h)+k` | asymptote x=h; passes (h+1, k) | a=1 h=0 k=0 |
| Bell curve | `a*exp(-(x-m)^2/(2*s^2))` | a = peak, m = centre, s = width (bends at m±s) | a=1 m=0 s=0.7071 |
| Sigmoid | `L/(1+exp(-k*(x-m)))` | L = top level, k = steepness, m = midpoint (y=L/2) | L=1 k=1 m=0 |
| Witch of Agnesi | `8*a^3/(x^2+4*a^2)` | a = radius of the circle that draws it; peak 2a | a=1 |
| x sin(1/x) | `x^n*sin(1/x)` | n=0: no limit at 0; n=1: squeezed to 0; n=2: smooth at 0 | n=1 |
| Two waves | `sin(x)+a*sin(b*x)` | a = second wave height, b = its frequency | a=1/3 b=3 |

## Equations

| title | form | note | defaults |
|---|---|---|---|
| Circle | `(x-a)^2+(y-b)^2=r^2` | centre (a, b), r = radius | a=0 b=0 r=2 |
| Ellipse | `(x-h)^2/a^2+(y-k)^2/b^2=1` | centre (h, k), a = half width, b = half height | a=3 b=2 h=0 k=0 |
| Hyperbola | `x^2/a^2-y^2/b^2=1` | vertices (±a, 0), asymptotes y=±(b/a)x | a=1 b=1 |
| Sideways parabola | `(y-k)^2=4*p*(x-h)` | vertex (h, k), focus p to the right, p<0 opens left | p=0.25 h=0 k=0 |
| Heart | `(x^2+y^2-1)^3=a*x^2*y^3` | a = height of the lobes; a=0 is a circle | a=1 |
| Lemniscate | `(x^2+y^2)^2=2*a^2*(x^2-y^2)` | foci (±a, 0), tips at ±a√2 | a=1 |
| Folium of Descartes | `x^3+y^3=3*a*x*y` | loop tip at (1.5a, 1.5a), asymptote x+y=-a | a=1 |
| Cardioid | `(x^2+y^2-a*x)^2=a^2*(x^2+y^2)` | cusp at 0, reaches x=2a | a=1 |
| Astroid | `abs(x)^(2/3)+abs(y)^(2/3)=a^(2/3)` | cusps at ±a on both axes | a=1 |
| Superellipse | `abs(x/a)^n+abs(y/b)^n=1` | n=2 ellipse, n>2 squarer, n=1 diamond, n<1 star | a=1 b=1 n=4 |
| Cassini oval | `(x^2+y^2)^2-2*a^2*(x^2-y^2)=b^4-a^4` | foci (±a, 0); b<a two ovals, b=a lemniscate, b>a one | a=1 b=1.107 |
| Elliptic curve | `y^2=x^3+a*x+b` | 4a³+27b² < 0: two pieces, > 0: one, = 0: pinched | a=-1 b=1 |
| Cissoid of Diocles | `y^2*(2*a-x)=x^3` | asymptote x=2a | a=1 |
| Devil's curve | `y^2*(y^2-a^2)=x^2*(x^2-b^2)` | crosses y axis at ±a, x axis at ±b; a=b: lines y=±x plus a circle | a=2 b=2.236 |
| Egg crate | `sin(b*x)*sin(b*y)=c` | c=0: grid of lines, 0<c<1: eggs, \|c\|>1: nothing; cell size π/b | b=1 c=0.3 |
| x^y = y^x | `x^y=y^x` | no constant: line y=x plus a curve crossing it at (e, e); passes (2, 4) | - |
