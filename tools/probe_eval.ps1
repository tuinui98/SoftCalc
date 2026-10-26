#==================================================================
#  probe_eval.ps1 - Phase 2 evaluation runner
#==================================================================
# Reads a case file from spec/cases/, types each case into the Input
# box, presses Enter, and records the text the History gains. Case
# file syntax:
#   # comment
#   @set prec=32|64|128|512 angle=deg|rad|grad base=hex|dec|bin rpn=on|off
#   <input line>                (blank line = submit empty input)
# Settings are restored to their starting values at the end. Needs
# elevation: run via run_elevated.ps1 -Probe "probe_eval.ps1 <group>".

param
(
	[Parameter(Mandatory = $true)][string]$Group,
	[int]$TimeoutMs = 15000
)

. "$PSScriptRoot\win32_probe.ps1"

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class EvalApi
{
	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern IntPtr SendMessageW(IntPtr hwnd, uint msg, IntPtr w, string l);
	[DllImport("user32.dll")]
	public static extern uint GetMenuState(IntPtr menu, uint id, uint flags);
}
"@

$WM_SETTEXT = 0x000C
$WM_KEYDOWN = 0x0100
$WM_KEYUP = 0x0101
$WM_CHAR = 0x0102
$EM_SETSEL = 0x00B1
$VK_RETURN = 0x0D
$CMD_CLEAR_HISTORY = 32800
$CMD_ADVANCED_VIEW = 32781
$CMD_RPN = 32805
$setting_cmds = @{
	prec = @{ "32" = 32810; "64" = 32811; "128" = 32812; "512" = 32813 }
	angle = @{ deg = 32774; rad = 32775; grad = 32776 }
	base = @{ hex = 32806; dec = 32807; bin = 32808 }
}

$root = Split-Path -Parent $PSScriptRoot
$case_file = Join-Path $root "spec\cases\$Group.txt"
$out_file = Join-Path $root "spec\golden\$Group.tsv"

function Find-Path([IntPtr]$hwnd, [string[]]$classes)
{
	$cur = $hwnd
	foreach ($cls in $classes)
	{
		$next = [IntPtr]::Zero
		foreach ($c in [Win]::Children($cur))
		{
			if ([Win]::ClassOf($c) -eq $cls)
			{
				$next = $c
				break
			}
		}
		if ($next -eq [IntPtr]::Zero)
		{
			throw "child '$cls' not found"
		}
		$cur = $next
	}
	return $cur
}

function Test-Checked([IntPtr]$main, [int]$cmd)
{
	$state = [EvalApi]::GetMenuState([Win]::GetMenu($main), $cmd, 0)    # MF_BYCOMMAND
	return (($state -band 0x08) -ne 0)    # MF_CHECKED
}

function Get-CurrentSettings([IntPtr]$main)
{
	$now = @{}
	foreach ($key in $setting_cmds.Keys)
	{
		foreach ($value in $setting_cmds[$key].Keys)
		{
			if (Test-Checked $main $setting_cmds[$key][$value])
			{
				$now[$key] = $value
			}
		}
	}
	$now["rpn"] = $(if (Test-Checked $main $CMD_RPN) { "on" } else { "off" })
	return $now
}

function Set-Settings([IntPtr]$main, [hashtable]$wanted)
{
	foreach ($key in $wanted.Keys)
	{
		if ($key -eq "rpn")
		{
			$is_on = Test-Checked $main $CMD_RPN
			if (($wanted[$key] -eq "on") -ne $is_on)
			{
				Send-MenuCommand $main $CMD_RPN
			}
		}
		else
		{
			Send-MenuCommand $main $setting_cmds[$key][$wanted[$key]]
		}
	}
	Start-Sleep -Milliseconds 300
}

function Wait-Stable([IntPtr]$history, [string]$before, [int]$timeout_ms, $proc)
{
	# Returns once the History text changed and stayed unchanged for 300 ms,
	# or as soon as the target process dies
	$start = Get-Date
	$last = $before
	$last_change = $null
	while (((Get-Date) - $start).TotalMilliseconds -lt $timeout_ms)
	{
		Start-Sleep -Milliseconds 50
		$proc.Refresh()
		if ($proc.HasExited)
		{
			break
		}
		$now = [Win]::Text($history)
		if ($now -ne $last)
		{
			$last = $now
			$last_change = Get-Date
		}
		elseif ($last_change -and ((Get-Date) - $last_change).TotalMilliseconds -ge 300)
		{
			break
		}
	}
	return @{ Text = $last; Ms = [int]((Get-Date) - $start).TotalMilliseconds; Changed = ($last -ne $before) }
}

