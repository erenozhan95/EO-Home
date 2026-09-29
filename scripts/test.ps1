$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$output = Join-Path $repo 'artifacts\tests'
New-Item -ItemType Directory -Force $output,(Join-Path $output 'work') | Out-Null
$sources = Get-ChildItem -LiteralPath (Join-Path $repo 'src') -Filter '*.cs' | Select-Object -ExpandProperty FullName
Push-Location $output
try {
    foreach ($name in @('TestDashboard','TestScanPolicy','TestGeneral')) {
        $exe = Join-Path $output "$name.exe"
        & $compiler /nologo /codepage:65001 /target:exe "/main:$name" "/out:$exe" "/win32icon:$repo\assets\LightController.ico" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll $sources (Join-Path $repo "tests\$name.cs")
        if ($LASTEXITCODE -ne 0) { throw "$name derlenemedi." }
        & $exe
        if ($LASTEXITCODE -ne 0) { throw "$name başarısız." }
    }
} finally { Pop-Location }
