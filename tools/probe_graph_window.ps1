#==================================================================
#  probe_graph_window.ps1 - Dump the Graph window's own menu bar
#==================================================================
# Opens the Graph window through the Graph toolbar's Window command
# (cmd 97, found by probe_dialogs.ps1), dumps its menu tree, then
# closes it. Needs elevation: run via run_elevated.ps1.

. "$PSScriptRoot\win32_probe.ps1"

$CMD_GRAPH_WINDOW = 97
$spec_dir = Join-Path (Split-Path -Parent $PSScriptRoot) "spec"

$proc = Get-PowerCalc
$main = (Get-MainWindow $proc)
Send-MenuCommand $main 32781
Start-Sleep -Milliseconds 700

$owner = [IntPtr]::Zero
$queue = New-Object System.Collections.Generic.Queue[IntPtr]
$queue.Enqueue($main)
while ($queue.Count -gt 0 -and $owner -eq [IntPtr]::Zero)
{
	$h = $queue.Dequeue()
	foreach ($c in [Win]::Children($h))
	{
		if ([Win]::ClassOf($c) -eq "FUNCTIONLISTCLASS")
		{
			$owner = $c
			break
		}
		$queue.Enqueue($c)
	}
}
if ($owner -eq [IntPtr]::Zero)
{
	throw "Graph toolbar owner not found"
}

$before = [Win]::TopWindowsOf([uint32]$proc.Id)
[void][Win]::PostMessageW($owner, $WM_COMMAND, [IntPtr]$CMD_GRAPH_WINDOW, [IntPtr]::Zero)
$graph = [IntPtr]::Zero
for ($i = 0; $i -lt 30 -and $graph -eq [IntPtr]::Zero; $i++)
{
	Start-Sleep -Milliseconds 100
	$graph = @([Win]::TopWindowsOf([uint32]$proc.Id) | Where-Object { $before -notcontains $_ }) | Select-Object -First 1
	if (-not $graph)
	{
		$graph = [IntPtr]::Zero
	}
}
if ($graph -eq [IntPtr]::Zero)
{
	throw "Graph window did not open"
}
Start-Sleep -Milliseconds 400

& "$PSScriptRoot\dump_menu.ps1" -Hwnd ([long]$graph) -OutFile (Join-Path $spec_dir "inventory\graph_window_menu.txt")
Get-Content (Join-Path $spec_dir "inventory\graph_window_menu.txt")

[void][Win]::PostMessageW($graph, $WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero)
Start-Sleep -Milliseconds 500
"graph window closed: $(-not [Win]::IsWindowVisible($graph))"
