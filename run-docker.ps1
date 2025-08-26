# Docker Runner for Chat Platform
# Optimized for clean code and production deployment

param(
    [switch]$SkipBuild,
    [switch]$SkipHealthCheck,
    [switch]$Verbose
)

Write-Host "=== Chat Platform Docker Runner v2.0 ===" -ForegroundColor Cyan
Write-Host "Clean Code & Production Ready" -ForegroundColor Green

# Function to write colored output
function Write-Status {
    param([string]$Message, [string]$Color = "White")
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $Message" -ForegroundColor $Color
}

# Function to check Docker health
function Test-DockerHealth {
    try {
        $health = docker-compose ps --format json | ConvertFrom-Json
        $unhealthy = $health | Where-Object { $_.State -ne "running" }
        
        if ($unhealthy) {
            Write-Status "Some services are not healthy:" "Red"
            $unhealthy | ForEach-Object { Write-Status "  - $($_.Name): $($_.State)" "Red" }
            return $false
        }
        
        Write-Status "All services are healthy!" "Green"
        return $true
    }
    catch {
        Write-Status "Error checking service health: $($_.Exception.Message)" "Red"
        return $false
    }
}

# Function to wait for services
function Wait-ForServices {
    param([int]$TimeoutSeconds = 120)
    
    Write-Status "Waiting for services to be ready (timeout: ${TimeoutSeconds}s)..." "Yellow"
    
    $startTime = Get-Date
    $elapsed = 0
    
    while ($elapsed -lt $TimeoutSeconds) {
        if (Test-DockerHealth) {
            Write-Status "Services are ready!" "Green"
            return $true
        }
        
        Start-Sleep -Seconds 5
        $elapsed = ((Get-Date) - $startTime).TotalSeconds
        
        if ($Verbose) {
            Write-Status "Elapsed: $([math]::Round($elapsed))s" "Gray"
        }
    }
    
    Write-Status "Timeout waiting for services to be ready" "Red"
    return $false
}

# Check if Docker is running
try {
    Write-Status "Checking Docker status..." "Blue"
    docker version | Out-Null
    Write-Status "Docker is running" "Green"
} 
catch {
    Write-Status "Docker is not running! Please start Docker Desktop first." "Red"
    Write-Status "Error: $($_.Exception.Message)" "Red"
    exit 1
}

# Check Docker Compose version
try {
    $composeVersion = docker-compose --version
    Write-Status "Docker Compose: $composeVersion" "Green"
}
catch {
    Write-Status "Docker Compose not found or not working" "Red"
    exit 1
}

# Stop any existing containers
Write-Status "Stopping existing containers..." "Yellow"
try {
    docker-compose down --remove-orphans
    Write-Status "Existing containers stopped" "Green"
}
catch {
    Write-Status "Warning: Error stopping containers: $($_.Exception.Message)" "Yellow"
}

# Clean up if requested
if (-not $SkipBuild) {
    Write-Status "Cleaning up Docker system..." "Yellow"
    try {
        docker system prune -f
        Write-Status "Docker system cleaned" "Green"
    }
    catch {
        Write-Status "Warning: Error cleaning Docker system: $($_.Exception.Message)" "Yellow"
    }
}

# Build and start services
Write-Status "Building and starting services..." "Green"
try {
    if ($SkipBuild) {
        docker-compose up -d
    } else {
        docker-compose up --build -d
    }
    Write-Status "Services started successfully" "Green"
}
catch {
    Write-Status "Error starting services: $($_.Exception.Message)" "Red"
    Write-Status "Trying to start without build..." "Yellow"
    
    try {
        docker-compose up -d
        Write-Status "Services started without build" "Green"
    }
    catch {
        Write-Status "Failed to start services: $($_.Exception.Message)" "Red"
        exit 1
    }
}

# Wait for services to be ready
if (-not $SkipHealthCheck) {
    if (Wait-ForServices -TimeoutSeconds 120) {
        Write-Status "All services are ready!" "Green"
    } else {
        Write-Status "Some services may not be ready. Check logs for details." "Yellow"
    }
} else {
    Write-Status "Skipping health check..." "Yellow"
    Start-Sleep -Seconds 10
}

# Show service status
Write-Status "Service status:" "Green"
try {
    docker-compose ps
}
catch {
    Write-Status "Error getting service status: $($_.Exception.Message)" "Red"
}

# Show recent logs
Write-Status "Recent logs:" "Green"
try {
    docker-compose logs --tail=20
}
catch {
    Write-Status "Error getting logs: $($_.Exception.Message)" "Red"
}

# Show endpoints
Write-Host "`n=== Chat Platform is running! ===" -ForegroundColor Green
Write-Host "Web UI:     http://localhost:5000" -ForegroundColor Cyan
Write-Host "API:        http://localhost:5000/api" -ForegroundColor Cyan
Write-Host "Swagger:    http://localhost:5000/swagger" -ForegroundColor Cyan
Write-Host "Health:     http://localhost:5000/health" -ForegroundColor Cyan
Write-Host "SignalR:    ws://localhost:5000/chathub" -ForegroundColor Cyan

# Show useful commands
Write-Host "`nUseful commands:" -ForegroundColor Yellow
Write-Host "  View logs:     docker-compose logs -f" -ForegroundColor White
Write-Host "  Stop:          docker-compose down" -ForegroundColor White
Write-Host "  Restart:       docker-compose restart" -ForegroundColor White
Write-Host "  Health check:  docker-compose ps" -ForegroundColor White
Write-Host "  Clean up:      docker system prune -f" -ForegroundColor White

# Show performance tips
Write-Host "`nPerformance tips:" -ForegroundColor Magenta
Write-Host "  - Use -SkipBuild for faster startup" -ForegroundColor Gray
Write-Host "  - Use -SkipHealthCheck for immediate access" -ForegroundColor Gray
Write-Host "  - Use -Verbose for detailed progress" -ForegroundColor Gray

# Interactive mode
Write-Host "`nPress any key to view live logs (Ctrl+C to exit)..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

# Show live logs
Write-Status "Showing live logs..." "Green"
try {
    docker-compose logs -f
}
catch {
    Write-Status "Error showing live logs: $($_.Exception.Message)" "Red"
    Write-Status "You can manually run: docker-compose logs -f" "Yellow"
}
