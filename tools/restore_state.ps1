#==================================================================
#  restore_state.ps1 - Close PowerCalc and restore its saved settings
#==================================================================
# Undoes test pollution (temporary variables/functions, settings):
# closes PowerCalc first so it cannot rewrite the key on exit, then
# replaces its registry key with spec/raw/registry_backup_HKCU.reg.
# Needs elevation (PowerCalc runs elevated): run via run_elevated.ps1.

. "$PSScriptRoot\win32_probe.ps1"

$key = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Applets\Calculator"
$backup = Join-Path (Split-Path -Parent $PSScriptRoot) "spec\raw\registry_backup_HKCU.reg"
if (-not (Test-Path $backup))
{
	throw "backup not found: $backup"
}

$proc = Get-Process -Name PowerCalc -ErrorAction SilentlyContinue | Select-Object -First 1
if ($proc)
{
	[void][Win]::PostMessageW((Get-MainWindow $proc), $WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero)
	if (-not $proc.WaitForExit(10000))
	{
		throw "PowerCalc did not close; registry left untouched"
	}
	"PowerCalc closed"
}

$before = (Get-ChildItem $key -Recurse | ForEach-Object { $_.Property.Count } | Measure-Object -Sum).Sum
Remove-Item $key -Recurse -Force
reg import $backup 2>&1 | Out-String
$after = (Get-ChildItem $key -Recurse | ForEach-Object { $_.Property.Count } | Measure-Object -Sum).Sum
"values in subkeys before restore: $before, after: $after"
$f = Get-Item "$key\Functions"
foreach ($n in $f.Property)
{
	"  function $n = $($f.GetValue($n))"
}
"variables: $((Get-Item "$key\Variables").Property.Count)"
