#==================================================================
#  probe_files.ps1 - Phase 2 group P: saved file formats, Polar range
#==================================================================
# Creates a known History and a few zz-prefixed variables/functions,
# then drives the program's own Save History and Save/Load Functions &
# Variables commands through their file dialogs, writing only under
# spec/raw/. Also switches the Graph window to Polar to dump its range
# panel. Needs elevation: run via run_elevated.ps1.

. "$PSScriptRoot\win32_probe.ps1"

$WM_SETTEXT = 0x000C
$WM_KEYDOWN = 0x0100
$WM_CHAR = 0x0102
$WM_KEYUP = 0x0101
$VK_RETURN = 0x0D
$IDOK = 1
$CMD_SAVE_HISTORY = 32801
$CMD_SAVE_FUNCS = 32783
$CMD_LOAD_FUNCS = 32784
$CMD_CLEAR_HISTORY = 32800
$CMD_ADVANCED_VIEW = 32781
$CMD_GRAPH_WINDOW = 97
$CMD_POLAR = 32815
$CMD_XY = 32814

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class FileApi
{
	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern IntPtr SendMessageW(IntPtr hwnd, uint msg, IntPtr w, string l);
}
"@

$root = Split-Path -Parent $PSScriptRoot
$raw = Join-Path $root "spec\raw"
$report = New-Object System.Collections.Generic.List[string]

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

function Find-All([IntPtr]$hwnd, [string]$class_name, $acc)
{
	foreach ($c in [Win]::Children($hwnd))
	{
		if ([Win]::ClassOf($c) -eq $class_name)
		{
			$acc.Add($c)
		}
		Find-All $c $class_name $acc
	}
}

function Submit([IntPtr]$edit, [string]$text)
{
	[void][FileApi]::SendMessageW($edit, $WM_SETTEXT, [IntPtr]::Zero, $text)
	[void][Win]::PostMessageW($edit, $WM_KEYDOWN, [IntPtr]$VK_RETURN, [IntPtr]0x001C0001)
	[void][Win]::PostMessageW($edit, $WM_CHAR, [IntPtr]$VK_RETURN, [IntPtr]0x001C0001)
	[void][Win]::PostMessageW($edit, $WM_KEYUP, [IntPtr]$VK_RETURN, [IntPtr]0xC01C0001)
	Start-Sleep -Milliseconds 500
}

function Wait-Popup([uint32]$proc_id, $before)
{
	for ($i = 0; $i -lt 50; $i++)
	{
		Start-Sleep -Milliseconds 100
		$fresh = @([Win]::TopWindowsOf($proc_id) | Where-Object { $before -notcontains $_ })
		if ($fresh.Count -gt 0)
		{
			Start-Sleep -Milliseconds 600
			return $fresh[0]
		}
	}
	return [IntPtr]::Zero
}

function Invoke-FileDialog($proc, [IntPtr]$main, [int]$cmd, [string]$path)
{
	# Types a full path into the dialog's file name box, then presses OK
	$before = [Win]::TopWindowsOf([uint32]$proc.Id)
	Send-MenuCommand $main $cmd
	$dlg = Wait-Popup ([uint32]$proc.Id) $before
	if ($dlg -eq [IntPtr]::Zero)
	{
		return "no dialog appeared"
	}
	$edits = New-Object System.Collections.Generic.List[IntPtr]
	Find-All $dlg "Edit" $edits
	$name_box = $edits | Where-Object { [Win]::GetDlgCtrlID($_) -in 1148, 1152 } | Select-Object -First 1
	if (-not $name_box)
	{
		$name_box = $edits | Select-Object -First 1
	}
	if (-not $name_box)
	{
		[void][Win]::PostMessageW($dlg, $WM_COMMAND, [IntPtr]2, [IntPtr]::Zero)
		return "dialog '$([Win]::Text($dlg))' has no edit box; cancelled"
	}
	[void][FileApi]::SendMessageW($name_box, $WM_SETTEXT, [IntPtr]::Zero, $path)
	[void][Win]::PostMessageW($dlg, $WM_COMMAND, [IntPtr]$IDOK, [IntPtr]::Zero)
	Start-Sleep -Milliseconds 1000
	$open = [Win]::IsWindowVisible($dlg)
	if ($open)
	{
		[void][Win]::PostMessageW($dlg, $WM_COMMAND, [IntPtr]2, [IntPtr]::Zero)
	}
	return "dialog '$([Win]::Text($dlg))' class=$([Win]::ClassOf($dlg)) still_open_after_ok=$open"
}

