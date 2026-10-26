#==================================================================
#  probe_views.ps1 - Dump window tree and screenshot for each view
#==================================================================
# Switches PowerCalc through Numeric / History / Advanced view via the
# same WM_COMMAND its View menu sends, dumps each window tree, takes a
# screenshot, then restores the view that was active at start.

. "$PSScriptRoot\win32_probe.ps1"

$spec_dir = Join-Path (Split-Path -Parent $PSScriptRoot) "spec"
$views = [ordered]@{ numeric = 32779; history = 32780; advanced = 32781 }
$view_by_mode = @{ 1 = 32779; 2 = 32780; 3 = 32781 }    # iViewMode -> command id (inferred)

$proc = Get-PowerCalc
$hwnd = (Get-MainWindow $proc)
$state_key = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Applets\Calculator\State"
$start_mode = (Get-ItemProperty $state_key).iViewMode

foreach ($name in $views.Keys)
{
	Send-MenuCommand $hwnd $views[$name]
	Start-Sleep -Milliseconds 700
	$out = Join-Path $spec_dir "inventory\view_$name.txt"
	$tree = Get-WindowTree $hwnd
	$tree | Out-File -FilePath $out -Encoding utf8
	Save-WindowShot $hwnd (Join-Path $spec_dir "raw\view_$name.png")
	"== $name ($($tree.Count) windows)"
	$tree
}

if ($view_by_mode.ContainsKey([int]$start_mode))
{
	Send-MenuCommand $hwnd $view_by_mode[[int]$start_mode]
	"restored view mode $start_mode"
}
