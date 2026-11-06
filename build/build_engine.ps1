#==================================================================
#  build_engine.ps1 - Freeze the Python engine into softcalc-engine.exe
#==================================================================
# Builds engine\dist\softcalc-engine\ (softcalc-engine.exe plus
# _internal\) with PyInstaller, one folder (M9: a one-file build unpacks
# itself to %TEMP% on every start). Console subsystem: the UI talks to it
# over stdin/stdout and hides the window. Run from any folder:
#   powershell -ExecutionPolicy Bypass -File build\build_engine.ps1

$ErrorActionPreference = "Stop"
$engine = Join-Path (Split-Path -Parent $PSScriptRoot) "engine"
$python = Join-Path $engine ".venv\Scripts\python.exe"
if (-not (Test-Path $python))
{
	throw "engine venv missing: create it as described in CLAUDE.md"
}

Push-Location $engine
try
{
	& $python -m PyInstaller --noconfirm --clean --onedir --console `
		--name softcalc-engine `
		--distpath dist --workpath build --specpath build `
		--log-level WARN `
		softcalc_engine\__main__.py
	if ($LASTEXITCODE -ne 0)
	{
		throw "PyInstaller failed with exit code $LASTEXITCODE"
	}
	$folder = Join-Path $engine "dist\softcalc-engine"
	$size = (Get-ChildItem $folder -Recurse -File | Measure-Object Length -Sum).Sum
	"built $folder ($([math]::Round($size / 1MB, 1)) MB)"
}
finally
{
	Pop-Location
}
