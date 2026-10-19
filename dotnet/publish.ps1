param(
    [string]$Runtime = 'win-x64',
    [string]$Configuration = 'Release',
    [switch]$SelfContained
)

# Публикация WPF-приложения одним файлом (аналог portable/NSIS из JS-версии).
# По умолчанию — framework-dependent: на машине нужен .NET 9 Desktop Runtime.
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'src/XRayForge/XRayForge.csproj'
$output = Join-Path $root "artifacts/$Runtime"
$selfContained = if ($SelfContained) { 'true' } else { 'false' }

Write-Host "Publishing XRayForge ($Runtime, self-contained=$selfContained) -> $output"
dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained $selfContained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $output

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Done: $(Join-Path $output 'XRayForge.exe')"
