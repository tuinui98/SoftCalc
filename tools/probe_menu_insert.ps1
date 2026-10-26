#==================================================================
#  probe_menu_insert.ps1 - Phase 2 groups J/K: menu insertion syntax
#==================================================================
# For every Functions and Conversions menu command, posts the command
# and reads back what it writes into the Input box. Two passes: Input
# empty, and Input holding "1" with the caret at the end. Never presses
# Enter, so nothing is evaluated. Needs elevation: run via
# run_elevated.ps1.

. "$PSScriptRoot\win32_probe.ps1"

$WM_SETTEXT = 0x000C
$EM_SETSEL = 0x00B1
$CMD_CLEAR_HISTORY = 32800
$CMD_ADVANCED_VIEW = 32781

$root = Split-Path -Parent $PSScriptRoot
$menu_file = Join-Path $root "spec\inventory\menu_tree.txt"
$out_file = Join-Path $root "spec\golden\menu_insert.tsv"

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class TextApi
{
	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern IntPtr SendMessageW(IntPtr hwnd, uint msg, IntPtr w, string l);
}
"@

function Find-Path([IntPtr]$hwnd, [string[]]$classes)
{
	# Follows a class-name path down the child tree, first match at each level
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

function Get-MenuCommands([string]$path)
{
	# Rebuilds "Top > Sub > Item" paths from the indented menu_tree.txt
	$stack = @{}
	$result = New-Object System.Collections.Generic.List[object]
	foreach ($line in Get-Content $path)
	{
		if ($line -match "^#" -or $line.Trim() -eq "" -or $line.Trim() -eq "----")
		{
			continue
		}
		$depth = ($line.Length - $line.TrimStart().Length) / 4
		$body = $line.Trim()
		$id = $null
		if ($body -match "^(.*?)\s+\[(.*)\]$")
		{
			$body = $Matches[1]
			if ($Matches[2] -match "id=(\d+)")
			{
				$id = [int]$Matches[1]
			}
		}
		$stack[$depth] = $body -replace "&", ""
		if ($id -ne $null -and ($stack[0] -eq "Functions" -or $stack[0] -eq "Conversions"))
		{
			$parts = @()
			for ($d = 0; $d -le $depth; $d++)
			{
				$parts += $stack[$d]
			}
			$result.Add([pscustomobject]@{ Id = $id; Path = ($parts -join " > ") })
		}
	}
	return $result
}

function Read-Inserted([IntPtr]$main, [IntPtr]$edit, [string]$seed, [int]$cmd)
{
	[void][TextApi]::SendMessageW($edit, $WM_SETTEXT, [IntPtr]::Zero, $seed)
	[void][Win]::SendMessageW($edit, $EM_SETSEL, [IntPtr]$seed.Length, [IntPtr]$seed.Length)
	Send-MenuCommand $main $cmd
	Start-Sleep -Milliseconds 150
	return [Win]::Text($edit)
}

#==================================================================
#  BLOCK 1 - Prepare : Advanced view, empty history, find Input box
#==================================================================
$proc = Get-PowerCalc
$main = (Get-MainWindow $proc)
Send-MenuCommand $main $CMD_ADVANCED_VIEW
Send-MenuCommand $main $CMD_CLEAR_HISTORY
Start-Sleep -Milliseconds 500
$edit = Find-Path $main @("INPUTVIEWCLASS", "Edit")
$history = Find-Path $main @("INPUTVIEWCLASS", "RICHEDIT")
$history_before = [Win]::Text($history)

#==================================================================
#  BLOCK 2 - Probe : each command, two seeds
#==================================================================
$commands = Get-MenuCommands $menu_file
$rows = New-Object System.Collections.Generic.List[string]
$rows.Add("id`tmenu_path`tinserted_into_empty`tinserted_after_1")
foreach ($c in $commands)
{
	$empty = Read-Inserted $main $edit "" $c.Id
	$after_one = Read-Inserted $main $edit "1" $c.Id
	$rows.Add("$($c.Id)`t$($c.Path)`t$empty`t$after_one")
}

#==================================================================
#  BLOCK 3 - Finish : clear Input, confirm nothing was evaluated
#==================================================================
[void][TextApi]::SendMessageW($edit, $WM_SETTEXT, [IntPtr]::Zero, "")
$history_after = [Win]::Text($history)
$dir = Split-Path -Parent $out_file
if (-not (Test-Path $dir))
{
	New-Item -ItemType Directory -Force $dir | Out-Null
}
$rows | Out-File -FilePath $out_file -Encoding utf8
"commands probed: $($commands.Count)"
"history unchanged: $($history_before -eq $history_after)"
$rows | Select-Object -First 40