#==================================================================
#  BLOCK 1 - Prepare : view, history, controls, starting settings
#==================================================================
if (-not (Test-Path $case_file))
{
	throw "case file not found: $case_file"
}
$proc = Get-PowerCalc
$main = (Get-MainWindow $proc)
Send-MenuCommand $main $CMD_ADVANCED_VIEW
Start-Sleep -Milliseconds 300
$edit = Find-Path $main @("INPUTVIEWCLASS", "Edit")
$history = Find-Path $main @("INPUTVIEWCLASS", "RICHEDIT")
$start_settings = Get-CurrentSettings $main
$current = $start_settings.Clone()

#==================================================================
#  BLOCK 2 - Run : one case per line, record the History delta
#==================================================================
$rows = New-Object System.Collections.Generic.List[string]
$rows.Add("input`tprec`tangle`tbase`trpn`tms`toutput")
foreach ($line in Get-Content $case_file -Encoding UTF8)
{
	if ($line.StartsWith("#"))
	{
		continue
	}
	if ($line.StartsWith("@set"))
	{
		$wanted = @{}
		foreach ($pair in ($line.Substring(4).Trim() -split "\s+"))
		{
			$kv = $pair -split "=", 2
			$wanted[$kv[0]] = $kv[1]
			$current[$kv[0]] = $kv[1]
		}
		Set-Settings $main $wanted
		continue
	}

	Send-MenuCommand $main $CMD_CLEAR_HISTORY
	Start-Sleep -Milliseconds 150
	$before = [Win]::Text($history)
	[void][EvalApi]::SendMessageW($edit, $WM_SETTEXT, [IntPtr]::Zero, $line)
	[void][Win]::SendMessageW($edit, $EM_SETSEL, [IntPtr]$line.Length, [IntPtr]$line.Length)
	[void][Win]::PostMessageW($edit, $WM_KEYDOWN, [IntPtr]$VK_RETURN, [IntPtr]0x001C0001)
	[void][Win]::PostMessageW($edit, $WM_CHAR, [IntPtr]$VK_RETURN, [IntPtr]0x001C0001)
	[void][Win]::PostMessageW($edit, $WM_KEYUP, [IntPtr]$VK_RETURN, [IntPtr]0xC01C0001)
	$res = Wait-Stable $history $before $TimeoutMs $proc

	$proc.Refresh()
	if ($proc.HasExited)
	{
		# The original crashes on some inputs; record it, relaunch, re-apply settings
		$rows.Add("$line`t$($current.prec)`t$($current.angle)`t$($current.base)`t$($current.rpn)`t$($res.Ms)`t<CRASH exit=0x$('{0:X8}' -f $proc.ExitCode)>")
		$proc = Start-PowerCalc
		$main = (Get-MainWindow $proc)
		Send-MenuCommand $main $CMD_ADVANCED_VIEW
		Start-Sleep -Milliseconds 300
		$edit = Find-Path $main @("INPUTVIEWCLASS", "Edit")
		$history = Find-Path $main @("INPUTVIEWCLASS", "RICHEDIT")
		Set-Settings $main $current
		continue
	}

	$delta = $res.Text
	if ($delta.StartsWith($before))
	{
		$delta = $delta.Substring($before.Length)
	}
	if (-not $res.Changed)
	{
		$delta = "<no history change; input now: '" + [Win]::Text($edit) + "'>"
	}
	$delta = ($delta.Trim() -replace "`r`n|`r|`n", " \n ")
	$rows.Add("$line`t$($current.prec)`t$($current.angle)`t$($current.base)`t$($current.rpn)`t$($res.Ms)`t$delta")
}

#==================================================================
#  BLOCK 3 - Finish : restore settings, clear input, write golden
#==================================================================
Set-Settings $main $start_settings
[void][EvalApi]::SendMessageW($edit, $WM_SETTEXT, [IntPtr]::Zero, "")
$dir = Split-Path -Parent $out_file
if (-not (Test-Path $dir))
{
	New-Item -ItemType Directory -Force $dir | Out-Null
}
$rows | Out-File -FilePath $out_file -Encoding utf8
$restored = Get-CurrentSettings $main
"cases: $($rows.Count - 1)"
"settings restored: " + (($start_settings.Keys | Where-Object { $start_settings[$_] -ne $restored[$_] }).Count -eq 0)
$rows
