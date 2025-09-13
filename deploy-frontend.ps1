# Deploy Frontend to Azure Storage Static Website
# Make sure you're logged in: az login

$resourceGroup = "energy-dev"
$location = "West Europe"
$storageAccountName = "energywebui"  # Update if you want to use existing storage account
$containerName = "`$web"

Write-Host "Deploying Energy Calculator Frontend to Azure Storage Static Website..." -ForegroundColor Green

# Check if logged in to Azure
Write-Host "Checking Azure login status..." -ForegroundColor Yellow
$account = az account show --query "user.name" -o tsv 2>$null
if (-not $account) {
    Write-Host "Please login to Azure first: az login" -ForegroundColor Red
    exit 1
}
Write-Host "Logged in as: $account" -ForegroundColor Green

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

# Check if storage account exists
Write-Host "Checking if storage account exists..." -ForegroundColor Yellow
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

# Build the React application
Write-Host "Building React application..." -ForegroundColor Yellow
Push-Location "frontend"

# Install dependencies
Write-Host "Installing npm dependencies..." -ForegroundColor Yellow
npm install

# Build for production
Write-Host "Building production bundle..." -ForegroundColor Yellow
$env:GENERATE_SOURCEMAP = "false"  # Reduce bundle size
npm run build

if (-not (Test-Path "./build")) {
    Write-Host "Build folder not found. Build may have failed." -ForegroundColor Red
    Pop-Location
    exit 1
}

Pop-Location

# Clear existing files in $web container
Write-Host "Clearing existing files in storage container..." -ForegroundColor Yellow
az storage blob delete-batch `
    --source $containerName `
    --account-name $storageAccountName `
    --account-key $storageKey

# Upload the built files
Write-Host "Uploading files to Azure Storage..." -ForegroundColor Yellow
az storage blob upload-batch `
    --destination $containerName `
    --source "./frontend/build" `
    --account-name $storageAccountName `
    --account-key $storageKey `
    --overwrite

# Set content types for proper MIME types
Write-Host "Setting content types..." -ForegroundColor Yellow

# Set content type for CSS files
az storage blob update `
    --container-name $containerName `
    --name "static/css/*.css" `
    --content-type "text/css" `
    --account-name $storageAccountName `
    --account-key $storageKey 2>$null

# Set content type for JS files  
az storage blob update `
    --container-name $containerName `
    --name "static/js/*.js" `
    --content-type "application/javascript" `
    --account-name $storageAccountName `
    --account-key $storageKey 2>$null

# Get the static website URL
$staticWebsiteUrl = az storage account show --name $storageAccountName --resource-group $resourceGroup --query "primaryEndpoints.web" -o tsv

Write-Host "Frontend deployed successfully!" -ForegroundColor Green
Write-Host "Static Website URL: $staticWebsiteUrl" -ForegroundColor Cyan

# Check if we need to update the frontend API endpoint
Write-Host "" -ForegroundColor Yellow
Write-Host "IMPORTANT: Update your frontend API endpoint" -ForegroundColor Yellow
Write-Host "You may need to update the API base URL in your React app to point to the deployed backend" -ForegroundColor Yellow
Write-Host "Check: frontend/src/services/energyApiService.js" -ForegroundColor Cyan

Write-Host "Frontend deployment completed!" -ForegroundColor Green
