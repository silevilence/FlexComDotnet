[CmdletBinding()]
param(
    # Both profiles use a test certificate; this script does not produce WHQL-signed drivers.
    [ValidateSet('debug', 'release')]
    [string]$Profile = 'debug'
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$targetDirectory = Join-Path $projectRoot "target\$Profile"
$stagingDirectory = Join-Path $targetDirectory 'serial_monitor_package.staging'
$packageDirectory = Join-Path $targetDirectory 'serial_monitor_package'
$driverDll = Join-Path $targetDirectory 'serial_monitor.dll'
$templateInf = Join-Path $projectRoot 'serial_monitor.inx'
$stagedSys = Join-Path $stagingDirectory 'serial_monitor.sys'
$stagedInf = Join-Path $stagingDirectory 'serial_monitor.inf'
$stagedCat = Join-Path $stagingDirectory 'serial_monitor.cat'
$stagedCer = Join-Path $stagingDirectory 'WDRLocalTestCert.cer'

function Get-CertificateFromFile {
    param([Parameter(Mandatory)][string]$Path)

    return [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($Path)
}

function Find-PrivateCertificate {
    param([Parameter(Mandatory)][string]$Thumbprint)

    $store = [System.Security.Cryptography.X509Certificates.X509Store]::new(
        'WDRTestCertStore',
        [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
    try {
        $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadOnly)
        return @($store.Certificates | Where-Object {
                $_.Thumbprint -eq $Thumbprint -and $_.HasPrivateKey
            }) | Select-Object -First 1
    } catch {
        return $null
    } finally {
        $store.Close()
    }
}

function Get-Sha256 {
    param([Parameter(Mandatory)][string]$Path)

    $stream = [System.IO.File]::OpenRead($Path)
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        return -join ($algorithm.ComputeHash($stream) | ForEach-Object { $_.ToString('X2') })
    } finally {
        $algorithm.Dispose()
        $stream.Dispose()
    }
}

$cargoArguments = @('build', '--locked', '--features', 'kernel')
if ($Profile -eq 'release') { $cargoArguments += '--release' }
Push-Location $projectRoot
try {
    & cargo @cargoArguments
} finally {
    Pop-Location
}
if ($LASTEXITCODE -ne 0) {
    throw "cargo build failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $driverDll)) {
    throw "Driver DLL was not produced: $driverDll"
}

$kitsRoot = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows Kits\Installed Roots' -ErrorAction Stop).KitsRoot10
if ([string]::IsNullOrWhiteSpace($kitsRoot)) {
    throw 'Windows Kits root was not found.'
}

$sdkVersion = Get-ChildItem -LiteralPath (Join-Path $kitsRoot 'Lib') -Directory |
    Where-Object { $_.Name -like '10.*' -and (Test-Path (Join-Path $kitsRoot "bin\$($_.Name)\x64\stampinf.exe")) } |
    Sort-Object { [version]$_.Name } -Descending |
    Select-Object -First 1 -ExpandProperty Name
if ([string]::IsNullOrWhiteSpace($sdkVersion)) {
    throw "No Windows SDK version was found below $kitsRoot\Lib."
}

$kitBin = Join-Path $kitsRoot "bin\$sdkVersion"
$stampInf = Join-Path $kitBin 'x64\stampinf.exe'
$inf2Cat = Join-Path $kitBin 'x86\inf2cat.exe'
$signTool = Join-Path $kitBin 'x64\signtool.exe'
$makeCert = Join-Path $kitBin 'x64\makecert.exe'
foreach ($tool in @($stampInf, $inf2Cat, $signTool, $makeCert)) {
    if (-not (Test-Path -LiteralPath $tool)) {
        throw "Required WDK tool was not found: $tool"
    }
}

New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null
Copy-Item -LiteralPath $driverDll -Destination $stagedSys -Force
Copy-Item -LiteralPath $templateInf -Destination $stagedInf -Force

$manifest = Get-Content -LiteralPath (Join-Path $projectRoot 'Cargo.toml') -Raw
if ($manifest -notmatch '(?m)^version\s*=\s*"(\d+\.\d+\.\d+)"') {
    throw 'Driver package version was not found in Cargo.toml.'
}
$driverVersion = "$($Matches[1]).0"
& $stampInf -f $stagedInf -d '*' -a amd64 -c serial_monitor.cat -v $driverVersion
if ($LASTEXITCODE -ne 0) {
    throw "StampInf failed with exit code $LASTEXITCODE."
}

& $inf2Cat "/driver:$stagingDirectory" '/os:10_x64' '/uselocaltime'
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $stagedCat)) {
    throw "Inf2Cat failed with exit code $LASTEXITCODE."
}

$certificate = $null
$existingCer = Join-Path $packageDirectory 'WDRLocalTestCert.cer'
if (Test-Path -LiteralPath $existingCer) {
    $existingThumbprint = (Get-CertificateFromFile -Path $existingCer).Thumbprint
    $certificate = Find-PrivateCertificate -Thumbprint $existingThumbprint
}

if ($null -eq $certificate) {
    & $makeCert -r -pe -a SHA256 -eku 1.3.6.1.5.5.7.3.3 -ss WDRTestCertStore -n 'CN=WDRLocalTestCert' $stagedCer
    if ($LASTEXITCODE -ne 0) {
        throw "MakeCert failed with exit code $LASTEXITCODE."
    }
    $certificate = Get-CertificateFromFile -Path $stagedCer
} else {
    Copy-Item -LiteralPath $existingCer -Destination $stagedCer -Force
}

foreach ($file in @($stagedSys, $stagedCat)) {
    & $signTool sign /v /s WDRTestCertStore /sha1 $certificate.Thumbprint /fd SHA256 $file
    if ($LASTEXITCODE -ne 0) {
        throw "SignTool failed for $file with exit code $LASTEXITCODE."
    }
}

New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null
foreach ($fileName in @('serial_monitor.sys', 'serial_monitor.inf', 'serial_monitor.cat', 'WDRLocalTestCert.cer')) {
    Copy-Item -LiteralPath (Join-Path $stagingDirectory $fileName) -Destination (Join-Path $packageDirectory $fileName) -Force
}

$hash = Get-Sha256 -Path (Join-Path $packageDirectory 'serial_monitor.sys')
Write-Host "SerialMonitor $Profile test-signed package created: $packageDirectory"
Write-Host "Signer=$($certificate.Thumbprint) SHA256=$hash"
