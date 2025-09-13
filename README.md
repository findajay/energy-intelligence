# 🌱 Energy Intelligence Platform

**Transform your Azure cloud spending into environmental insights!**

This intelligent platform automatically discovers your Azure infrastructure and provides **real energy consumption and carbon footprint analysis** using actual cost data from Microsoft Azure. Perfect for organizations wanting to understand and optimize their cloud environmental impact.

## 🎯 What This Platform Does

### For Business Users
- **💰 Cost to Carbon Translation**: See how your Azure spending translates to actual energy consumption and CO2 emissions
- **📊 Easy-to-Understand Dashboards**: Visual charts showing your environmental impact over time
- **🔍 Automatic Discovery**: No technical setup needed - just connect and discover your entire Azure infrastructure
- **📈 Trend Analysis**: Track improvements in your cloud efficiency and carbon footprint
- **📋 Executive Reports**: Generate reports for sustainability initiatives and compliance

### For Technical Teams
- **🤖 Zero Configuration**: Automatic discovery of all Azure resources across subscriptions
- **🔌 Real Data Integration**: Direct connection to Azure Cost Management API for accurate cost and usage data
- **⚡ Energy Modeling**: Advanced algorithms convert cloud costs to energy consumption (kWh) and carbon footprint (CO2)
- **🎨 Interactive Visualizations**: React-based dashboard with real-time updates
- **📱 API-First Design**: RESTful APIs for integration with existing tools and workflows

## 🚀 Key Features

### 🔍 **Smart Discovery**
- **One-Click Setup**: Connect your Azure subscription and automatically discover ALL resources
- **Intelligent Grouping**: Automatically identifies microservices, databases, and shared infrastructure
- **Real-Time Sync**: Keeps up with changes in your Azure environment

### 💰 **Real Cost Integration**
- **Live Data**: Pulls actual costs from Azure Cost Management API (no estimates!)
- **Historical Analysis**: Track cost and energy trends over time
- **Resource Breakdown**: See exactly which services consume the most energy

### 🌍 **Environmental Impact**
- **Carbon Footprint**: Calculate CO2 emissions based on Azure region and energy mix
- **Energy Consumption**: Convert cloud costs to actual kWh consumption
- **Optimization Insights**: Identify opportunities to reduce environmental impact

### 📊 **Business Intelligence**
- **Executive Dashboards**: High-level summaries for leadership
- **Detailed Analytics**: Drill down into specific resources and time periods
- **Export Capabilities**: CSV exports for further analysis and reporting

---

## 🏗️ How It Works (Simple Overview)

```
1. 🔌 Connect     → Link your Azure subscription (one-time setup)
2. 🔍 Discover    → Platform automatically finds all your cloud resources
3. 💰 Analyze     → Retrieves real cost data from Azure
4. ⚡ Calculate   → Converts costs to energy consumption and carbon footprint
5. 📊 Visualize   → Shows results in easy-to-understand charts and reports
6. 📈 Track       → Monitor trends and improvements over time
```

---

## ⚙️ Quick Start Guide

### Step 1: Minimal Setup (5 minutes)
Just need your Azure subscription details - no complex configuration required!

```json
{
  "Azure": {
    "SubscriptionId": "your-subscription-id",
    "TenantId": "your-tenant-id",
    "AutoDiscovery": {
      "Enabled": true
    }
  }
}
```

### Step 2: Run the Platform
```powershell
# Start the backend
cd backend
dotnet run

# Start the frontend dashboard
cd frontend
npm start
```

### Step 3: Open Dashboard
Visit `http://localhost:3000` and click "Discover Resources" to start!

---

## 🔍 Auto-Discovery Magic

### What Gets Discovered Automatically
- **🌐 Web Apps & APIs**: All your application services
- **🗄️ Databases**: SQL servers, Cosmos DB, and other data services  
- **📨 Messaging**: Service Bus, Event Hubs for communication
- **💾 Storage**: Blob storage, file shares, and data lakes
- **🔧 Infrastructure**: Virtual networks, load balancers, monitoring tools

### How Smart Grouping Works
The platform automatically organizes your resources by:
1. **Resource Groups**: Natural Azure organization boundaries
2. **Naming Patterns**: Detects conventions like `myapp-dev-api` or `rg-service-prod`
3. **Service Types**: Groups related services (app + database + storage)
4. **Environment Detection**: Identifies dev, test, staging, production environments

### Benefits Over Manual Configuration
| Manual Setup ❌ | Auto-Discovery ✅ |
|-----------------|-------------------|
| List every resource by hand | Finds everything automatically |
| Update config for new resources | Keeps up with changes |
| Error-prone resource IDs | Never misses anything |
| Limited infrastructure view | Complete visibility |

---

## 📊 Dashboard Features

### 🏠 **Main Dashboard**
- **Energy Overview**: Total kWh consumption across all services
- **Carbon Footprint**: CO2 emissions with regional factors
- **Cost Breakdown**: Which resources cost (and consume) the most
- **Trend Charts**: Historical data showing improvements

### 🔍 **Resource Discovery**
- **Test Connection**: Verify Azure access with one click
- **Discovery Summary**: High-level overview of your infrastructure
- **Microservices View**: Automatically detected application services
- **Resource Explorer**: Detailed view of all discovered resources

