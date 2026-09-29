$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# derleyicisi bulunamadı.' }
$output = Join-Path $repo 'dist\LightController'
New-Item -ItemType Directory -Force $output | Out-Null
$sources = Get-ChildItem -LiteralPath (Join-Path $repo 'src') -Filter '*.cs' | Select-Object -ExpandProperty FullName
& $compiler /nologo /codepage:65001 /target:winexe /optimize+ /platform:anycpu "/win32icon:$repo\assets\LightController.ico" "/out:$output\LightController.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll $sources
if ($LASTEXITCODE -ne 0) { throw "Derleme başarısız: $LASTEXITCODE" }
Copy-Item -LiteralPath (Join-Path $repo 'docs\KULLANIM.md') -Destination (Join-Path $output 'KULLANIM.md') -Force
Copy-Item -LiteralPath (Join-Path $repo 'THIRD_PARTY_NOTICES.md') -Destination $output -Force
New-Item -ItemType Directory -Force (Join-Path $output 'third-party') | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'third-party\smart-gadget-LICENSE.txt') -Destination (Join-Path $output 'third-party') -Force
if (Test-Path -LiteralPath (Join-Path $repo 'LICENSE')) { Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination $output -Force }
Write-Output "Hazır: $output\LightController.exe"
