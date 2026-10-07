$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'src\LumenBar.WinUI\LumenBar.WinUI.csproj'
$output = Join-Path $PSScriptRoot 'dist\LumenBar-WinUI3-win-x64'
$distRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'dist'))
$staging = Join-Path $distRoot ('publish-' + [guid]::NewGuid().ToString('N'))
$previous = Join-Path $distRoot ('previous-' + [guid]::NewGuid().ToString('N'))
# All generated directory operations must stay inside this workspace's dist folder.
foreach ($target in @($output, $staging, $previous)) {
    if (-not [IO.Path]::GetFullPath($target).StartsWith($distRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid publish path' }
}
if (Get-Process LumenBar -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $output 'LumenBar.exe') }) { throw 'Close the running LumenBar application before publishing.' }
dotnet publish $project -c Release -r win-x64 --self-contained true -p:DebugType=None -o $staging
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $staging 'README.md')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'THIRD_PARTY_NOTICES.txt') -Destination (Join-Path $staging 'THIRD_PARTY_NOTICES.txt')
if (Test-Path -LiteralPath $output) { Move-Item -LiteralPath $output -Destination $previous }
try { Move-Item -LiteralPath $staging -Destination $output }
catch {
    if (Test-Path -LiteralPath $previous) { Move-Item -LiteralPath $previous -Destination $output }
    throw
}
if (Test-Path -LiteralPath $previous) { Remove-Item -LiteralPath $previous -Recurse -Force }
Write-Output $output
