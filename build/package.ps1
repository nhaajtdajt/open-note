<#
  Builds everything users download. Nothing extra to install for them: the .NET runtime ships inside.
    publish\OpenNote\                        self-contained app folder (win-x64)
    publish\OpenNote-<ver>-win-x64.zip       portable: unzip and run OpenNote.exe
    publish\OpenNote-<ver>-x64.msix          for the Microsoft Store (unsigned: the Store signs it)
    publish\OpenNote-Setup-<ver>-x64.exe     installer for GitHub Releases (needs Inno Setup 6)

  Store identity values come from Partner Center > Product identity (docs\STORE-SUBMISSION.md):
    .\build\package.ps1 -IdentityName 12345Name.OpenNote -Publisher "CN=..." -PublisherDisplayName "..."
#>
param(
    [string]$Version,
    [string]$IdentityName = "OpenNote.Dev",
    [string]$Publisher = "CN=OpenNoteDev",
    [string]$PublisherDisplayName = "Open Note contributors",
    [switch]$SkipTests
)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "src\OpenNote\OpenNote.csproj"
$out = Join-Path $root "publish"
$app = Join-Path $out "OpenNote"

if (-not $Version) { $Version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1 }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version must look like 1.2.3 (got '$Version')." }

function Invoke-Native([string]$exe) {
    & $exe @args
    if ($LASTEXITCODE -ne 0) { throw "$exe failed with exit code $LASTEXITCODE." }
}

if (-not $SkipTests) {
    Write-Host "== Tests"
    Invoke-Native dotnet test (Join-Path $root "tests\OpenNote.Tests\OpenNote.Tests.csproj") -c Release --nologo -v q
}

Write-Host "== Publish $Version (self-contained win-x64)"
if (Test-Path $app) { Remove-Item $app -Recurse -Force }
Invoke-Native dotnet publish $project -c Release -r win-x64 --self-contained true -o $app --nologo -v q `
    -p:Version=$Version -p:DebugType=none -p:GenerateDocumentationFile=false

Write-Host "== Zip"
$zip = Join-Path $out "OpenNote-$Version-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path $app -DestinationPath $zip -CompressionLevel Optimal

Write-Host "== MSIX"
function Find-MakeAppx {
    $kits = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makeappx.exe" -ErrorAction SilentlyContinue
    $nuget = Get-ChildItem "$env:USERPROFILE\.nuget\packages\microsoft.windows.sdk.buildtools\*\bin\*\x64\makeappx.exe" -ErrorAction SilentlyContinue
    @($kits) + @($nuget) | Where-Object { $_ } | Sort-Object { [version]($_.Directory.Parent.Name -replace '[^\d.]', '') } | Select-Object -Last 1
}
$makeappx = Find-MakeAppx
if (-not $makeappx) {
    Write-Warning "makeappx.exe not found (install the Windows SDK). Skipping MSIX."
} else {
    $stage = Join-Path $out "msix-stage"
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
    Copy-Item $app $stage -Recurse
    Copy-Item (Join-Path $root "packaging\msix\Images") (Join-Path $stage "Images") -Recurse
    $manifest = (Get-Content (Join-Path $root "packaging\msix\AppxManifest.xml") -Raw).
        Replace('$(IdentityName)', $IdentityName).
        Replace('$(Publisher)', [System.Security.SecurityElement]::Escape($Publisher)).
        Replace('$(PublisherDisplayName)', [System.Security.SecurityElement]::Escape($PublisherDisplayName)).
        Replace('$(Version)', "$Version.0")  # the Store needs the 4th part to be 0
    [System.IO.File]::WriteAllText((Join-Path $stage "AppxManifest.xml"), $manifest)
    $msix = Join-Path $out "OpenNote-$Version-x64.msix"
    Invoke-Native $makeappx.FullName pack /d $stage /p $msix /o /h SHA256 | Out-Null
    Remove-Item $stage -Recurse -Force
}

Write-Host "== Installer"
$iscc = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    Write-Warning "Inno Setup 6 not found (winget install JRSoftware.InnoSetup). Skipping the installer."
} else {
    Invoke-Native $iscc /Q "/DAppVersion=$Version" "/DSourceDir=$app" "/DOutputDir=$out" (Join-Path $root "packaging\innosetup\OpenNote.iss")
}

Write-Host "== Done"
Get-ChildItem $out -File | ForEach-Object { "{0,-40} {1,8:N1} MB" -f $_.Name, ($_.Length / 1MB) }
