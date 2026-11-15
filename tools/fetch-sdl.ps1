<#
  Lädt die offizielle SDL3-Runtime (win32-x64) von github.com/libsdl-org/SDL,
  prüft den SHA-256 gegen den gepinnten Wert und legt SDL3.dll + Lizenz unter lib/SDL3 ab.
  Zum Aktualisieren: $Version und $Sha256 anpassen (Digest steht auf der GitHub-Release-Seite).
#>
param(
    [string]$Version = '3.4.20',
    [string]$Sha256  = '368bed8b24e44ad9ea372990814f2343fcfb9e468abb7f8f4a3bc146c7304752'
)
$ErrorActionPreference = 'Stop'
$root   = Split-Path -Parent $PSScriptRoot
$target = Join-Path $root 'lib/SDL3'
$tmp    = Join-Path ([IO.Path]::GetTempPath()) ("sdl3-" + [guid]::NewGuid())
New-Item -ItemType Directory -Force $tmp, $target | Out-Null
try {
    $zip = Join-Path $tmp 'sdl3.zip'
    $url = "https://github.com/libsdl-org/SDL/releases/download/release-$Version/SDL3-$Version-win32-x64.zip"
    Write-Host "Lade $url"
    Invoke-WebRequest $url -OutFile $zip -UseBasicParsing
    $actual = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $Sha256.ToLowerInvariant()) { throw "SHA-256 stimmt nicht: $actual" }
    Expand-Archive $zip -DestinationPath (Join-Path $tmp 'x') -Force
    Get-ChildItem (Join-Path $tmp 'x') -Recurse -Include 'SDL3.dll','LICENSE.txt','README-SDL.txt' |
        ForEach-Object { Copy-Item $_.FullName $target -Force }
    Set-Content (Join-Path $target 'VERSION.txt') "SDL $Version (win32-x64), sha256(zip)=$Sha256" -Encoding utf8
    Write-Host "SDL $Version nach $target kopiert."
}
finally { Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue }
