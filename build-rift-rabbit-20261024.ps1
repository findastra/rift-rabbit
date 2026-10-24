# Builds Rift Rabbit's program on this PC with the C# compiler that ships with Windows (.NET Framework 4). No downloads.
#   bin\rift-rabbit-20261024.exe        the launcher behind the Launch buttons (no window, no tray icon); --startup at sign-in
#   bin\rift-rabbit-check-20261024.exe  the same program with a console: --self-test, --programs
# The farm event format and the background-pet contract (menu and mood files for Startup Star) are Log Den's (one tool,
# one owner): clone findastra/log-den next to this folder, or name its folder with -LogDen.
# Claude Opus 5.5 (claude-opus-5-5), 2026-10-24.
param([string]$LogDen = (Join-Path (Split-Path $PSScriptRoot -Parent) 'log-den'))
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $compiler)) { throw "The C# compiler that ships with Windows was not found at $compiler." }
$contracts = Join-Path $LogDen 'contracts'
$sources = @((Join-Path $contracts 'farm-event-v1-20261018.cs'), (Join-Path $contracts 'background-pet-20261021.cs'), (Join-Path $PSScriptRoot 'rift-rabbit-20261024.cs'), (Join-Path $PSScriptRoot 'vr-launch-20261024.cs'))
foreach ($s in $sources) { if (-not (Test-Path $s)) { throw "Not found: $s. Clone findastra/log-den next to this folder (Log Den owns the shared contracts)." } }
$bin = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force $bin | Out-Null
$common = @('/nologo', '/platform:anycpu', '/optimize+', '/warn:4',
  '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll',
  '/reference:System.Windows.Forms.dll', '/reference:System.Web.Extensions.dll')
& $compiler @common '/target:winexe' ('/out:' + (Join-Path $bin 'rift-rabbit-20261024.exe')) @sources
if ($LASTEXITCODE -ne 0) { throw 'Rift Rabbit build failed. Quit its launcher (--quit) before rebuilding.' }
& $compiler @common '/target:exe' ('/out:' + (Join-Path $bin 'rift-rabbit-check-20261024.exe')) @sources
if ($LASTEXITCODE -ne 0) { throw 'Rift Rabbit console build failed.' }
Write-Output (Join-Path $bin 'rift-rabbit-20261024.exe')
