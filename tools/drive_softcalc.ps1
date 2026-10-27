#==================================================================
#  drive_softcalc.ps1 - Smoke-drive SoftCalc's own window for a visual check
#==================================================================
# Starts the Debug build of SoftCalc, types lines into its Input box
# through window messages (no real keyboard or mouse), switches views
# through its menu commands, and saves screenshots to spec/raw/.
# Only for SoftCalc itself; not for the original program.

param
(
	[string[]]$Lines = @("x=5", "2x+1", "1/3", "now [longdatetime]", "1 [ft->in]"),
	[string]$Shot = "softcalc_advanced.png",
	[switch]$OpenGraph
)

. "$PSScriptRoot\win32_probe.ps1"
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Drive
{
	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern IntPtr SendMessageW(IntPtr hwnd, uint msg, IntPtr w, string l);
}
"@

$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root "ui\SoftCalc\bin\Debug\net10.0-windows\SoftCalc.exe"
$proc = Start-Process $exe -PassThru
$main = [IntPtr]::Zero
for ($i = 0; $i -lt 60 -and $main -eq [IntPtr]::Zero; $i++)
{
	Start-Sleep -Milliseconds 250
	$proc.Refresh()
	$main = $proc.MainWindowHandle
}
Start-Sleep -Seconds 3    # engine start

function Find-Edit([IntPtr]$hwnd)
{
	foreach ($c in [Win]::Children($hwnd))
	{
		$cls = [Win]::ClassOf($c)
		$style = [Win]::GetWindowLongW($c, -16)
		# single-line EDIT (no ES_MULTILINE 0x4) is the Input box
		if ($cls -like "WindowsForms10.EDIT*" -and ($style -band 0x4) -eq 0)
		{
			return $c
		}
		$hit = Find-Edit $c
		if ($hit -ne [IntPtr]::Zero)
		{
			return $hit
		}
	}
	return [IntPtr]::Zero
}

$edit = Find-Edit $main
"main=$main edit=$edit"
foreach ($line in $Lines)
{
	[void][Drive]::SendMessageW($edit, 0x000C, [IntPtr]::Zero, $line)
	[void][Win]::PostMessageW($edit, 0x0100, [IntPtr]0x0D, [IntPtr]0x001C0001)
	Start-Sleep -Milliseconds 900
}
Start-Sleep -Milliseconds 500
Save-WindowShot $main (Join-Path $root "spec\raw\$Shot")
"saved $Shot"

if ($OpenGraph)
{
	# Press the sidebar's Window toolbar button through UI Automation, then shoot the Graph window
	Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
	$rootElement = [Windows.Automation.AutomationElement]::FromHandle($main)
	$cond = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty, "Window")
	$button = $rootElement.FindFirst([Windows.Automation.TreeScope]::Descendants, $cond)
	if ($button)
	{
		$button.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
		Start-Sleep -Seconds 2
		$graph = [Win]::TopWindowsOf([uint32]$proc.Id) | Where-Object { [Win]::Text($_) -eq "Graph" } | Select-Object -First 1
		if ($graph)
		{
			Save-WindowShot $graph (Join-Path $root "spec\raw\softcalc_graph_window.png")
			"saved softcalc_graph_window.png"
		}
	}
	else
	{
		"Window button not found"
	}
}
$proc.CloseMainWindow() | Out-Null
if (-not $proc.WaitForExit(5000))
{
	$proc.Kill()
}
"exited: $($proc.HasExited)"
