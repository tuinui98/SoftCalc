#==================================================================
#  run_elevated.ps1 - Launch PowerCalc and a probe script, elevated
#==================================================================
# PowerCalc only runs under an XP compat layer, and every XP layer
# forces elevation, so probes must run elevated too (UIPI blocks
# messages from a non-elevated process). Call this from a normal
# shell: it asks for one UAC consent, starts PowerCalc from the
# elevated child (no second prompt), runs the probe, and writes a log.
#
# Usage: .\tools\run_elevated.ps1 -Probe probe_views.ps1
#        .\tools\run_elevated.ps1 -Probe probe_eval.ps1 -Group A_arith

param
(
	[Parameter(Mandatory = $true)][string]$Probe,
	[string]$Group = "",
	[switch]$Inner
)

$root = Split-Path -Parent $PSScriptRoot
$log = Join-Path $root "spec\raw\probe_log.txt"

if (-not $Inner)
{
	$args_line = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Probe `"$Probe`" -Inner"
	if ($Group)
	{
		$args_line += " -Group `"$Group`""
	}
	# -Wait would also wait for PowerCalc, which the child may start
	$p = Start-Process powershell -Verb RunAs -ArgumentList $args_line -WindowStyle Hidden -PassThru
	$p.WaitForExit()
	"elevated probe exit code: $($p.ExitCode)"
	if (Test-Path $log)
	{
		Get-Content $log
	}
	exit $p.ExitCode
}

#==================================================================
#  BLOCK 1 - Elevated side : ensure PowerCalc runs, then probe
#==================================================================
Start-Transcript -Path $log -Force | Out-Null
try
{
	. "$PSScriptRoot\win32_probe.ps1"
	if (-not (Get-Process -Name PowerCalc -ErrorAction SilentlyContinue))
	{
		[void](Start-PowerCalc)
	}
	if ($Group)
	{
		# Comma-separated groups run in sequence under the same UAC consent
		foreach ($g in ($Group -split ","))
		{
			"===== group $g"
			if (-not (Get-Process -Name PowerCalc -ErrorAction SilentlyContinue))
			{
				[void](Start-PowerCalc)
			}
			& (Join-Path $PSScriptRoot $Probe) -Group $g.Trim()
		}
	}
	else
	{
		& (Join-Path $PSScriptRoot $Probe)
	}
}
catch
{
	"ERROR: $_"
	Stop-Transcript | Out-Null
	exit 1
}
Stop-Transcript | Out-Null
