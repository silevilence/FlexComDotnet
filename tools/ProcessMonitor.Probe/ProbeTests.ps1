param([ValidateSet('x86', 'x64')][string]$Architecture = 'x64')
$ErrorActionPreference = 'Stop'
$binary = Join-Path $PSScriptRoot "artifacts/$Architecture/ProcessMonitor.Probe.exe"
if (!(Test-Path -LiteralPath $binary)) { throw "Missing probe: $binary" }
& $binary --self-test
if ($LASTEXITCODE -ne 0) { throw "Probe self-test failed: $LASTEXITCODE" }
