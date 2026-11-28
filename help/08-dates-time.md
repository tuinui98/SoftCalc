# Dates and time

## Dates

Write a date as `dd.mm.yyyy`, with an optional time after a space.

```calc
25.12.2026 - 1.11.2026        => 54
1.11.2026 + 30                => 01.12.2026
today + 30
now [longdatetime]
```

- `date - date` is a number of days; `date + n` and `date - n` are
  dates. A fraction of a day adds a time.
- `today` is the current date, `now` the current date and time.
- Years are Gregorian (2026, not 2569).

## Durations

Write a duration as `h:mm` or `h:mm:ss`; hours may go past 24.

```calc
1:30 + 0:45                   => 2:15:00
8:00 * 5                      => 40:00:00
36:00:00 / 1:30               => 24
```

- Durations add to durations and to dates; they multiply and divide by
  numbers. A duration divided by a duration is a number.
- `1:30 + 2` is an error: is 2 hours or days? Write `1:30 + 2:00`.
- To get hours as a number, divide: `(17:45 - 9:15) / 1:00` is 8.5.

## Formats

Tags without an arrow format a date. Conversions > Date Format inserts
them.

```calc
1.11.2026 [longdate]          => Sunday, 1 November 2026
fromepoch(0) [utc->local]
```

| tag | gives |
|---|---|
| `[longdate]` | Sunday, 1 November 2026 |
| `[longtime]` | 14:05:09 |
| `[longdatetime]` | both |
| `[epoch]` | Unix seconds |
| `[fromepoch]` | a date and time from Unix seconds |

The same names work as functions: `longdate(today)`, `epoch(now)`.

## Time zones

`[utc->local]` and `[local->utc]` shift a date-time by this computer's
current time zone offset.

## Stopwatch, timer and alarm

Typed in Input in any view, or from the Timers window. The running ones
show faint at the right of the hint line under Input (`⏱ 0:01:23.4`,
`⌛ 4:12`, `⏰ 0:02`, `+2` for more); click or tap them to open the window.

```calc
:sw
:lap
:sw pause
:sw
:timer 5m "tea"
:alarm 14:30 "call"
:timer *
:timer 90                     => ERROR:A timer needs a length, not a number; write :timer 5m, 90s, 1h30m, 0:05:00 or a sum giving one (0:05*3), a name in quotes after it
```

| line | does |
|---|---|
| `:sw` or `:stopwatch` | starts the stopwatch, or stops it |
| `:sw pause`, `:sw go` | pauses it, goes on |
| `:lap` or `:sw lap` | a lap; the stopwatch runs on (not while paused) |
| `:sw reset` | clears it without a record |
| `:sw last` | the last run with its laps and pauses, also after a restart |
| `:timer 5m` | a timer: `90s`, `1h30m`, `1.5m`, `0:05:00`, or a sum such as `0:05*3` or `now+0:15` |
| `:alarm 14:30` | an alarm at the next 14:30; `30.11.2026 7:00`, `today+1+7:00`, `now+0:15` |
| `"name"` after either | names it; a name in use sets that one's time again |
| `:timer`, `:alarm` | what is left of each |
| `:timer off`, `:alarm off "call"` | cancels all, or the one named |
| `*` after any of them | lists what is going: the stopwatch, every timer and alarm |

- Stop and Lap go to History as a value, `0:01:23.45`, with the label
  faint on the right (`Lap 2, total 0:01:23.45`); it is `answer`, so
  `answer/12` works. A stopped run then lists its laps and pauses.
- `5:00` is five hours as a duration, so write `5m` or `0:05`.
  `:alarm 0:15` is a quarter past midnight; `now+0:15` is fifteen
  minutes on, the same as `:timer 15m`. A plain number is refused:
  `now+90` would be ninety days.
- Typing a name in quotes shows the names in use in the hint line.
- When one is done, History says so and the sound plays: once, again
  every 2 seconds for a minute, or not at all (the window's choice);
  any key quiets it. Ones done while SoftCalc was closed show on the
  next start, without a sound.
<!-- desktop -->
- The taskbar button flashes when SoftCalc is not in front. **F8**
  starts or stops the stopwatch, **Shift+F8** takes a lap, **Ctrl+F8**
  pauses or goes on, **Ctrl+Shift+F8** resets it, in the main window and
  the Timers window.
<!-- /desktop -->
<!-- web -->
- A phone also buzzes. On a keyboard, **F8** starts or stops the
  stopwatch, **Shift+F8** takes a lap, **Ctrl+F8** pauses or goes on,
  **Ctrl+Shift+F8** resets it. While the app is closed or in the
  background, nothing sounds; the line shows when it comes back.
<!-- /web -->

### The Timers window

<!-- desktop -->
Tools > Stopwatch & Timers, or a click on the readout. Three tabs:
<!-- /desktop -->
<!-- web -->
⋮ > Timers, or a tap on the readout. Three tabs:
<!-- /web -->

- **Stopwatch**: the time large; Start / Stop, Pause / Go on, Lap,
  Reset; the laps and pauses of this run, or of the last one.
- **Timer** and **Alarm**: a field for the length or time and one for a
  name, Start, and the list with time left; Cancel (✕) takes one off.
- **Sound when done**: once, repeat or off; **Quiet** stops a sound.

### A countdown page

<!-- desktop -->
Double-click a timer or an alarm (or select it and press Enter):
<!-- /desktop -->
<!-- web -->
Tap a timer or an alarm:
<!-- /web -->

- Its name, and this computer's date and time.
- Three numbers, the first large. Click one to choose what it shows,
  **Time left** (counts down, then `T+` once done), **Since start**,
  **% done**, **% left**, **Set length**, **Started**, **Ends** or
  **Clock**, and its format: `ddd hh:mm:ss.s`, `hhh:mm:ss.s`,
  `mmm:ss.s` or `ssss.s` for a length, `HH:mm:ss.s`, `HH:mm:ss` or
  `dd.MM.yyyy HH:mm:ss.s` for a time of day. The choice is kept, once
  for timers and once for alarms.
- **Set** (the length; for an alarm, the wait from when it was set),
  **Started** and **Ends**.
- **Change time…** takes a new length or time as the line would; a
  timer counts it from now or from its start.
- In the last 10 seconds the main number turns amber and ticks each
  second (**Quiet countdown** turns the ticks off); when done it counts
  on as `T+` in red until **Dismiss**.
