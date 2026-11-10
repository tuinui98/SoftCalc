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
