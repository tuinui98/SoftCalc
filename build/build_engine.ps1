#==================================================================
#  build_engine.ps1 - Freeze the Python engine into softcalc-engine.exe
#==================================================================
# Builds engine\dist\softcalc-engine.exe with PyInstaller (one file,
# console subsystem: the UI talks to it over stdin/stdout and hides
# the window). Run from any folder:
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
	& $python -m PyInstaller --noconfirm --clean --onefile --console `
		--name softcalc-engine `
		--distpath dist --workpath build --specpath build `
		--log-level WARN `
		softcalc_engine\__main__.py
	if ($LASTEXITCODE -ne 0)
	{
		throw "PyInstaller failed with exit code $LASTEXITCODE"
	}
	$exe = Join-Path $engine "dist\softcalc-engine.exe"
	"built $exe ($([math]::Round((Get-Item $exe).Length / 1MB, 1)) MB)"
}
finally
{
	Pop-Location
}
