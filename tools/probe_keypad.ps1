#==================================================================
#  probe_keypad.ps1 - Phase 2 group O: Numeric view keypad and memory
#==================================================================
# Switches to Numeric Calc view, clicks keypad buttons in scripted
# sequences (BM_CLICK on the button controls), and records the Input
# text and History after every click. Needs elevation: run via
# run_elevated.ps1.

. "$PSScriptRoot\win32_probe.ps1"

$BM_CLICK = 0x00F5
$WM_SETTEXT = 0x000C
$CMD_NUMERIC_VIEW = 32779
$CMD_ADVANCED_VIEW = 32781
$CMD_CLEAR_HISTORY = 32800

$sequences = @(
	"7 + 8 Ent",
	"9 / 0 Ent",
	"1 2 Clr",
	"5 M+ MR",
	"MC 5 M+ 3 M+ MR",
	"MC 5 M+ 3 M- MR",
	"MC MR",
	"2 Ent * 3 Ent",
	"Ent"
)

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class KeyApi
{
	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern IntPtr SendMessageW(IntPtr hwnd, uint msg, IntPtr w, string l);
}
"@

function Find-Child([IntPtr]$hwnd, [string]$class_name)
{
	foreach ($c in [Win]::Children($hwnd))
	{
		if ([Win]::ClassOf($c) -eq $class_name)
		{
			return $c
		}
	}
	throw "child '$class_name' not found"
}

#==================================================================
#  BLOCK 1 - Prepare : Numeric view, map button labels to handles
#==================================================================
$proc = Get-PowerCalc
$main = Get-MainWindow $proc
Send-MenuCommand $main $CMD_NUMERIC_VIEW
Start-Sleep -Milliseconds 600
$input_view = Find-Child $main "INPUTVIEWCLASS"
$edit = Find-Child $input_view "Edit"
$history = Find-Child $input_view "RICHEDIT"
$keypad = Find-Child $main "KeyPadCLASS"
$buttons = @{}
foreach ($b in [Win]::Children($keypad))
{
	$buttons[[Win]::Text($b)] = $b
}

#==================================================================
#  BLOCK 2 - Run : click each sequence, record state after each key
#==================================================================
$root = Split-Path -Parent $PSScriptRoot
$rows = New-Object System.Collections.Generic.List[string]
$rows.Add("sequence`tkey`tinput_after`thistory_after")
foreach ($seq in $sequences)
{
	Send-MenuCommand $main $CMD_CLEAR_HISTORY
	[void][KeyApi]::SendMessageW($edit, $WM_SETTEXT, [IntPtr]::Zero, "")
	Start-Sleep -Milliseconds 200
	foreach ($key in ($seq -split " "))
	{
		if (-not $buttons.ContainsKey($key))
		{
			$rows.Add("$seq`t$key`t<no such button>`t")
			continue
		}
		[void][Win]::SendMessageW($buttons[$key], $BM_CLICK, [IntPtr]::Zero, [IntPtr]::Zero)
		Start-Sleep -Milliseconds 350
		$hist = ([Win]::Text($history).Trim() -replace "`r`n|`r|`n", " \n ")
		$rows.Add("$seq`t$key`t$([Win]::Text($edit))`t$hist")
	}
}

#==================================================================
#  BLOCK 3 - Finish : back to Advanced view, write golden
#==================================================================
[void][KeyApi]::SendMessageW($edit, $WM_SETTEXT, [IntPtr]::Zero, "")
Send-MenuCommand $main $CMD_ADVANCED_VIEW
$rows | Out-File -FilePath (Join-Path $root "spec\golden\O_keypad.tsv") -Encoding utf8
$rows
