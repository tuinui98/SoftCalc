# Phase 2 test plan - behavior capture

Goal: record how the original responds to inputs, as golden outputs for
the clone. Black-box only: type into the Input box, read the History.

## Mechanism
- Input: set the Input edit text (`WM_SETTEXT`), then send Enter to it.
- Output: read the History rich-edit text (`WM_GETTEXT`) after each case;
  the new tail is the result. No file dialogs needed for this.
- Menu insertions: post a Functions/Conversions menu command with an empty
  Input, read back the Input text to learn the exact syntax it inserts.
- Keypad: `BM_CLICK` on keypad buttons in Numeric view.
- Each case records: mode settings, input, raw history tail, time taken.
- Output file: `spec/golden/<group>.tsv` (input, settings, output).

## Safety and restore
- Before: registry snapshot (already in `spec/raw/`); clear History.
- Creates temporary variables/functions with a `zz` prefix only; deletes
  them at the end through the program's own commands.
- Restores precision, angle mode, base, RPN, view at the end; then diffs
  the registry against the snapshot and reports any difference.
- Precision 512 cases run last with a per-case timeout (menu warns
  "very slow").

## Case groups

### A. Arithmetic and precedence
`1+2*3`, `(1+2)*3`, `2^3^2`, `-2^2`, `(-2)^2`, `2^-1`, `10/4`, `7/0`,
`0/0`, `1-1-1`, `2*-3`, `--2`, `+5`, `((1))`, `3!`, `7 mod 2`, `7%2`

### B. Number literals
`1e3`, `1.5e-3`, `1E+2`, `.5`, `5.`, `0x1F`, `0xff`, `101b`, `0b101`,
`1,000`, `1 000`, `00012`, `1.2.3`

### C. Output base (Hex / Dec / Bin output)
`255`, `-1`, `3.5`, `2^40`, `0.1`, `1/3` under each output base

### D. Complex numbers
`sqrt(-1)`, `i`, `j`, `2i`, `3+4i`, `(1+2i)*(3-4i)`, `abs(3+4i)`,
`ln(-1)`, `exp(i*pi)`, `(-8)^(1/3)`

### E. Trig under Degrees / Radians / Grads
`sin(pi/2)`, `sin(90)`, `sin(100)`, `cos(0)`, `tan(pi/2)`, `asin(2)`,
`acos(-1)`, `atan(1)`, `sinh(1)`, `atanh(1)`, `acosh(0)`

### F. Logs and exp
`ln(0)`, `ln(e)`, `log10(1000)`, `log10(-1)`, `exp(1)`, `log(100)`,
`log2(8)`, `exp(1000)`

### G. Constants and history references
`pi`, `e`, `ans`, `ans*2`, `$1`, `$2+1`, `$0`, `$999`

### H. Variables
`zza=5`, `zza*2`, `zza=zza+1`, `zza`, `ZZA`, `2zza`, `zza zza`,
`pi=3`, `e=1`, `zzb=zzundefined`, `zzlong_name_1=1`, `zzb=` 

### I. User functions
`zzf(x)=x^2`, `zzf(3)`, `zzf(zzf(2))`, `zzg(x,y)=x+y`, `zzg(1,2)`,
`zzg(1)`, `zzh(x)=zzh(x-1)`, `zzh(1)`, `zzk(t)=t*zza`, `zzk(2)`,
`zzf(x)=x+1` (redefine), `zzf(1)`

### J. Conversions
- For every Conversions menu entry (IDs 5022-5121): insert into empty
  Input, read back the inserted text. Gives the full `->` syntax table.
- Then evaluate a sample per category: `1 in->cm` style forms, plus
  `0 c->f`, `-40 c->f`, `0 k->c`, `100 km/h->mi/h`, `1 d->s`
- Mixed: `(1+1) ft->in`, conversion of a variable, conversion inside a
  larger expression

### K. Functions menu insertions
For every Functions menu entry (IDs 5000-5021, 6000+): insert into empty
Input, read back the inserted text.

### L. Precision levels (32 / 64 / 128 / 512)
`pi`, `1/3`, `2/3`, `sqrt(2)`, `2^200`, `10^100`, `1e-40`, `100!`,
`0.1+0.2`, `1e20+1-1e20`

### M. RPN mode
`64 53 +`, `2 3 ^`, `1 2 3 + *`, `5 sqrt`, `2 3`, `+`, `1 0 /`, then read
the Samples pane text in RPN mode

### N. Errors and edge inputs
empty input, spaces only, `1+`, `(`, `)`, `zzunknown(1)`, `sin()`,
`sin(1,2)`, `1/`, very long input (500 chars), unicode letters

### O. Numeric view keypad
Click sequences: `7 + 8 Ent`, `9 / 0 Ent`, `Clr`, `5 M+`, `MR`, `3 M-`,
`MR`, `MC`, `MR`

### P. Files (separate pass, needs file dialogs)
Save History to a text file; Save Functions & Variables to `.pcf`;
Load it back. Record formats. Written under `spec/raw/` only.

## Out of scope for Phase 2
Graph rendering accuracy (compare visually later), Help, clipboard.
