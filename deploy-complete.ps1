# Complete Deployment Script for Energy Calculator
# Deploys both backend and frontend to Azure

$resourceGroup = "energy-dev"
$location = "West Europe"
$appServicePlan = "energy-plan"
$webAppName = "energycalc"
$storageAccountName = "energycalcstore"  # Using existing storage account

Write-Host "=== Energy Calculator - Complete Azure Deployment ===" -ForegroundColor Magenta
Write-Host ""

# Check if logged in to Azure
Write-Host "Checking Azure login status..." -ForegroundColor Yellow
$account = az account show --query "user.name" -o tsv 2>$null
if (-not $account) {
    Write-Host "Please login to Azure first: az login" -ForegroundColor Red
    exit 1
}
Write-Host "Logged in as: $account" -ForegroundColor Green
Write-Host ""

# === BACKEND DEPLOYMENT ===
Write-Host "=== DEPLOYING BACKEND ===" -ForegroundColor Blue

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

# Build and deploy backend
Write-Host "Building and deploying backend..." -ForegroundColor Yellow
Push-Location "backend"

dotnet clean
dotnet publish -c Release -o ./publish

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$zipFile = "../energy-backend-$timestamp.zip"

if (Test-Path "./publish") {
    Compress-Archive -Path "./publish/*" -DestinationPath $zipFile -Force
    Write-Host "Backend package created successfully" -ForegroundColor Green
} else {
    Write-Host "Backend build failed" -ForegroundColor Red
    Pop-Location
    exit 1
}

Pop-Location

# Deploy backend to Azure
az webapp deployment source config-zip `
    --resource-group $resourceGroup `
    --name $webAppName `
    --src $zipFile

# Configure backend application settings
az webapp config appsettings set `
    --resource-group $resourceGroup `
    --name $webAppName `
    --settings @("ASPNETCORE_ENVIRONMENT=Production", "WEBSITE_RUN_FROM_PACKAGE=1")

# Enable CORS for frontend
az webapp cors add `
    --resource-group $resourceGroup `
    --name $webAppName `
    --allowed-origins "*"

# Enable logging
az webapp log config `
    --resource-group $resourceGroup `
    --name $webAppName `
    --application-logging filesystem `
    --level information

# Get backend URL
$backendUrl = az webapp show --name $webAppName --resource-group $resourceGroup --query "defaultHostName" -o tsv
$backendApiUrl = "https://$backendUrl"

Write-Host "Backend deployed successfully!" -ForegroundColor Green
Write-Host "Backend URL: $backendApiUrl" -ForegroundColor Cyan
Write-Host ""

# === FRONTEND DEPLOYMENT ===
Write-Host "=== DEPLOYING FRONTEND ===" -ForegroundColor Blue

# List existing storage accounts in the resource group
Write-Host "Checking existing storage accounts in resource group..." -ForegroundColor Yellow
$existingAccounts = az storage account list --resource-group $resourceGroup --query "[].name" -o tsv
if ($existingAccounts) {
    Write-Host "Existing storage accounts:" -ForegroundColor Cyan
    $existingAccounts | ForEach-Object { Write-Host "  - $_" -ForegroundColor Cyan }
    
    Write-Host "Do you want to use an existing storage account? (y/N)" -ForegroundColor Yellow
    $useExisting = Read-Host
    
    if ($useExisting -eq "y" -or $useExisting -eq "Y") {
        Write-Host "Enter the storage account name to use:" -ForegroundColor Yellow
        $inputAccount = Read-Host
        if ($inputAccount -and ($existingAccounts -contains $inputAccount)) {
            $storageAccountName = $inputAccount
            Write-Host "Using existing storage account: $storageAccountName" -ForegroundColor Green
        } else {
            Write-Host "Invalid storage account name. Creating new one..." -ForegroundColor Yellow
        }
    }
}

# Create storage account if needed
$accountExists = az storage account show --name $storageAccountName --resource-group $resourceGroup --query "name" -o tsv 2>$null
if (-not $accountExists) {
    Write-Host "Creating storage account: $storageAccountName" -ForegroundColor Yellow
    az storage account create `
        --name $storageAccountName `
        --resource-group $resourceGroup `
        --location $location `
        --sku Standard_LRS `
        --kind StorageV2
} else {
    Write-Host "Storage account $storageAccountName already exists" -ForegroundColor Green
}

# Enable static website hosting
Write-Host "Enabling static website hosting..." -ForegroundColor Yellow
az storage blob service-properties update `
    --account-name $storageAccountName `
    --static-website `
    --index-document index.html `
    --404-document index.html

# Get storage account key
$storageKey = az storage account keys list --resource-group $resourceGroup --account-name $storageAccountName --query "[0].value" -o tsv

# Create production environment file for frontend
Write-Host "Creating production environment configuration..." -ForegroundColor Yellow
$envContent = "REACT_APP_API_URL=$backendApiUrl"
$envContent | Out-File -FilePath "frontend\.env.production" -Encoding UTF8

# Build frontend
Write-Host "Building React application..." -ForegroundColor Yellow
Push-Location "frontend"

npm install
$env:GENERATE_SOURCEMAP = "false"
npm run build

if (-not (Test-Path "./build")) {
    Write-Host "Frontend build failed" -ForegroundColor Red
    Pop-Location
    exit 1
}

Pop-Location

# Deploy frontend
Write-Host "Deploying frontend to Azure Storage..." -ForegroundColor Yellow
az storage blob delete-batch `
    --source "`$web" `
    --account-name $storageAccountName `
    --account-key $storageKey 2>$null

az storage blob upload-batch `
    --destination "`$web" `
    --source "./frontend/build" `
    --account-name $storageAccountName `
    --account-key $storageKey `
    --overwrite

# Get frontend URL
$frontendUrl = az storage account show --name $storageAccountName --resource-group $resourceGroup --query "primaryEndpoints.web" -o tsv

# Clean up deployment files
Remove-Item $zipFile -Force 2>$null
Remove-Item "frontend\.env.production" -Force 2>$null

Write-Host ""
Write-Host "=== DEPLOYMENT COMPLETED SUCCESSFULLY! ===" -ForegroundColor Green
Write-Host ""
Write-Host "Backend App Service:" -ForegroundColor Cyan
Write-Host "  URL: $backendApiUrl" -ForegroundColor White
Write-Host "  Health Check: $backendApiUrl/api/health" -ForegroundColor White
Write-Host "  Swagger: $backendApiUrl/swagger" -ForegroundColor White
Write-Host ""
Write-Host "Frontend Static Website:" -ForegroundColor Cyan
Write-Host "  URL: $frontendUrl" -ForegroundColor White
Write-Host ""
Write-Host "Resource Group: $resourceGroup" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "1. Test the backend health endpoint" -ForegroundColor White
Write-Host "2. Open the frontend URL and test the application" -ForegroundColor White
Write-Host "3. Verify the frontend can communicate with the backend API" -ForegroundColor White
