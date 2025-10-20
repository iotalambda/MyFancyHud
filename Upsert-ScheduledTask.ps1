# Upsert-ScheduledTask.ps1
# This script creates or updates the MyFancyHud scheduled task to run at Windows startup
# This is the RECOMMENDED approach since Windows Services cannot show UI

$ErrorActionPreference = "Stop"

# Check if running as Administrator
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
$isAdmin = $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "ERROR: This script must be run as Administrator!" -ForegroundColor Red
    Write-Host "Please right-click PowerShell and select 'Run as Administrator'" -ForegroundColor Yellow
    exit 1
}

$taskName = "MyFancyHud"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = $scriptDir
$publishPath = Join-Path $scriptDir "bin\Release\net8.0-windows\win-x64\publish"
$exePath = Join-Path $publishPath "MyFancyHud.exe"

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "MyFancyHud Startup Installer" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "This will configure MyFancyHud to run automatically at login" -ForegroundColor Green
Write-Host "using Windows Task Scheduler (RECOMMENDED for UI applications)" -ForegroundColor Green
Write-Host ""

# Build and publish the project
Write-Host "Building and publishing the project..." -ForegroundColor Green
Push-Location $projectPath

try {
    dotnet publish -c Release -r win-x64 --self-contained false

    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }
} catch {
    Write-Host "ERROR: Build failed - $_" -ForegroundColor Red
    Pop-Location
    exit 1
}

Pop-Location

# Verify the executable exists
if (-not (Test-Path $exePath)) {
    Write-Host "ERROR: Executable not found at $exePath" -ForegroundColor Red
    exit 1
}

Write-Host "Executable found at: $exePath" -ForegroundColor Green

# Remove existing task if it exists
$existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if ($existingTask) {
    Write-Host "Removing existing scheduled task..." -ForegroundColor Yellow
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
}

# Create the scheduled task
Write-Host "Creating scheduled task..." -ForegroundColor Green

try {
    # Task action: Run the executable
    $action = New-ScheduledTaskAction -Execute $exePath

    # Task trigger: At user logon
    $trigger = New-ScheduledTaskTrigger -AtLogOn

    # Task settings
    $settings = New-ScheduledTaskSettingsSet `
        -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries `
        -StartWhenAvailable `
        -RunOnlyIfNetworkAvailable:$false `
        -DontStopOnIdleEnd `
        -RestartInterval (New-TimeSpan -Minutes 1) `
        -RestartCount 3

    # Task principal: Run with highest privileges for current user
    $principal = New-ScheduledTaskPrincipal `
        -UserId $env:USERNAME `
        -LogonType Interactive `
        -RunLevel Highest

    # Register the task
    Register-ScheduledTask `
        -TaskName $taskName `
        -Action $action `
        -Trigger $trigger `
        -Settings $settings `
        -Principal $principal `
        -Description "MyFancyHud - Displays idle messages and scheduled notifications" `
        -Force | Out-Null

    Write-Host ""
    Write-Host "======================================" -ForegroundColor Green
    Write-Host "SUCCESS!" -ForegroundColor Green
    Write-Host "======================================" -ForegroundColor Green
    Write-Host "Scheduled task '$taskName' created successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "The application will:" -ForegroundColor Cyan
    Write-Host "  • Start automatically when you log in" -ForegroundColor Cyan
    Write-Host "  • Run with your user permissions" -ForegroundColor Cyan
    Write-Host "  • Display UI windows on your desktop" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "To start it now, run:" -ForegroundColor Yellow
    Write-Host "  Start-ScheduledTask -TaskName '$taskName'" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "To disable auto-start:" -ForegroundColor Yellow
    Write-Host "  Disable-ScheduledTask -TaskName '$taskName'" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "To completely remove:" -ForegroundColor Yellow
    Write-Host "  Unregister-ScheduledTask -TaskName '$taskName'" -ForegroundColor Yellow
    Write-Host ""

    # Ask if user wants to start it now
    $startNow = Read-Host "Start MyFancyHud now? (Y/N)"
    if ($startNow -eq 'Y' -or $startNow -eq 'y') {
        Start-ScheduledTask -TaskName $taskName
        Write-Host "MyFancyHud started!" -ForegroundColor Green
    }

} catch {
    Write-Host "ERROR: Failed to create scheduled task - $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
