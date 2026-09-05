[CmdletBinding()]
param(
    [switch]$Run,
    [switch]$SkipSelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceRoot = Join-Path $projectRoot 'src'
$outputRoot = Join-Path $projectRoot 'dist'
$objectRoot = Join-Path $projectRoot 'obj'
$outputExe = Join-Path $outputRoot 'LaptopKeyboardDoctor.exe'
$manifestPath = Join-Path $projectRoot 'app.manifest'
$iconPath = Join-Path $objectRoot 'LaptopKeyboardDoctor.ico'

if (-not (Test-Path -LiteralPath $outputRoot)) {
    New-Item -ItemType Directory -Path $outputRoot | Out-Null
}
if (-not (Test-Path -LiteralPath $objectRoot)) {
    New-Item -ItemType Directory -Path $objectRoot | Out-Null
}

# Remove only artifacts produced by older versions. The final dist directory must
# contain exactly one portable EXE.
$legacyOutputFiles = @(
    ($outputExe + '.config'),
    (Join-Path $outputRoot 'self-test-result.txt')
)
foreach ($legacyFile in $legacyOutputFiles) {
    if (Test-Path -LiteralPath $legacyFile) {
        Remove-Item -LiteralPath $legacyFile -Force
    }
}

$compilerCandidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) {
    throw 'Cannot find the .NET Framework 4.x C# compiler. Enable .NET Framework 4.8 in Windows Features.'
}

function New-EmbeddedIcon {
    param([Parameter(Mandatory = $true)][string]$Path)

    Add-Type -AssemblyName System.Drawing
    if (-not ('LaptopKeyboardDoctor.NativeIcon' -as [type])) {
        Add-Type @'
using System;
using System.Runtime.InteropServices;
namespace LaptopKeyboardDoctor {
    public static class NativeIcon {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr handle);
    }
}
'@
    }

    $bitmap = New-Object System.Drawing.Bitmap -ArgumentList 64, 64
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $background = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(31, 111, 189))
    $keyboard = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $keyBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(220, 234, 247))
    $accent = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(245, 185, 66))
    $checkPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(22, 59, 104)), 4
    $handle = [IntPtr]::Zero
    $icon = $null
    $stream = $null

    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.FillRectangle($background, 0, 0, 64, 64)
        $graphics.FillRectangle($keyboard, 8, 12, 48, 32)
        for ($row = 0; $row -lt 3; $row++) {
            for ($column = 0; $column -lt 5; $column++) {
                $graphics.FillRectangle($keyBrush, 12 + ($column * 8), 16 + ($row * 8), 6, 5)
            }
        }
        $graphics.FillEllipse($accent, 39, 35, 22, 22)
        $graphics.DrawLines($checkPen, @(
            (New-Object System.Drawing.Point -ArgumentList 44, 46),
            (New-Object System.Drawing.Point -ArgumentList 49, 51),
            (New-Object System.Drawing.Point -ArgumentList 57, 41)
        ))
        $handle = $bitmap.GetHicon()
        $icon = [System.Drawing.Icon]::FromHandle($handle)
        $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Create)
        $icon.Save($stream)
    }
    finally {
        if ($stream) { $stream.Dispose() }
        if ($icon) { $icon.Dispose() }
        if ($handle -ne [IntPtr]::Zero) { [LaptopKeyboardDoctor.NativeIcon]::DestroyIcon($handle) | Out-Null }
        $checkPen.Dispose()
        $accent.Dispose()
        $keyBrush.Dispose()
        $keyboard.Dispose()
        $background.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
    }

    if (-not (Test-Path -LiteralPath $Path)) {
        throw 'Icon generation failed.'
    }
}

New-EmbeddedIcon -Path $iconPath

$sourceFiles = @(Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' -File | Sort-Object Name)
if ($sourceFiles.Count -lt 10) {
    throw "Missing source files: found only $($sourceFiles.Count) C# files in $sourceRoot"
}

$compilerArguments = @(
    '/nologo',
    '/target:winexe',
    '/optimize+',
    '/platform:anycpu',
    '/warn:4',
    '/codepage:65001',
    "/out:$outputExe",
    "/win32manifest:$manifestPath",
    "/win32icon:$iconPath",
    '/reference:System.dll',
    '/reference:System.Core.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll'
)
$compilerArguments += $sourceFiles.FullName

Write-Host 'Compiling Laptop Keyboard Doctor 2026...'
& $compiler @compilerArguments
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $outputExe)) {
    throw "Compilation failed with exit code $LASTEXITCODE"
}

if (-not $SkipSelfTest) {
    $selfTestOutput = Join-Path ([System.IO.Path]::GetTempPath()) ('LaptopKeyboardDoctor-self-test-' + [Guid]::NewGuid().ToString('N') + '.txt')
    try {
        $argumentLine = '--self-test "' + $selfTestOutput + '"'
        $process = Start-Process -FilePath $outputExe -ArgumentList $argumentLine -Wait -PassThru
        if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $selfTestOutput)) {
            throw "Self-test could not run or failed with exit code $($process.ExitCode)"
        }
        $selfTestText = Get-Content -LiteralPath $selfTestOutput -Raw
        if ($selfTestText -notmatch 'SELF TEST: PASS') {
            throw "Self-test failed:`r`n$selfTestText"
        }
        Write-Host $selfTestText.Trim()
    }
    finally {
        if (Test-Path -LiteralPath $selfTestOutput) {
            Remove-Item -LiteralPath $selfTestOutput -Force
        }
    }
}

$distFiles = @(Get-ChildItem -LiteralPath $outputRoot -File)
if ($distFiles.Count -ne 1 -or -not [string]::Equals($distFiles[0].FullName, $outputExe, [StringComparison]::OrdinalIgnoreCase)) {
    $unexpected = ($distFiles | ForEach-Object { $_.Name }) -join ', '
    throw "Single-file gate failed. dist must contain only LaptopKeyboardDoctor.exe; found: $unexpected"
}

$hash = (Get-FileHash -LiteralPath $outputExe -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "BUILD: PASS"
Write-Host "SINGLE-FILE DIST: PASS"
Write-Host "EXE: $outputExe"
Write-Host "SHA256: $hash"

if ($Run) {
    Start-Process -FilePath $outputExe | Out-Null
}
