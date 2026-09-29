$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    $tracked = @(git ls-files)
    if ($LASTEXITCODE -ne 0) { throw 'Git dosya listesi okunamadı.' }
    $privatePath = '(^|/)(Ayarlar|node_modules|target|dist|artifacts)/|(^|/)(diagnostics\.json|eo-light-v5\.json|\.env(?:\..*)?|[^/]+\.local\.json)$|\.(exe|dll|pdb|zip)$'
    $privateIP = '(?<!\d)(?:10\.(?:\d{1,3}\.){2}\d{1,3}|172\.(?:1[6-9]|2\d|3[01])\.\d{1,3}\.\d{1,3}|192\.168\.\d{1,3}\.\d{1,3})(?!\d)'
    $textExtensions = @('.cs', '.rs', '.ts', '.css', '.html', '.md', '.mjs', '.json', '.toml', '.yml', '.yaml', '.ps1', '.txt')
    $findings = @()
    foreach ($relative in $tracked) {
        if ($relative -match $privatePath) { $findings += "Özel dosya: $relative"; continue }
        $path = Join-Path $repo $relative
        if ([IO.Path]::GetExtension($relative) -notin $textExtensions) { continue }
        $content = [IO.File]::ReadAllText($path)
        if ($content -match $privateIP) { $findings += "Yerel IP: $relative" }
        if ($content -match '[A-Za-z]:\\Users\\') { $findings += "Kullanıcı yolu: $relative" }
    }
    if ($findings.Count) { $findings | ForEach-Object { Write-Error $_ }; exit 1 }
    Write-Output "PASS: $($tracked.Count) yayımlanan dosyada kişisel ağ verisi bulunmadı."
} finally {
    Pop-Location
}
