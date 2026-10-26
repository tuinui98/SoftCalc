# Persisted settings (observed)

Observed 2026-10-27 by reading the registry the running program writes to.
Root: `HKCU\Software\Microsoft\Windows\CurrentVersion\Applets\Calculator`.
No HKLM or VirtualStore copy exists.

## `Functions` (REG_SZ, one value per function)
Value name is the list index (`0`, `1`, ...); data is the full definition text.

| index | data (factory default) |
|---|---|
| 0 | `g(x) = x` |
| 1 | `function(var) = var*cos(var)` |

Implication: function names and parameter names are free identifiers, not
fixed to `f`/`x`. These entries populate *Functions > User Functions*.

## `Variables`
Empty by default. Expected to hold user-defined variables the same way.
To confirm by adding one in Phase 2.

## `State` (REG_DWORD unless noted)

| name | observed | meaning (inferred from menu state) |
|---|---|---|
| Precision | 32 | digits: 32 / 64 / 128 / 512 |
| Base | 10 | output base: 16 / 10 / 2 |
| angMode | 1 | angle unit; menu shows Radians selected (Deg/Rad/Grad order) |
| fRPN | 0 | RPN mode off |
| iViewMode | 3 | Advanced View selected (Numeric / History / Advanced) |
| fShowHelp | 1 | *Show Samples* checked |
| xMainWnd, yMainWnd, cxMainWnd, cyMainWnd | 517, 104, 1088, 758 | window rect |
| fMainWndMaximized | 0 | |
| iGraphMode | 32814 | graph mode; looks like a command ID, XY vs polar |
| fShowGrid | 1 | graph grid on |

Mappings marked inferred need a toggle test to confirm (e.g. `angMode` values
for Degrees and Grads).

## `State\2DXY` (REG_SZ)
`minX=-10 maxX=10 gridX=1 minY=-10 maxY=10 gridY=1`

## `State\2DPolar` (REG_SZ)
`minT=-3.1415926535897931 maxT=3.1415926535897932384626433832795 gridT=1 minR=0 maxR=10 gridR=1`

Note: ranges are stored as strings, and `minT`/`maxT` differ in printed
precision, which suggests they are kept as entered text, not as doubles.
