#==================================================================
#  probe_files2.ps1 - Guarded file-format retests, Polar, Edit Variable
#==================================================================
# Follow-up to probe_files.ps1, whose .pcf load hung the program. Every
# step that may hang is followed by a watchdog: if PowerCalc stops
# answering for 15 s it is killed and relaunched, and the step is
# recorded as HANG. Writes only under spec/raw/. Needs elevation: run
# via run_elevated.ps1.

. "$PSScriptRoot\win32_probe.ps1"

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class F2
{
	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern IntPtr SendMessageW(IntPtr hwnd, uint msg, IntPtr w, string l);
	[DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
	[DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr VirtualAllocEx(IntPtr proc, IntPtr addr, IntPtr size, uint type, uint protect);
	[DllImport("kernel32.dll", SetLastError = true)] public static extern bool VirtualFreeEx(IntPtr proc, IntPtr addr, IntPtr size, uint type);
	[DllImport("kernel32.dll", SetLastError = true)] public static extern bool ReadProcessMemory(IntPtr proc, IntPtr addr, byte[] buf, IntPtr size, out IntPtr read);
	[DllImport("kernel32.dll", SetLastError = true)] public static extern bool WriteProcessMemory(IntPtr proc, IntPtr addr, byte[] buf, IntPtr size, out IntPtr written);
	[DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
}
"@

$WM_SETTEXT = 0x000C
$WM_KEYDOWN = 0x0100
$WM_CHAR = 0x0102
$WM_KEYUP = 0x0101
$VK_RETURN = 0x0D
$IDOK = 1
$IDCANCEL = 2
$LVM_GETITEMCOUNT = 0x1004
$LVM_SETITEMSTATE = 0x102B
$LVM_GETITEMTEXTW = 0x1073
$CMD_SAVE_HISTORY = 32801
$CMD_SAVE_FUNCS = 32783
$CMD_LOAD_FUNCS = 32784
$CMD_CLEAR_HISTORY = 32800
$CMD_ADVANCED_VIEW = 32781
$CMD_TB_EDIT = 96
$CMD_GRAPH_WINDOW = 97
$CMD_POLAR = 32815
$CMD_XY = 32814

$root = Split-Path -Parent $PSScriptRoot
$raw = Join-Path $root "spec\raw"
$pcf_dir = Join-Path $raw "pcf_tests"
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

function Connect
{
	# (Re)binds the script-wide handles to the current PowerCalc instance
	$script:proc = Get-PowerCalc
	$script:main = Get-MainWindow $script:proc
	Send-MenuCommand $script:main $CMD_ADVANCED_VIEW
	Start-Sleep -Milliseconds 400
	$script:edit = Find-Path $script:main @("INPUTVIEWCLASS", "Edit")
	$script:history = Find-Path $script:main @("INPUTVIEWCLASS", "RICHEDIT")
}

function Watch([string]$step)
{
	# Returns $true when healthy; on a hang or exit kills, relaunches, reconnects
	$deadline = (Get-Date).AddSeconds(15)
	while ((Get-Date) -lt $deadline)
	{
		$script:proc.Refresh()
		if ($script:proc.HasExited)
		{
			$report.Add("!! $step : CRASH (process exited)")
			[void](Start-PowerCalc)
			Connect
			return $false
		}
		if ([Win]::Responds($script:main, 1000))
		{
			return $true
		}
	}
	$report.Add("!! $step : HANG (no response for 15 s), killed")
	Stop-Process -Id $script:proc.Id -Force
	Start-Sleep -Seconds 1
	[void](Start-PowerCalc)
	Connect
	return $false
}

function Submit([string]$text)
{
	[void][F2]::SendMessageW($script:edit, $WM_SETTEXT, [IntPtr]::Zero, $text)
	[void][Win]::PostMessageW($script:edit, $WM_KEYDOWN, [IntPtr]$VK_RETURN, [IntPtr]0x001C0001)
	[void][Win]::PostMessageW($script:edit, $WM_CHAR, [IntPtr]$VK_RETURN, [IntPtr]0x001C0001)
	[void][Win]::PostMessageW($script:edit, $WM_KEYUP, [IntPtr]$VK_RETURN, [IntPtr]0xC01C0001)
	Start-Sleep -Milliseconds 450
}

function Read-History
{
	return ([Win]::Text($script:history).Trim() -replace "`r`n|`r|`n", " \n ")
}

function Wait-Popup($before)
{
	for ($i = 0; $i -lt 50; $i++)
	{
		Start-Sleep -Milliseconds 100
		$fresh = @([Win]::TopWindowsOf([uint32]$script:proc.Id) | Where-Object { $before -notcontains $_ })
		if ($fresh.Count -gt 0)
		{
			Start-Sleep -Milliseconds 600
			return $fresh[0]
		}
	}
	return [IntPtr]::Zero
}

function Invoke-FileDialog([int]$cmd, [string]$path)
{
	$before = [Win]::TopWindowsOf([uint32]$script:proc.Id)
	Send-MenuCommand $script:main $cmd
	$dlg = Wait-Popup $before
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
		[void][Win]::PostMessageW($dlg, $WM_COMMAND, [IntPtr]$IDCANCEL, [IntPtr]::Zero)
		return "dialog has no edit box; cancelled"
	}
	[void][F2]::SendMessageW($name_box, $WM_SETTEXT, [IntPtr]::Zero, $path)
	[void][Win]::PostMessageW($dlg, $WM_COMMAND, [IntPtr]$IDOK, [IntPtr]::Zero)
	Start-Sleep -Milliseconds 1500
	if ([Win]::Responds($dlg, 500) -and [Win]::IsWindowVisible($dlg))
	{
		[void][Win]::PostMessageW($dlg, $WM_COMMAND, [IntPtr]$IDCANCEL, [IntPtr]::Zero)
		return "dialog still open after OK; cancelled"
	}
	return "ok"
}

function Write-Pcf([string]$name, [string[]]$lines)
{
	# Same encoding the program writes: UTF-16 LE, no BOM, CRLF
	$path = Join-Path $pcf_dir $name
	$text = ($lines -join "`r`n") + "`r`n"
	[IO.File]::WriteAllBytes($path, [Text.Encoding]::Unicode.GetBytes($text))
	return $path
}

function Describe-File([string]$path)
{
	if (-not (Test-Path $path))
	{
		return "NOT WRITTEN"
	}
	$bytes = [IO.File]::ReadAllBytes($path)
	$head = ($bytes | Select-Object -First 4 | ForEach-Object { '{0:X2}' -f $_ }) -join " "
	$enc = [Text.Encoding]::Unicode
	if ($bytes.Length -ge 2 -and $bytes[1] -ne 0)
	{
		$enc = [Text.Encoding]::Default
	}
	$text = $enc.GetString($bytes) -replace "`r`n", " | "
	return "$($bytes.Length) bytes, head=$head, text: $text"
}

#==================================================================
#  BLOCK 1 - Save History retest
#==================================================================
New-Item -ItemType Directory -Force $pcf_dir | Out-Null
Connect
Send-MenuCommand $main $CMD_CLEAR_HISTORY
foreach ($l in @("1+2", "zzsv=5", "2*zzsv", "1/0"))
{
	Submit $l
}
$report.Add("history on screen: " + (Read-History))
$hist_file = Join-Path $raw "saved_history2.txt"
Remove-Item $hist_file -ErrorAction SilentlyContinue
$report.Add("save history: " + (Invoke-FileDialog $CMD_SAVE_HISTORY $hist_file))
[void](Watch "save history")
Start-Sleep -Milliseconds 500
$report.Add("saved_history2.txt: " + (Describe-File $hist_file))

#==================================================================
#  BLOCK 2 - Save Functions & Variables retest (are values empty?)
#==================================================================
$save_file = Join-Path $raw "saved_funcs2.pcf"
Remove-Item $save_file -ErrorAction SilentlyContinue
Submit "zzsw=7"
Submit "zzsf(x)=x*zzsw"
$report.Add("save funcs: " + (Invoke-FileDialog $CMD_SAVE_FUNCS $save_file))
[void](Watch "save funcs")
$report.Add("saved_funcs2.pcf: " + (Describe-File $save_file))

#==================================================================
#  BLOCK 3 - Load hand-made .pcf files one at a time
#==================================================================
$cases = [ordered]@{
	"t1_func_only.pcf"   = @("zzt(x) = x+1")
	"t2_var_value.pcf"   = @("zzv=5")
	"t3_var_empty.pcf"   = @("zzv=")
	"t4_var_and_func.pcf" = @("zzv=5", "zzt(x) = x*zzv")
	"t5_two_params.pcf"  = @("zzu(a, b) = a^b")
	"t6_answer_body.pcf" = @("zzt(x) = x*answer")
}
foreach ($name in $cases.Keys)
{
	$path = Write-Pcf $name $cases[$name]
	$result = Invoke-FileDialog $CMD_LOAD_FUNCS $path
	$healthy = Watch "load $name"
	if ($healthy)
	{
		Send-MenuCommand $main $CMD_CLEAR_HISTORY
		Start-Sleep -Milliseconds 200
		foreach ($probe in @("zzv", "zzt(2)", "zzu(2,3)", "zzsw"))
		{
			Submit $probe
		}
		$report.Add("load ${name}: $result; after: " + (Read-History))
	}
	else
	{
		$report.Add("load ${name}: $result; program did not survive")
	}
}

#==================================================================
#  BLOCK 4 - Polar range panel in the Graph window
#==================================================================
$owner = Find-Path $main @("ITEMLISTCLASS", "CALCGRAPHWINCLASS", "FUNCTIONLISTCLASS")
$before = [Win]::TopWindowsOf([uint32]$proc.Id)
[void][Win]::PostMessageW($owner, $WM_COMMAND, [IntPtr]$CMD_GRAPH_WINDOW, [IntPtr]::Zero)
$graph = Wait-Popup $before
if ($graph -ne [IntPtr]::Zero)
{
	Send-MenuCommand $graph $CMD_POLAR
	Start-Sleep -Milliseconds 800
	$report.Add("---- graph window in Polar mode")
	foreach ($l in (Get-WindowTree $graph))
	{
		$report.Add($l)
	}
	Save-WindowShot $graph (Join-Path $raw "dlg_graph_Polar.png")
	Send-MenuCommand $graph $CMD_XY
	Start-Sleep -Milliseconds 400
	[void][Win]::PostMessageW($graph, $WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero)
	Start-Sleep -Milliseconds 400
}
else
{
	$report.Add("graph window did not open")
}

#==================================================================
#  BLOCK 5 - Edit Variable : select a user variable, open Edit
#==================================================================
Submit "zzev=7"
$var_owner = Find-Path $main @("ITEMLISTCLASS", "VARLISTCLASS")
$list = Find-Path $var_owner @("SysListView32")
$ph = [F2]::OpenProcess(0x0438, $false, $proc.Id)
$buf = [F2]::VirtualAllocEx($ph, [IntPtr]::Zero, [IntPtr]4096, 0x3000, 0x04)
try
{
	# 32-bit LVITEMW at buf, text buffer at buf+512
	$count = [int][Win]::SendMessageW($list, $LVM_GETITEMCOUNT, [IntPtr]::Zero, [IntPtr]::Zero)
	$target = -1
	$names = @()
	for ($i = 0; $i -lt $count; $i++)
	{
		$item = New-Object byte[] 64
		[BitConverter]::GetBytes([int]0).CopyTo($item, 8)                          # iSubItem
		[BitConverter]::GetBytes([int]($buf.ToInt64() + 512)).CopyTo($item, 20)     # pszText
		[BitConverter]::GetBytes([int]255).CopyTo($item, 24)                        # cchTextMax
		$w = [IntPtr]::Zero
		[void][F2]::WriteProcessMemory($ph, $buf, $item, [IntPtr]64, [ref]$w)
		$len = [int][Win]::SendMessageW($list, $LVM_GETITEMTEXTW, [IntPtr]$i, $buf)
		$txt = New-Object byte[] 512
		[void][F2]::ReadProcessMemory($ph, [IntPtr]($buf.ToInt64() + 512), $txt, [IntPtr]512, [ref]$w)
		$name = [Text.Encoding]::Unicode.GetString($txt, 0, [Math]::Max(0, $len) * 2)
		$names += $name
		if ($name -ieq "zzev")
		{
			$target = $i
		}
	}
	$report.Add("variable list items: " + ($names -join ", "))
	if ($target -ge 0)
	{
		$item = New-Object byte[] 64
		[BitConverter]::GetBytes([int]3).CopyTo($item, 12)    # state = LVIS_FOCUSED|LVIS_SELECTED
		[BitConverter]::GetBytes([int]3).CopyTo($item, 16)    # stateMask
		[void][F2]::WriteProcessMemory($ph, $buf, $item, [IntPtr]64, [ref]$w)
		[void][Win]::SendMessageW($list, $LVM_SETITEMSTATE, [IntPtr]$target, $buf)
		Start-Sleep -Milliseconds 400
		$before = [Win]::TopWindowsOf([uint32]$proc.Id)
		[void][Win]::PostMessageW($var_owner, $WM_COMMAND, [IntPtr]$CMD_TB_EDIT, [IntPtr]::Zero)
		$dlg = Wait-Popup $before
		if ($dlg -ne [IntPtr]::Zero)
		{
			$report.Add("---- Edit Variable dialog")
			foreach ($l in (Get-WindowTree $dlg))
			{
				$report.Add($l)
			}
			Save-WindowShot $dlg (Join-Path $raw "dlg_variables_Edit.png")
			[void][Win]::PostMessageW($dlg, $WM_COMMAND, [IntPtr]$IDCANCEL, [IntPtr]::Zero)
			Start-Sleep -Milliseconds 400
		}
		else
		{
			$report.Add("Edit Variable: no dialog appeared")
		}
	}
	else
	{
		$report.Add("Edit Variable: zzev not found in list")
	}
}
finally
{
	[void][F2]::VirtualFreeEx($ph, $buf, [IntPtr]::Zero, 0x8000)
	[void][F2]::CloseHandle($ph)
}

$report | Out-File -FilePath (Join-Path $root "spec\inventory\files_polar_editvar.txt") -Encoding utf8
$report
