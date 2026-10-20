param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$project = Join-Path $root "src\DailyGoal\DailyGoal.csproj"
$publishDirectory = Join-Path $root "publish\win-x64"
$installerScript = Join-Path $root "installer\DailyGoal.iss"

$dotnetCandidates = @(
    "C:\Users\tongh\.dotnet\dotnet.exe",
    (Get-Command dotnet.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1)
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }
$dotnet = $dotnetCandidates | Select-Object -First 1
if (-not $dotnet) {
    throw "The .NET SDK was not found."
}

& $dotnet publish $project `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishProfile=WinX64SelfContained `
    -o $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "Self-contained publish failed with exit code $LASTEXITCODE."
}

$innoCandidates = @(
    "E:\Inno Setup 6\ISCC.exe",
    "D:\Inno Setup 7\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1)
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }
$iscc = $innoCandidates | Select-Object -First 1
if (-not $iscc) {
    throw "The Inno Setup compiler ISCC.exe was not found."
}

& $iscc $installerScript
if ($LASTEXITCODE -ne 0) {
    throw "Installer compilation failed with exit code $LASTEXITCODE."
}

$installer = Join-Path $root "dist\DailyGoal-Setup-2.5.0-x64.exe"
if (-not (Test-Path -LiteralPath $installer)) {
    throw "Installer output was not created: $installer"
}

Get-Item -LiteralPath $installer | Select-Object FullName, Length, LastWriteTime