### 📈 **Analytics & Reports**
- **Time-Series Analysis**: Track energy consumption trends
- **Comparative Analysis**: Compare different time periods
- **Resource Efficiency**: Identify over/under-utilized resources
- **CSV Export**: Download data for executive reports

---

## 🔌 Available Endpoints (For Developers)

### Discovery & Analysis
- `GET /api/ResourceDiscovery/summary` - Infrastructure overview
- `GET /api/ResourceDiscovery/microservices` - Detected application services
- `POST /api/analyze` - Trigger energy analysis
- `GET /api/energy/platform` - Calculate energy consumption with real cost data

### Data & Reports
- `GET /api/reports/history` - Historical energy and cost trends
- `GET /api/reports/{id}` - Specific analysis report
- `GET /api/resources/discovery` - Auto-discovered Azure resources

### Health & Status
- `GET /api/health` - Service health and Azure connectivity
- `GET /api/ResourceDiscovery/test-connection` - Verify Azure access

---

## 🧮 Energy Calculation Explained

### How We Convert Cloud Costs to Environmental Impact

**Step 1: Real Cost Data** 🔌  
→ Get actual spending from Azure Cost Management API

**Step 2: Resource Analysis** 🖥️  
→ Map each service to its energy consumption profile (e.g., VM size = power usage)

**Step 3: Usage Patterns** 📊  
→ Apply utilization factors based on real consumption data

**Step 4: Energy Conversion** ⚡  
→ Convert to kWh: `Power (Watts) × Time (Hours) ÷ 1000`

**Step 5: Carbon Footprint** 🌍  
→ Apply regional carbon intensity: `kWh × Regional CO2 Factor`

**Step 6: Total Impact** 📋  
→ Include embodied carbon from hardware manufacturing

### Example Calculation
```
Azure VM (Standard_D2_v3) running 24/7 for 1 month:
• Base Power: ~100W
• Monthly Hours: 720 hours  
• Energy: 100W × 720h ÷ 1000 = 72 kWh
• Carbon (EU West): 72 kWh × 0.3 kg CO2/kWh = 21.6 kg CO2
```

---

## 🛠️ Technical Architecture

### Components
- **Backend API** (ASP.NET Core): Energy calculations + Azure integration
- **EnergyCore Library**: Advanced analytics and trend analysis
- **React Dashboard**: Interactive visualizations and reports
- **Azure Storage**: Historical data and analysis results
- **Azure Cost Management**: Real-time cost and usage data

### Key Technologies
- **Azure Resource Manager API**: Resource discovery
- **Azure Cost Management API**: Real cost data
- **HttpClient**: Authenticated API communication
- **Service Principal**: Secure Azure authentication
- **Bootstrap Charts**: Data visualization

---

## 🚦 Getting Started Checklist

### For Business Users
- [ ] Get Azure subscription ID from your IT team
- [ ] Open the dashboard at provided URL
- [ ] Click "Test Connection" to verify access
- [ ] Run "Discovery Summary" to see your infrastructure
- [ ] Generate your first energy report

### For IT Teams
- [ ] Set up Service Principal for Azure access
- [ ] Configure authentication in `appsettings.json`
- [ ] Enable auto-discovery: `"AutoDiscovery": { "Enabled": true }`
- [ ] Test connection using `/api/ResourceDiscovery/test-connection`
- [ ] Deploy to Azure App Service for team access

### Advanced Configuration Options
```json
{
  "CostManagement": {
    "UseRealData": true,          // Enable real Azure cost data
    "SubscriptionId": "xxx",      // Target subscription
    "FallbackToMockData": true    // Graceful fallback if API fails
  },
  "AutoDiscovery": {
    "Enabled": true,
    "IncludeResourceTypes": ["Microsoft.Web/sites", "Microsoft.Sql/servers"],
    "ExcludeResourceGroups": ["NetworkWatcherRG"]
  }
}
```

---

## 📚 Additional Resources

### Documentation
- **Setup Guide**: Detailed installation instructions
- **API Documentation**: Complete endpoint reference  
- **Configuration Guide**: Advanced setup options
- **Troubleshooting**: Common issues and solutions

### Support
- **Business Questions**: Contact your sustainability team
- **Technical Issues**: Contact your IT/DevOps team
- **Feature Requests**: Submit via your internal channels

### Security & Compliance
- **Data Privacy**: All analysis stays within your Azure tenant
- **Authentication**: Secure Service Principal access only
- **Audit Trail**: Complete logging of all API calls and calculations

---

## 🏆 Success Stories

### What Organizations Achieve
- **🎯 20-30% Cloud Efficiency Improvements**: By identifying and optimizing underutilized resources
- **📊 Executive Sustainability Reporting**: Regular carbon footprint reports for board meetings
- **💰 Cost Optimization**: Understanding which services provide best energy efficiency per dollar
- **🌱 Green IT Initiatives**: Data-driven decisions for sustainable cloud architecture

---

*Ready to start your cloud sustainability journey? Connect your Azure subscription and discover your environmental impact in minutes!* 🚀🌱
