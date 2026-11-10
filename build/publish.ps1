#==================================================================
#  publish.ps1 - Build the portable single-file exes in dist\ (M9)
#==================================================================
# 1. freezes the engine (build_engine.ps1, one folder) and runs the
#    frozen-exe test; 2. zips it; 3. refreshes THIRD-PARTY-NOTICES.txt;
# 4. publishes the UI as single files with the engine zip embedded:
#    SoftCalc.exe self-contained and compressed, SoftCalc-net10.exe
#    needing the .NET 10 Desktop Runtime; 5. smoke-runs each exe from
#    an empty folder. The ECB rates of the day are embedded too
#    (rates-seed.xml, used before the first fetch; skipped when the ECB
#    cannot be reached).
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
# Today's ECB rates for a PC that was never online (owner 2026-11-10)
$seed = Join-Path $root "ui\SoftCalc\obj\rates-seed.xml"
if (Test-Path $seed) { Remove-Item $seed }
$seedArg = @()
try
{
	$xml = (Invoke-WebRequest "https://www.ecb.europa.eu/stats/eurofxref/eurofxref-daily.xml" -UseBasicParsing -TimeoutSec 20).Content
	if ($xml -notmatch "<Cube time='(\d{4}-\d\d-\d\d)'") { throw "not an ECB rates file" }
	[IO.File]::WriteAllText($seed, $xml)
	$seedArg = @("-p:RatesSeed=$seed")
	"rates-seed.xml ECB $($Matches[1])"
}
catch
{
	Write-Warning "ECB rates not embedded: $($_.Exception.Message)"
}
# end BLOCK 2

#==================================================================
#  BLOCK 3 - Publish the UI
#==================================================================
if (Test-Path $dist) { Remove-Item -Recurse -Force $dist }
$project = Join-Path $root "ui\SoftCalc\SoftCalc.csproj"
# SoftCalc.exe carries the .NET runtime; SoftCalc-net10.exe needs the
# .NET 10 Desktop Runtime (x64) installed and is about 9 MB (owner 2026-11-07)
& $dotnet publish $project -c Release -r win-x64 --self-contained true `
	-p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
	-p:DebugType=none "-p:EngineZip=$zip" @seedArg -o $dist --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "dotnet publish (self-contained) failed" }
$shared = Join-Path ([IO.Path]::GetTempPath()) ("softcalc-fd-" + [guid]::NewGuid().ToString("N"))
& $dotnet publish $project -c Release -r win-x64 --self-contained false `
	-p:PublishSingleFile=true -p:DebugType=none "-p:EngineZip=$zip" @seedArg -o $shared --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "dotnet publish (framework-dependent) failed" }
Move-Item (Join-Path $shared "SoftCalc.exe") (Join-Path $dist "SoftCalc-net10.exe")
Remove-Item -Recurse -Force $shared
$exes = @("SoftCalc.exe", "SoftCalc-net10.exe")
$extra = Get-ChildItem $dist | Where-Object { $exes -notcontains $_.Name }
if ($extra) { throw "publish left more than the two exes: $($extra.Name -join ', ')" }
foreach ($name in $exes)
{
	"published $name ($([math]::Round((Get-Item (Join-Path $dist $name)).Length / 1MB, 1)) MB)"
}
# end BLOCK 3

#==================================================================
#  BLOCK 4 - Smoke run from an empty folder
#==================================================================
# Not SOFTCALC_DATA_DIR: the point is that each exe makes SoftCalc-data\ itself
$saved = $env:SOFTCALC_DATA_DIR
Remove-Item Env:SOFTCALC_DATA_DIR -ErrorAction SilentlyContinue
try
{
	foreach ($name in $exes)
	{
		$smoke = Join-Path ([IO.Path]::GetTempPath()) ("softcalc-smoke-" + [guid]::NewGuid().ToString("N"))
		New-Item -ItemType Directory $smoke | Out-Null
		Copy-Item (Join-Path $dist $name) $smoke
		try
		{
			$shot = "softcalc_published_" + [IO.Path]::GetFileNameWithoutExtension($name) + ".png"
			& (Join-Path $root "tools\drive_softcalc.ps1") -Exe (Join-Path $smoke $name) -Lines @("1/3", "2^10") -Shot $shot | Out-Host
			$data = Join-Path $smoke "SoftCalc-data"
			$engineExe = Get-ChildItem (Join-Path $data "engine") -Recurse -Filter "softcalc-engine.exe" | Select-Object -First 1
			if (-not $engineExe) { throw "smoke ${name}: engine was not unpacked under $data" }
			if (-not (Test-Path (Join-Path $data "settings.json"))) { throw "smoke ${name}: settings.json not written in $data" }
			$history = Get-Content (Join-Path $data "history.jsonl") -Raw
			if ($history -notmatch '"text":"1024"') { throw "smoke ${name}: 2^10 not answered" }
			"smoke ok: $name"
		}
		finally
		{
			Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$smoke*" } | Stop-Process -Force
			Start-Sleep -Seconds 1
			Remove-Item -Recurse -Force $smoke -ErrorAction SilentlyContinue
		}
	}
}
finally
{
	if ($saved) { $env:SOFTCALC_DATA_DIR = $saved }
}
# end BLOCK 4
