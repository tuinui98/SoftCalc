#==================================================================
#  probe_dialogs.ps1 - Open each toolbar dialog, dump it, cancel it
#==================================================================
# Reads the Graph and Variables toolbar buttons from the live PowerCalc
# process (button command IDs via remote memory), posts the same
# WM_COMMAND a click would send, dumps and screenshots the window that
# opens, then dismisses it with Cancel. Destructive buttons (Delete) are
# listed but never pressed. Needs elevation: run via run_elevated.ps1.

. "$PSScriptRoot\win32_probe.ps1"

Add-Type @"
using System;
using System.Runtime.InteropServices;

public static class Remote
{
	[DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
	[DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr VirtualAllocEx(IntPtr proc, IntPtr addr, IntPtr size, uint type, uint protect);
	[DllImport("kernel32.dll", SetLastError = true)] public static extern bool VirtualFreeEx(IntPtr proc, IntPtr addr, IntPtr size, uint type);
	[DllImport("kernel32.dll", SetLastError = true)] public static extern bool ReadProcessMemory(IntPtr proc, IntPtr addr, byte[] buf, IntPtr size, out IntPtr read);
	[DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
}
"@

$TB_GETBUTTON = 0x0417
$TB_BUTTONCOUNT = 0x0418
$TB_GETBUTTONTEXTW = 0x044B
$IDCANCEL = 2
$skip_labels = @("Delete")    # never press: may remove data without asking

$spec_dir = Join-Path (Split-Path -Parent $PSScriptRoot) "spec"
$out_file = Join-Path $spec_dir "inventory\dialogs.txt"
$lines = New-Object System.Collections.Generic.List[string]

function Find-Descendant([IntPtr]$hwnd, [string]$class_name)
{
	foreach ($child in [Win]::Children($hwnd))
	{
		if ([Win]::ClassOf($child) -eq $class_name)
		{
			return $child
		}
		$hit = Find-Descendant $child $class_name
		if ($hit -ne [IntPtr]::Zero)
		{
			return $hit
		}
	}
	return [IntPtr]::Zero
}

function Get-ToolbarButtons([IntPtr]$toolbar, [IntPtr]$proc_handle)
{
	# Target is 32-bit: TBBUTTON is 20 bytes (iBitmap, idCommand, fsState, fsStyle, 2 pad, dwData, iString)
	$buf = [Remote]::VirtualAllocEx($proc_handle, [IntPtr]::Zero, [IntPtr]512, 0x3000, 0x04)
	$buttons = @()
	try
	{
		$count = [int][Win]::SendMessageW($toolbar, $TB_BUTTONCOUNT, [IntPtr]::Zero, [IntPtr]::Zero)
		for ($i = 0; $i -lt $count; $i++)
		{
			[void][Win]::SendMessageW($toolbar, $TB_GETBUTTON, [IntPtr]$i, $buf)
			$raw = New-Object byte[] 20
			$read = [IntPtr]::Zero
			[void][Remote]::ReadProcessMemory($proc_handle, $buf, $raw, [IntPtr]20, [ref]$read)
			$cmd = [BitConverter]::ToInt32($raw, 4)
			$state = $raw[8]
			$style = $raw[9]

			$len = [int][Win]::SendMessageW($toolbar, $TB_GETBUTTONTEXTW, [IntPtr]$cmd, $buf)
			$label = ""
			if ($len -gt 0)
			{
				$text_raw = New-Object byte[] (($len + 1) * 2)
				[void][Remote]::ReadProcessMemory($proc_handle, $buf, $text_raw, [IntPtr]$text_raw.Length, [ref]$read)
				$label = [Text.Encoding]::Unicode.GetString($text_raw, 0, $len * 2)
			}
			$buttons += [pscustomobject]@{ Index = $i; Command = $cmd; Label = $label; Enabled = (($state -band 0x04) -ne 0); Separator = (($style -band 0x01) -ne 0) }
		}
	}
	finally
	{
		[void][Remote]::VirtualFreeEx($proc_handle, $buf, [IntPtr]::Zero, 0x8000)
	}
	return $buttons
}

function Wait-NewWindow([uint32]$proc_id, $before, [int]$timeout_ms)
{
	$deadline = (Get-Date).AddMilliseconds($timeout_ms)
	while ((Get-Date) -lt $deadline)
	{
		$now = [Win]::TopWindowsOf($proc_id)
		$fresh = @($now | Where-Object { $before -notcontains $_ })
		if ($fresh.Count -gt 0)
		{
			Start-Sleep -Milliseconds 400
			return $fresh[0]
		}
		Start-Sleep -Milliseconds 100
	}
	return [IntPtr]::Zero
}

function Close-Popup([IntPtr]$popup)
{
	[void][Win]::PostMessageW($popup, $WM_COMMAND, [IntPtr]$IDCANCEL, [IntPtr]::Zero)
	Start-Sleep -Milliseconds 500
	if ([Win]::IsWindowVisible($popup))
	{
		[void][Win]::PostMessageW($popup, $WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero)
		Start-Sleep -Milliseconds 500
	}
	return (-not [Win]::IsWindowVisible($popup))
}

#==================================================================
#  BLOCK 1 - Prepare : Advanced view, locate both toolbars
#==================================================================
$proc = Get-PowerCalc
$main = $proc.MainWindowHandle
Send-MenuCommand $main 32781
Start-Sleep -Milliseconds 700

$proc_handle = [Remote]::OpenProcess(0x0438, $false, $proc.Id)
if ($proc_handle -eq [IntPtr]::Zero)
{
	throw "OpenProcess failed; run elevated"
}

$sections = [ordered]@{
	graph = Find-Descendant $main "FUNCTIONLISTCLASS"
	variables = Find-Descendant $main "VARLISTCLASS"
}

#==================================================================
#  BLOCK 2 - Probe : press each safe button, dump, cancel
#==================================================================
$lines.Add("# PowerCalc toolbar dialogs")
$lines.Add("# captured: " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))
foreach ($name in $sections.Keys)
{
	$owner = $sections[$name]
	$toolbar = Find-Descendant $owner "ToolbarWindow32"
	$buttons = Get-ToolbarButtons $toolbar $proc_handle
	$lines.Add("")
	$lines.Add("## $name toolbar")
	foreach ($b in $buttons)
	{
		$lines.Add(("  [{0}] cmd={1} '{2}' enabled={3} separator={4}" -f $b.Index, $b.Command, $b.Label, $b.Enabled, $b.Separator))
	}

	foreach ($b in $buttons)
	{
		if ($b.Separator -or -not $b.Enabled -or $skip_labels -contains $b.Label)
		{
			continue
		}
		$before = [Win]::TopWindowsOf([uint32]$proc.Id)
		# Toolbar clicks reach the toolbar's parent as WM_COMMAND(cmd, hwndToolbar)
		[void][Win]::PostMessageW($owner, $WM_COMMAND, [IntPtr]$b.Command, $toolbar)
		$popup = Wait-NewWindow ([uint32]$proc.Id) $before 3000
		$lines.Add("")
		$lines.Add("### $name / $($b.Label)")
		if ($popup -eq [IntPtr]::Zero)
		{
			$lines.Add("  (no new window appeared)")
			continue
		}
		foreach ($l in (Get-WindowTree $popup))
		{
			$lines.Add("  $l")
		}
		$popup_menu = [Win]::GetMenu($popup)
		$lines.Add("  menu bar: " + $(if ($popup_menu -ne [IntPtr]::Zero) { "yes (dump separately)" } else { "none" }))
		$safe_name = ($name + "_" + $b.Label) -replace "[^A-Za-z0-9_]", ""
		Save-WindowShot $popup (Join-Path $spec_dir "raw\dlg_$safe_name.png")
		$closed = Close-Popup $popup
		$lines.Add("  closed via cancel: $closed")
	}
}

#==================================================================
#  BLOCK 3 - Cleanup : no popup may stay open
#==================================================================
[void][Remote]::CloseHandle($proc_handle)
$left = @([Win]::TopWindowsOf([uint32]$proc.Id) | Where-Object { $_ -ne $main })
$lines.Add("")
$lines.Add("leftover windows after probe: $($left.Count)")
$lines | Out-File -FilePath $out_file -Encoding utf8
$lines
