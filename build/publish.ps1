#==================================================================
#  publish.ps1 - Build the portable single-file dist\SoftCalc.exe (M9)
#==================================================================
# 1. freezes the engine (build_engine.ps1, one folder) and runs the
#    frozen-exe test; 2. zips it; 3. refreshes THIRD-PARTY-NOTICES.txt;
# 4. publishes the UI self-contained, single file, compressed, with the
#    engine zip embedded; 5. smoke-runs the exe from an empty folder.
# Plan: design/m9_plan.md. Run from any folder:
#   powershell -ExecutionPolicy Bypass -File build\publish.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$python = Join-Path $root "engine\.venv\Scripts\python.exe"
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
$dist = Join-Path $root "dist"

#==================================================================
#  BLOCK 1 - Engine : freeze and test
#==================================================================
& powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "build_engine.ps1")
if ($LASTEXITCODE -ne 0) { throw "engine build failed" }
Push-Location (Join-Path $root "engine")
try
{
	& $python -m pytest -q "tests/test_protocol.py::test_frozen_exe_answers"
	if ($LASTEXITCODE -ne 0) { throw "frozen engine test failed" }
}
finally
{
	Pop-Location
}
# end BLOCK 1

#==================================================================
#  BLOCK 2 - Zip and notices
#==================================================================
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = Join-Path $root "ui\SoftCalc\obj\engine.zip"
New-Item -ItemType Directory -Force (Split-Path $zip) | Out-Null
if (Test-Path $zip) { Remove-Item $zip }
[IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $root "engine\dist\softcalc-engine"), $zip,
	[IO.Compression.CompressionLevel]::Optimal, $false)
"engine.zip $([math]::Round((Get-Item $zip).Length / 1MB, 1)) MB"
& $python (Join-Path $root "tools\make_notices.py")
if ($LASTEXITCODE -ne 0) { throw "notices failed" }
# end BLOCK 2

#==================================================================
#  BLOCK 3 - Publish the UI
#==================================================================
if (Test-Path $dist) { Remove-Item -Recurse -Force $dist }
& $dotnet publish (Join-Path $root "ui\SoftCalc\SoftCalc.csproj") -c Release -r win-x64 --self-contained true `
	-p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
	-p:DebugType=none "-p:EngineZip=$zip" -o $dist --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
$exe = Join-Path $dist "SoftCalc.exe"
$extra = Get-ChildItem $dist | Where-Object { $_.Name -ne "SoftCalc.exe" }
if ($extra) { throw "publish left more than SoftCalc.exe: $($extra.Name -join ', ')" }
"published $exe ($([math]::Round((Get-Item $exe).Length / 1MB, 1)) MB)"
# end BLOCK 3

#==================================================================
#  BLOCK 4 - Smoke run from an empty folder
#==================================================================
# Not SOFTCALC_DATA_DIR: the point is that the exe makes SoftCalc-data\ itself
$smoke = Join-Path ([IO.Path]::GetTempPath()) ("softcalc-smoke-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory $smoke | Out-Null
Copy-Item $exe $smoke
$saved = $env:SOFTCALC_DATA_DIR
Remove-Item Env:SOFTCALC_DATA_DIR -ErrorAction SilentlyContinue
try
{
	& (Join-Path $root "tools\drive_softcalc.ps1") -Exe (Join-Path $smoke "SoftCalc.exe") -Lines @("1/3", "2^10") -Shot "softcalc_published.png" | Out-Host
	$data = Join-Path $smoke "SoftCalc-data"
	$engineExe = Get-ChildItem (Join-Path $data "engine") -Recurse -Filter "softcalc-engine.exe" | Select-Object -First 1
	if (-not $engineExe) { throw "smoke: engine was not unpacked under $data" }
	if (-not (Test-Path (Join-Path $data "settings.json"))) { throw "smoke: settings.json not written in $data" }
	$history = Get-Content (Join-Path $data "history.jsonl") -Raw
	if ($history -notmatch '"text":"1024"') { throw "smoke: 2^10 not answered" }
	"smoke ok: $data"
}
finally
{
	if ($saved) { $env:SOFTCALC_DATA_DIR = $saved }
	Get-Process SoftCalc -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$smoke*" } | Stop-Process -Force
	Start-Sleep -Seconds 1
	Remove-Item -Recurse -Force $smoke -ErrorAction SilentlyContinue
}
# end BLOCK 4
