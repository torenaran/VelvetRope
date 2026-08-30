$ErrorActionPreference = 'Stop'

Write-Host 'Building Velvet Rope 0.3.10 (Release)...'
dotnet build "$PSScriptRoot\VelvetRope.slnx" -c Release

Write-Host ''
Write-Host 'Build complete.'
Write-Host 'Look under VelvetRope\bin\x64\Release for the plugin DLL/package output.'
