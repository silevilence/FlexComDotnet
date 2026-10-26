param([ValidateSet('x86', 'x64')][string]$Architecture = 'x64')
$ErrorActionPreference = 'Stop'
$commit = 'e4bfd6b03e50de46b47abfbd1e46b384f0c5f833'
$expectedHash = 'FEF0B17C5C3C9356FB8BA7366A3B196149DAADD131FB6A92ABEDA1CB4D3B9B85'
$cache = Join-Path $PSScriptRoot '.cache'
$output = Join-Path $PSScriptRoot "artifacts/$Architecture"
New-Item -ItemType Directory -Path $cache,$output -ErrorAction SilentlyContinue | Out-Null
$archive = Join-Path $cache 'detours.zip'
if (!(Test-Path -LiteralPath $archive)) {
    Invoke-WebRequest "https://codeload.github.com/microsoft/Detours/zip/$commit" -OutFile $archive
}
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Detours archive hash mismatch.' }
$source = Join-Path $cache "Detours-$commit"
if (!(Test-Path -LiteralPath $source)) { Expand-Archive -LiteralPath $archive -DestinationPath $cache }
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$visualStudio = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (!$visualStudio) { throw 'Visual C++ x86/x64 build tools are required.' }
$vcvars = Join-Path $visualStudio 'VC/Auxiliary/Build/vcvarsall.bat'
$batchPath = Join-Path $cache "build-$Architecture.cmd"
$library = if ($Architecture -eq 'x64') { 'lib.X64' } else { 'lib.X86' }
$batch = @"
@call "$vcvars" $Architecture >nul
@if errorlevel 1 exit /b 1
@cd /d "$source\src"
@nmake /nologo > "$output\detours-build.log" 2>&1
@if errorlevel 1 exit /b 1
@cd /d "$output"
@cl /nologo /std:c++17 /EHsc /W4 /WX /MT /DUNICODE /D_UNICODE /I"$source\include" /LD "$PSScriptRoot\Hook.cpp" /link /DEF:"$PSScriptRoot\Hook.def" "$source\$library\detours.lib" /OUT:FlexComProbeHook.dll
@if errorlevel 1 exit /b 1
@cl /nologo /std:c++17 /EHsc /W4 /WX /MT /DUNICODE /D_UNICODE /I"$source\include" "$PSScriptRoot\Probe.cpp" /link "$source\$library\detours.lib" user32.lib shell32.lib /SUBSYSTEM:CONSOLE /OUT:ProcessMonitor.Probe.exe
@if errorlevel 1 exit /b 1
@link /nologo Probe.obj "$source\$library\detours.lib" user32.lib shell32.lib /SUBSYSTEM:WINDOWS /OUT:ProcessMonitor.Probe.Gui.exe
@exit /b %errorlevel%
"@
[IO.File]::WriteAllText($batchPath, $batch, [Text.Encoding]::Default)
& $env:ComSpec /d /c $batchPath
if ($LASTEXITCODE -ne 0) { throw "Native build failed; see $output/detours-build.log" }
Copy-Item -LiteralPath (Join-Path $source 'LICENSE.md') -Destination (Join-Path $output 'Detours-LICENSE.md')
