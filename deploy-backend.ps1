# Deploy Backend to Azure App Service
# Make sure you're logged in: az login

$resourceGroup = "energy-dev"
$location = "West Europe"
$appServicePlan = "energy-plan"
$webAppName = "energy-backend"
$subscriptionId = "your-subscription-id"  # Update this

Write-Host "Deploying Energy Calculator Backend to Azure..." -ForegroundColor Green

# Check if logged in to Azure
Write-Host "Checking Azure login status..." -ForegroundColor Yellow
$account = az account show --query "user.name" -o tsv 2>$null
if (-not $account) {
    Write-Host "Please login to Azure first: az login" -ForegroundColor Red
    exit 1
}
Write-Host "Logged in as: $account" -ForegroundColor Green

# Set subscription (optional - update subscription ID above)
# az account set --subscription $subscriptionId

# Check if resource group exists
Write-Host "Checking if resource group exists..." -ForegroundColor Yellow
$rgExists = az group exists --name $resourceGroup
if ($rgExists -eq "false") {
    Write-Host "Creating resource group: $resourceGroup" -ForegroundColor Yellow
    az group create --name $resourceGroup --location $location
} else {
    Write-Host "Resource group $resourceGroup already exists" -ForegroundColor Green
}

# Check if App Service Plan exists
Write-Host "Checking if App Service Plan exists..." -ForegroundColor Yellow
$planExists = az appservice plan show --name $appServicePlan --resource-group $resourceGroup --query "name" -o tsv 2>$null
if (-not $planExists) {
    Write-Host "Creating App Service Plan: $appServicePlan" -ForegroundColor Yellow
    az appservice plan create `
        --name $appServicePlan `
        --resource-group $resourceGroup `
        --sku B1 `
        --is-linux
} else {
    Write-Host "App Service Plan $appServicePlan already exists" -ForegroundColor Green
}

# Check if Web App exists
Write-Host "Checking if Web App exists..." -ForegroundColor Yellow
$appExists = az webapp show --name $webAppName --resource-group $resourceGroup --query "name" -o tsv 2>$null
if (-not $appExists) {
    Write-Host "Creating Web App: $webAppName" -ForegroundColor Yellow
    az webapp create `
        --name $webAppName `
        --resource-group $resourceGroup `
        --plan $appServicePlan `
        --runtime "DOTNET:9.0"
        
    # Configure startup command for .NET 9
    az webapp config set `
        --name $webAppName `
        --resource-group $resourceGroup `
        --startup-file "dotnet EnergyCalculator.dll"
} else {
    Write-Host "Web App $webAppName already exists" -ForegroundColor Green
}

# Build and package the application
Write-Host "Building and packaging the backend application..." -ForegroundColor Yellow
Push-Location "backend"

# Clean and build
dotnet clean
dotnet publish -c Release -o ./publish

# Create deployment package
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$zipFile = "../energy-backend-$timestamp.zip"
Write-Host "Creating deployment package: $zipFile" -ForegroundColor Yellow

# Compress the publish folder
if (Test-Path "./publish") {
    Compress-Archive -Path "./publish/*" -DestinationPath $zipFile -Force
    Write-Host "Package created successfully" -ForegroundColor Green
} else {
    Write-Host "Publish folder not found. Build may have failed." -ForegroundColor Red
    Pop-Location
    exit 1
}

Pop-Location

# Deploy to Azure
Write-Host "Deploying to Azure App Service..." -ForegroundColor Yellow
az webapp deployment source config-zip `
    --resource-group $resourceGroup `
    --name $webAppName `
    --src $zipFile

# Configure application settings (add any required environment variables here)
Write-Host "Configuring application settings..." -ForegroundColor Yellow
az webapp config appsettings set `
    --resource-group $resourceGroup `
    --name $webAppName `
    --settings @("ASPNETCORE_ENVIRONMENT=Production", "WEBSITE_RUN_FROM_PACKAGE=1")

# Enable logging
az webapp log config `
    --resource-group $resourceGroup `
    --name $webAppName `
    --application-logging filesystem `
    --level information

# Get the URL
$appUrl = az webapp show --name $webAppName --resource-group $resourceGroup --query "defaultHostName" -o tsv
Write-Host "Backend deployed successfully!" -ForegroundColor Green
Write-Host "URL: https://$appUrl" -ForegroundColor Cyan
Write-Host "Health Check: https://$appUrl/api/health" -ForegroundColor Cyan
Write-Host "Swagger: https://$appUrl/swagger" -ForegroundColor Cyan

# Clean up deployment package
Remove-Item $zipFile -Force
Write-Host "Deployment package cleaned up" -ForegroundColor Yellow

Write-Host "Backend deployment completed!" -ForegroundColor Green
