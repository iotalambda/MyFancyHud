# Remove-ScheduledTask.ps1
# This script removes the MyFancyHud scheduled task

param(
    [switch]$Force
)

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

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "MyFancyHud Scheduled Task Removal" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Check if task exists
$task = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue

if (-not $task) {
    Write-Host "Scheduled task '$taskName' is not installed." -ForegroundColor Yellow
    Write-Host "Nothing to remove." -ForegroundColor Yellow
    exit 0
}

Write-Host "Found scheduled task '$taskName'" -ForegroundColor Yellow
Write-Host "State: $($task.State)" -ForegroundColor Yellow
Write-Host ""

# Confirm removal if not forced
if (-not $Force) {
    $confirmation = Read-Host "Are you sure you want to remove this scheduled task? (Y/N)"
    if ($confirmation -ne 'Y' -and $confirmation -ne 'y') {
        Write-Host "Operation cancelled." -ForegroundColor Yellow
        exit 0
    }
}

# Stop the task if it's running
if ($task.State -eq 'Running') {
    Write-Host "Stopping scheduled task..." -ForegroundColor Yellow
    try {
        Stop-ScheduledTask -TaskName $taskName
        Start-Sleep -Seconds 1
        Write-Host "Task stopped." -ForegroundColor Green
    } catch {
        Write-Host "WARNING: Failed to stop task - $_" -ForegroundColor Yellow
        Write-Host "Attempting to continue with removal..." -ForegroundColor Yellow
    }
}

# Remove the scheduled task
Write-Host "Removing scheduled task..." -ForegroundColor Yellow

try {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false

    # Verify removal
    $taskCheck = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue

    if (-not $taskCheck) {
        Write-Host ""
        Write-Host "======================================" -ForegroundColor Green
        Write-Host "SUCCESS!" -ForegroundColor Green
        Write-Host "======================================" -ForegroundColor Green
        Write-Host "Scheduled task '$taskName' has been removed." -ForegroundColor Green
        Write-Host "MyFancyHud will no longer start automatically at login." -ForegroundColor Green
        Write-Host ""
    } else {
        Write-Host "WARNING: Task still appears in Task Scheduler." -ForegroundColor Yellow
        Write-Host "You may need to remove it manually via Task Scheduler." -ForegroundColor Yellow
    }
} catch {
    Write-Host "ERROR: Failed to remove scheduled task - $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "You may need to:" -ForegroundColor Yellow
    Write-Host "1. Open Task Scheduler (taskschd.msc)" -ForegroundColor Yellow
    Write-Host "2. Find and delete the '$taskName' task manually" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
