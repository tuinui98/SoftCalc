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
	[switch]$OpenGraph,
	[switch]$OpenCompound,    # Tools > Compound Interest, screenshot of that window
	[string]$DataDir = ""    # SOFTCALC_DATA_DIR for this run, so tests never touch the owner's %APPDATA%
)

if ($DataDir)
{
	$env:SOFTCALC_DATA_DIR = $DataDir
}

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
# Wait until start-up has finished (the Input box is enabled again)
for ($i = 0; $i -lt 80 -and -not [Win]::IsWindowEnabled($edit); $i++)
{
	Start-Sleep -Milliseconds 250
}
"main=$main edit=$edit"
foreach ($line in $Lines)
{
	[void][Drive]::SendMessageW($edit, 0x000C, [IntPtr]::Zero, $line)
	[void][Win]::PostMessageW($edit, 0x0100, [IntPtr]0x0D, [IntPtr]0x001C0001)
	# Wait until the line is done: Input is read-only (ES_READONLY 0x800) while
	# it is evaluated, which may include an ECB rates fetch
	Start-Sleep -Milliseconds 300
	for ($i = 0; $i -lt 60 -and ([Win]::GetWindowLongW($edit, -16) -band 0x800) -ne 0; $i++)
	{
		Start-Sleep -Milliseconds 250
	}
	Start-Sleep -Milliseconds 300
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
if ($OpenCompound)
{
	# Open the Tools menu, then invoke its item, through UI Automation
	Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
	$auto = [Windows.Automation.AutomationElement]
	$scope = [Windows.Automation.TreeScope]::Descendants
	$tools = $auto::FromHandle($main).FindFirst($scope, (New-Object Windows.Automation.PropertyCondition($auto::NameProperty, "Tools")))
	if ($tools)
	{
		$tools.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
		Start-Sleep -Milliseconds 700
		$byPid = New-Object Windows.Automation.PropertyCondition($auto::ProcessIdProperty, [int]$proc.Id)
		$byName = New-Object Windows.Automation.PropertyCondition($auto::NameProperty, "Compound Interest...")
		$item = $auto::RootElement.FindFirst($scope, (New-Object Windows.Automation.AndCondition($byPid, $byName)))
		if ($item)
		{
			$item.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
			Start-Sleep -Seconds 3
			$window = [Win]::TopWindowsOf([uint32]$proc.Id) | Where-Object { [Win]::Text($_) -eq "Compound Interest" } | Select-Object -First 1
			if ($window)
			{
				Save-WindowShot $window (Join-Path $root "spec\raw\softcalc_compound.png")
				"saved softcalc_compound.png"
			}
		}
		else
		{
			"Compound Interest item not found"
		}
	}
}
$proc.CloseMainWindow() | Out-Null
if (-not $proc.WaitForExit(5000))
{
	$proc.Kill()
}
"exited: $($proc.HasExited)"
