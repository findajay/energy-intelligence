# 🔐 Security & Configuration Notes

This repository has been sanitized to remove all sensitive information before making it public. 

## ⚠️ What was removed/changed:

### Company References
- All references to "Apcoa" have been replaced with generic "Energy" references
- Company-specific resource names have been generalized

### Sensitive Data Removed
- **Azure Subscription IDs** - Real subscription ID `5f7f3495-660d-40b7-8bc1-6f36a22c7565` replaced with `YOUR_SUBSCRIPTION_ID`
- **Azure Tenant IDs** - Real tenant ID replaced with `YOUR_TENANT_ID`
- **Storage Account Keys** - All connection strings sanitized
- **Service Principal Secrets** - Client secrets replaced with placeholder values
- **KeyVault URIs** - Replaced with example URIs
- **Resource IDs** - All real Azure resource IDs replaced with example values

### Files Affected
- `appsettings.json` - Contains placeholder values (use `appsettings.Example.json` as template)
- `appsettings.Development.json` - Contains placeholder values
- `ui-request-payload.json` - Removed (use `ui-request-payload.example.json` as template)
- Various frontend and backend files with hardcoded resource references

## 🛠️ Configuration Required

To use this application, you'll need to:

1. **Create your own configuration files:**
   ```
   cp appsettings.Example.json appsettings.json
   cp appsettings.Example.json appsettings.Development.json
   cp ui-request-payload.example.json ui-request-payload.json
   ```

2. **Replace placeholders with your actual values:**
   - `YOUR_SUBSCRIPTION_ID`
   - `YOUR_TENANT_ID`
   - `YOUR_CLIENT_ID`
   - `YOUR_CLIENT_SECRET`
   - `YOUR_STORAGE_CONNECTION_STRING`

3. **Update resource IDs in the example files to match your Azure resources**

## 📋 Security Best Practices

- Never commit real Azure credentials to version control
- Use Azure Key Vault for production secrets
- Consider using Managed Identity in Azure environments
- The `.gitignore` file has been updated to prevent accidental commits of sensitive files

## 🔄 Original Repository

This is a sanitized version prepared for public sharing. The original private repository contained real Azure resource configurations and credentials specific to the original deployment environment.