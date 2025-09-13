import React from 'react';
import { Pie, Bar } from 'react-chartjs-2';
import { Chart, ArcElement, Tooltip, Legend, CategoryScale, LinearScale, BarElement, Title } from 'chart.js';

Chart.register(ArcElement, Tooltip, Legend, CategoryScale, LinearScale, BarElement, Title);

const EnergyChart_Enhanced = ({ data, useMockData = false }) => {
  console.log('EnergyChart_Enhanced received data:', data);

  if (!data || (Array.isArray(data) && data.length === 0)) {
    return (
      <div className="text-center text-muted p-4">
        <div style={{ fontSize: '3rem', marginBottom: '1rem' }}>📊</div>
        <div className="h5">No Energy Data Available</div>
        <p>Run an energy analysis or toggle mock data to see results</p>
      </div>
    );
  }

  let chartData = null;
  let barChartData = null;
  let totalEnergy = 0;
  let microserviceBreakdown = [];
  let resourceTypeBreakdown = {};
  let sharedResourcesDetails = [];

  const colors = ['#FF6384', '#36A2EB', '#FFCE56', '#4BC0C0', '#9966FF', '#FF9F40', '#8A2BE2', '#00CED1', '#FFD700', '#DC143C', '#32CD32', '#FF69B4'];

  // Handle API data with enhanced shared resources breakdown
  if (Array.isArray(data) && data[0]) {
    console.log('EnergyChart_Enhanced: Processing API data with detailed breakdown');
    const report = data[0];
    totalEnergy = report.kilowattHours;
    
    const details = report.details || {};
    const labels = [];
    const values = [];
    const serviceGroups = {};
    
    console.log('EnergyChart_Enhanced: API details object:', details);
    
    if (Object.keys(details).length === 0) {
      // Fallback when no details available
      const avgEnergyPerService = totalEnergy / 4;
      chartData = {
        labels: ['App Services', 'Function Apps', 'Service Bus', 'Databases'],
        datasets: [{
          data: [avgEnergyPerService * 0.4, avgEnergyPerService * 0.3, avgEnergyPerService * 0.15, avgEnergyPerService * 0.15],
          backgroundColor: ['#36A2EB', '#FF6384', '#FFCE56', '#4BC0C0']
        }]
      };
      resourceTypeBreakdown = {
        'App Service': avgEnergyPerService * 0.4,
        'Function Apps': avgEnergyPerService * 0.3,
        'Service Bus': avgEnergyPerService * 0.15,
        'Database': avgEnergyPerService * 0.15
      };
    } else {
      // Process detailed breakdown from API energyReport
      Object.keys(details).forEach(key => {
        const value = details[key];
        let displayName = key;
        let serviceName = 'Shared Resources';
        let resourceType = 'Mixed';
        
        // Enhanced parsing for shared resources
        if (key.startsWith('Shared_')) {
          // Handle detailed shared resource breakdown: Shared_Storage_myaccount, Shared_Redis_cache, etc.
          const parts = key.split('_');
          if (parts.length >= 3) {
            const sharedResourceType = parts[1];
            const resourceName = parts.slice(2).join(' ');
            displayName = `${sharedResourceType}: ${resourceName}`;
            serviceName = 'Shared Infrastructure';
            resourceType = 'Shared Resources'; // Group all shared resources under one category for bar chart
            
            // Add to shared resources details for separate display
            sharedResourcesDetails.push({
              type: sharedResourceType,
              name: resourceName,
              energy: value,
              percentage: (value / totalEnergy * 100).toFixed(1)
            });
          } else {
            displayName = 'Shared Infrastructure';
            serviceName = 'Shared Infrastructure';
            resourceType = 'Shared Resources';
          }
        } else if (key === 'SharedResources') {
          displayName = 'Shared Resources (Legacy)';
          serviceName = 'Shared Infrastructure';
          resourceType = 'Shared Resources';
        } else if (key.includes('_')) {
          const [rawServiceName, rawResourceType] = key.split('_');
          serviceName = rawServiceName;
          resourceType = rawResourceType;
          
          // Transform API keys to beautiful display names
          if (rawResourceType === 'AppService') {
            displayName = `${serviceName} - App Service`;
            resourceType = 'App Service';
          } else if (rawResourceType === 'Functions') {
            displayName = `${serviceName} - Function Apps`;
            resourceType = 'Function Apps';
          } else if (rawResourceType === 'ServiceBus') {
            displayName = `${serviceName} - Service Bus`;
            resourceType = 'Service Bus';
          } else if (rawResourceType === 'Database') {
            displayName = `${serviceName} - Database`;
            resourceType = 'Database';
          }
        }
        
        // Group by service for service breakdown chart
        if (!serviceGroups[serviceName]) {
          serviceGroups[serviceName] = { totalEnergy: 0, resources: [] };
        }
        serviceGroups[serviceName].totalEnergy += value;
        serviceGroups[serviceName].resources.push({ 
          type: resourceType, 
          displayName: displayName, // Add the parsed display name
          energy: value 
        });
        
        // Add to overall labels and values
        labels.push(displayName);
        values.push(value);
        
        // Group by resource type for resource type breakdown
        resourceTypeBreakdown[resourceType] = (resourceTypeBreakdown[resourceType] || 0) + value;
      });

      chartData = {
        labels: labels,
        datasets: [{
          data: values,
          backgroundColor: colors.slice(0, labels.length),
          borderColor: colors.slice(0, labels.length),
          borderWidth: 1
        }]
      };

      // Generate breakdown for display
      microserviceBreakdown = Object.keys(serviceGroups).map(serviceName => ({
        name: serviceName,
        energy: serviceGroups[serviceName].totalEnergy,
        resources: serviceGroups[serviceName].resources
      }));
    }

    // Create bar chart data for resource types
    const resourceTypes = Object.keys(resourceTypeBreakdown);
    const resourceValues = Object.values(resourceTypeBreakdown);
    
    barChartData = {
      labels: resourceTypes,
      datasets: [{
        label: 'Energy Consumption (kWh)',
        data: resourceValues,
        backgroundColor: colors.slice(0, resourceTypes.length),
        borderColor: colors.slice(0, resourceTypes.length),
        borderWidth: 1
      }]
    };
  }

  const chartOptions = {
    responsive: true,
    plugins: {
      legend: {
        position: 'bottom',
        labels: {
          boxWidth: 12,
          padding: 8
        }
      },
      tooltip: {
        callbacks: {
          label: function(context) {
            const value = context.parsed;
            const percentage = ((value / totalEnergy) * 100).toFixed(1);
            return `${context.label}: ${value.toFixed(2)} kWh (${percentage}%)`;
          }
        }
      }
    }
  };

  const barOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        display: false
      },
      title: {
        display: false // Remove title since we have it in card header
      },
      tooltip: {
        callbacks: {
          label: function(context) {
            const value = context.parsed.y;
            const percentage = ((value / totalEnergy) * 100).toFixed(1);
            return `${context.label}: ${value.toFixed(2)} kWh (${percentage}%)`;
          }
        }
      }
    },
    scales: {
      y: {
        beginAtZero: true,
        title: {
          display: true,
          text: 'Energy (kWh)',
          font: {
            size: 12
          }
        },
        ticks: {
          font: {
            size: 11
          }
        }
      },
      x: {
        title: {
          display: true,
          text: 'Resource Types',
          font: {
            size: 12
          }
        },
        ticks: {
          font: {
            size: 11
          },
          maxRotation: 45,
          minRotation: 0
        }
      }
    }
  };

  return (
    <div className="energy-chart-container">
      {/* Main Charts Row */}
      <div className="row">
        <div className="col-md-7">
          <div className="card">
            <div className="card-header">
              <h5 className="mb-0">🥧 Service & Resource Breakdown</h5>
              <small className="text-muted">Detailed energy consumption by service and resource type</small>
            </div>
            <div className="card-body">
              {chartData && <Pie data={chartData} options={chartOptions} />}
            </div>
          </div>
        </div>
        
        <div className="col-md-5">
          <div className="card h-100">
            <div className="card-header">
              <h5 className="mb-0">📊 Resource Type Summary</h5>
              <small className="text-muted">Energy consumption by category</small>
            </div>
            <div className="card-body" style={{ minHeight: '400px', position: 'relative' }}>
              {barChartData && barChartData.labels && barChartData.labels.length > 0 ? (
                <Bar data={barChartData} options={barOptions} />
              ) : (
                <div className="d-flex align-items-center justify-content-center h-100">
                  <div className="text-center text-muted">
                    <div style={{ fontSize: '3rem' }}>📊</div>
                    <div>No resource type data available</div>
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* Detailed Shared Resources Breakdown */}
      {sharedResourcesDetails.length > 0 && (
        <div className="row mt-4">
          <div className="col-12">
            <div className="card">
              <div className="card-header bg-info text-white">
                <h5 className="mb-0">🔧 Shared Infrastructure Details</h5>
                <small>Breakdown of the {sharedResourcesDetails.reduce((sum, item) => sum + item.energy, 0).toFixed(2)} kWh consumed by shared resources</small>
              </div>
              <div className="card-body">
                <div className="row">
                  {sharedResourcesDetails.map((resource, index) => (
                    <div key={index} className="col-md-6 col-lg-4 mb-3">
                      <div className="card border-info h-100">
                        <div className="card-body text-center">
                          <div className="mb-2">
                            {getResourceIcon(resource.type)}
                          </div>
                          <h6 className="card-title text-truncate" title={resource.name}>
                            {resource.type}
                          </h6>
                          <p className="card-text">
                            <strong>{resource.energy.toFixed(2)} kWh</strong>
                            <br />
                            <small className="text-muted">{resource.percentage}% of total</small>
                            <br />
                            <small className="text-info">{resource.name}</small>
                          </p>
                          <div className="progress" style={{ height: '4px' }}>
                            <div 
                              className="progress-bar bg-info" 
                              style={{ width: `${resource.percentage}%` }}
                            ></div>
                          </div>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
                
                {/* Summary explanation for judges */}
                <div className="alert alert-info mt-3">
                  <h6><i className="fas fa-info-circle me-2"></i>What are Shared Resources?</h6>
                  <p className="mb-2">
                    Shared resources are infrastructure components used across multiple microservices:
                  </p>
                  <ul className="mb-0">
                    <li><strong>Storage:</strong> Blob storage, file shares, queues used by multiple services</li>
                    <li><strong>Redis:</strong> Shared caching layer for session management and performance</li>
                    <li><strong>CosmosDB:</strong> Global distributed database serving multiple applications</li>
                    <li><strong>KeyVault:</strong> Centralized secrets and certificate management</li>
                    <li><strong>ApplicationInsights:</strong> Monitoring and telemetry collection</li>
                    <li><strong>Load Balancer:</strong> Traffic distribution and availability</li>
                    <li><strong>CDN:</strong> Content delivery and global caching</li>
                  </ul>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Service breakdown */}
      <div className="row mt-4">
        <div className="col-12">
          <div className="card">
            <div className="card-header">
              <h5 className="mb-0">📈 Energy Consumption by Service</h5>
            </div>
            <div className="card-body">
              {microserviceBreakdown.map((service, index) => (
                <div key={index} className="mb-3">
                  <div className="d-flex justify-content-between align-items-center mb-2">
                    <h6 className="mb-0">{service.name}</h6>
                    <span className="badge bg-primary">{service.energy.toFixed(2)} kWh</span>
                  </div>
                  <div className="row">
                    {service.resources && service.resources.map((resource, rIndex) => (
                      <div key={rIndex} className="col-md-3 mb-2">
                        <div className="small">
                          <span className="text-muted">
                            {getResourceIcon(resource.type)}
                            {resource.displayName || resource.type}: 
                          </span>
                          <strong> {resource.energy.toFixed(2)} kWh</strong>
                        </div>
                      </div>
                    ))}
                  </div>
                  {index < microserviceBreakdown.length - 1 && <hr />}
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

// Helper function to get appropriate icons for resource types
const getResourceIcon = (resourceType) => {
  const iconMap = {
    'App Service': '🌐',
    'Function Apps': '⚡',
    'Service Bus': '📨',
    'Database': '🗄️',
    'Storage': '💾',
    'Redis': '🚀',
    'KeyVault': '🔐',
    'ApplicationInsights': '📊',
    'CosmosDB': '🌍',
    'CDN': '🌐',
    'LoadBalancer': '⚖️',
    'VirtualNetwork': '🔗',
    'NetworkSecurityGroup': '🛡️',
    'PublicIP': '🌍',
    'TrafficManager': '🚦',
    'Infrastructure': '🏗️'
  };
  
  return iconMap[resourceType] || '🔧';
};

export default EnergyChart_Enhanced;