#==================================================================
#  BLOCK 1 - Prepare : known History and temporary definitions
#==================================================================
$proc = Get-PowerCalc
$main = Get-MainWindow $proc
Send-MenuCommand $main $CMD_ADVANCED_VIEW
Send-MenuCommand $main $CMD_CLEAR_HISTORY
Start-Sleep -Milliseconds 400
$edit = Find-Path $main @("INPUTVIEWCLASS", "Edit")
foreach ($line in @("1+2", "zzpa=5", "zzpf(x)=x*zzpa", "zzpg(a,b)=a^b", "zzpf(2)", "1/0"))
{
	Submit $edit $line
}

#==================================================================
#  BLOCK 2 - Save History, Save and Load Functions & Variables
#==================================================================
$hist_file = Join-Path $raw "saved_history.txt"
$pcf_file = Join-Path $raw "saved_funcs.pcf"
Remove-Item $hist_file, $pcf_file -ErrorAction SilentlyContinue

$report.Add("save history: " + (Invoke-FileDialog $proc $main $CMD_SAVE_HISTORY $hist_file))
$report.Add("save funcs:   " + (Invoke-FileDialog $proc $main $CMD_SAVE_FUNCS $pcf_file))
foreach ($f in @($hist_file, $pcf_file))
{
	if (Test-Path $f)
	{
		$bytes = [IO.File]::ReadAllBytes($f)
		$report.Add("---- $(Split-Path -Leaf $f): $($bytes.Length) bytes, first bytes: " + (($bytes | Select-Object -First 4 | ForEach-Object { '{0:X2}' -f $_ }) -join " "))
		$report.Add((Get-Content $f -Raw))
	}
	else
	{
		$report.Add("---- $(Split-Path -Leaf $f): NOT WRITTEN")
	}
}

# Load: change a definition first so the reload is observable
Submit $edit "zzpa=99"
if (Test-Path $pcf_file)
{
	$report.Add("load funcs:   " + (Invoke-FileDialog $proc $main $CMD_LOAD_FUNCS $pcf_file))
	Send-MenuCommand $main $CMD_CLEAR_HISTORY
	Start-Sleep -Milliseconds 300
	Submit $edit "zzpa"
	$history = Find-Path $main @("INPUTVIEWCLASS", "RICHEDIT")
	$report.Add("zzpa after load: " + ([Win]::Text($history).Trim() -replace "`r`n|`r|`n", " \n "))
}

#==================================================================
#  BLOCK 3 - Polar range panel in the Graph window
#==================================================================
$owner = Find-Path $main @("ITEMLISTCLASS", "CALCGRAPHWINCLASS", "FUNCTIONLISTCLASS")
$before = [Win]::TopWindowsOf([uint32]$proc.Id)
[void][Win]::PostMessageW($owner, $WM_COMMAND, [IntPtr]$CMD_GRAPH_WINDOW, [IntPtr]::Zero)
$graph = Wait-Popup ([uint32]$proc.Id) $before
if ($graph -ne [IntPtr]::Zero)
{
	Send-MenuCommand $graph $CMD_POLAR
	Start-Sleep -Milliseconds 700
	$report.Add("---- graph window in Polar mode")
	foreach ($l in (Get-WindowTree $graph))
	{
		$report.Add($l)
	}
	Save-WindowShot $graph (Join-Path $raw "dlg_graph_Polar.png")
	Send-MenuCommand $graph $CMD_XY
	Start-Sleep -Milliseconds 400
	[void][Win]::PostMessageW($graph, $WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero)
}
else
{
	$report.Add("graph window did not open")
}

$report | Out-File -FilePath (Join-Path $root "spec\inventory\files_and_polar.txt") -Encoding utf8
$report
