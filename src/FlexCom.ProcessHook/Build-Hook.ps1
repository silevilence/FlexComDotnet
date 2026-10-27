param([ValidateSet('x86','x64')][string]$Architecture = 'x64')
$ErrorActionPreference = 'Stop'
$probe = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../tools/ProcessMonitor.Probe'))
& (Join-Path $probe 'Build-Probe.ps1') -Architecture $Architecture
$source = Join-Path $probe '.cache/Detours-e4bfd6b03e50de46b47abfbd1e46b384f0c5f833'
$output = Join-Path $PSScriptRoot "artifacts/$Architecture"
New-Item -ItemType Directory -Path $output -ErrorAction SilentlyContinue | Out-Null
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$visualStudio = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (!$visualStudio) { throw 'Visual C++ build tools are required.' }
$vcvars = Join-Path $visualStudio 'VC/Auxiliary/Build/vcvarsall.bat'
$library = if ($Architecture -eq 'x64') { 'lib.X64' } else { 'lib.X86' }
$batch = @"
@call "$vcvars" $Architecture >nul
@if errorlevel 1 exit /b 1
@cd /d "$output"
@cl /nologo /std:c++17 /utf-8 /EHsc /W4 /WX /MT /DUNICODE /D_UNICODE /I"$source\include" /LD "$PSScriptRoot\Hook.cpp" /link /DEF:"$PSScriptRoot\Hook.def" "$source\$library\detours.lib" /OUT:FlexCom.ProcessHook.dll
@if errorlevel 1 exit /b 1
@cl /nologo /std:c++17 /utf-8 /EHsc /W4 /WX /MT /DUNICODE /D_UNICODE /I"$source\include" "$PSScriptRoot\Helper.cpp" /link "$source\$library\detours.lib" /OUT:FlexCom.ProcessHook.Helper.exe
@if errorlevel 1 exit /b 1
@cl /nologo /std:c++17 /utf-8 /EHsc /W4 /WX /MT /DUNICODE /D_UNICODE "$probe\NativeTests.cpp" /link user32.lib /SUBSYSTEM:CONSOLE /OUT:NativeTests.exe
@if errorlevel 1 exit /b 1
@link /nologo NativeTests.obj user32.lib /SUBSYSTEM:WINDOWS /OUT:NativeTests.Gui.exe
@if errorlevel 1 exit /b 1
@cl /nologo /std:c++17 /utf-8 /EHsc /W4 /WX /MT /DUNICODE /D_UNICODE /I"$source\include" "$PSScriptRoot\..\..\tests\FlexCom.ProcessHook.Tests\HookContractTests.cpp" /link "$source\$library\detours.lib" /OUT:HookContractTests.exe
@exit /b %errorlevel%
"@
$batchPath = Join-Path $output 'build.cmd'
[IO.File]::WriteAllText($batchPath, $batch, [Text.Encoding]::Default)
& $env:ComSpec /d /c $batchPath
if ($LASTEXITCODE -ne 0) { throw 'Native Hook build failed.' }
Copy-Item -LiteralPath (Join-Path $source 'LICENSE.md') -Destination (Join-Path $output 'Detours-LICENSE.md')
