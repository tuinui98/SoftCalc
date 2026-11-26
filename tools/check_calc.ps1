#==================================================================
#  check_calc.ps1 - Check the engine's answers one by one (owner 2026-11-26)
#==================================================================
# Runs tools/check_calc.py with the engine's Python. Every case is printed
# (or only the failures with -Fails) and written to logs\check-<date-time>.txt.
#
#   .\tools\check_calc.ps1                       golden, matrix, stats, help
#   .\tools\check_calc.ps1 stats help            some suites
#   .\tools\check_calc.ps1 -Filter tcdf          only lines holding "tcdf"
#   .\tools\check_calc.ps1 -Fails                print only the failures
#   .\tools\check_calc.ps1 -Open                 open the log in Notepad afterwards

[CmdletBinding(PositionalBinding = $false)]    # suite names go to -Suites, never to -Filter
param
(
	[Parameter(Position = 0, ValueFromRemainingArguments = $true)][string[]]$Suites = @(),
	[string]$Filter = "",
	[switch]$Fails,
	[string]$Log = "",
	[switch]$Open
)

$root = Split-Path -Parent $PSScriptRoot
$python = Join-Path $root "engine\.venv\Scripts\python.exe"
if (-not (Test-Path $python))
{
	Write-Error "No engine Python at $python (see CLAUDE.md: python -m venv .venv in engine\)"
	exit 2
}
$arguments = @((Join-Path $PSScriptRoot "check_calc.py")) + $Suites
if ($Filter) { $arguments += @("--filter", $Filter) }
if ($Fails) { $arguments += "--fails" }
if (-not $Log) { $Log = Join-Path $root ("logs\check-" + (Get-Date -Format "yyyyMMdd-HHmmss") + ".txt") }
$arguments += @("--log", $Log)
$env:PYTHONIOENCODING = "utf-8"
& $python @arguments
$code = $LASTEXITCODE
if ($Open) { notepad $Log }
exit $code
