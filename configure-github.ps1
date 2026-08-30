param(
    [Parameter(Mandatory=$true)]
    [string]$GitHubUser
)

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$files = @(
    (Join-Path $root "repo.json"),
    (Join-Path $root "README.md"),
    (Join-Path $root "START-HERE.md"),
    (Join-Path $root "VelvetRope\VelvetRope.csproj")
)

foreach ($file in $files) {
    if (Test-Path $file) {
        $content = Get-Content -Raw -Path $file
        $content = $content.Replace("YOUR_GITHUB_USERNAME", $GitHubUser)
        Set-Content -Path $file -Value $content -Encoding UTF8
    }
}

Write-Host ""
Write-Host "Configured Velvet Rope for GitHub user: $GitHubUser" -ForegroundColor Green
Write-Host "Repository URL: https://github.com/$GitHubUser/VelvetRope"
Write-Host "Custom repo URL: https://raw.githubusercontent.com/$GitHubUser/VelvetRope/main/repo.json"
Write-Host ""
Write-Host "Next: follow START-HERE.md from Step 3." -ForegroundColor Cyan
