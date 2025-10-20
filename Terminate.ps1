# Terminate.ps1
# This script terminates all MyFancyHud processes and stops/disables the scheduled task

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
Write-Host "MyFancyHud Termination Utility" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Stop and disable scheduled task
Write-Host "Checking for scheduled task '$taskName'..." -ForegroundColor Yellow
$existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue

if ($existingTask) {
    Write-Host "Stopping scheduled task..." -ForegroundColor Yellow
    Stop-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue

    Write-Host "Disabling scheduled task..." -ForegroundColor Yellow
    Disable-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue

    Write-Host "Scheduled task '$taskName' stopped and disabled" -ForegroundColor Green
} else {
    Write-Host "Scheduled task '$taskName' not found (may already be removed)" -ForegroundColor Yellow
}

# Terminate all running instances
Write-Host ""
Write-Host "Checking for running MyFancyHud processes..." -ForegroundColor Yellow
$runningProcesses = Get-Process -Name "MyFancyHud" -ErrorAction SilentlyContinue

if ($runningProcesses) {
    Write-Host "Found $($runningProcesses.Count) running instance(s)" -ForegroundColor Yellow
    Write-Host "Terminating all MyFancyHud processes..." -ForegroundColor Yellow
    $runningProcesses | Stop-Process -Force
    Start-Sleep -Seconds 1

    # Verify all processes are terminated
    $stillRunning = Get-Process -Name "MyFancyHud" -ErrorAction SilentlyContinue
    if ($stillRunning) {
        Write-Host "WARNING: Some processes may still be running" -ForegroundColor Red
    } else {
        Write-Host "All MyFancyHud processes terminated successfully" -ForegroundColor Green
    }
} else {
    Write-Host "No running MyFancyHud processes found" -ForegroundColor Green
}

Write-Host ""
Write-Host "======================================" -ForegroundColor Green
Write-Host "Termination Complete" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  • All MyFancyHud processes have been stopped" -ForegroundColor Cyan
Write-Host "  • Scheduled task has been stopped and disabled" -ForegroundColor Cyan
Write-Host ""
Write-Host "To completely remove the scheduled task, run:" -ForegroundColor Yellow
Write-Host "  Unregister-ScheduledTask -TaskName '$taskName' -Confirm:`$false" -ForegroundColor Yellow
Write-Host ""
Write-Host "To re-enable the scheduled task later, run:" -ForegroundColor Yellow
Write-Host "  Enable-ScheduledTask -TaskName '$taskName'" -ForegroundColor Yellow
Write-Host ""
