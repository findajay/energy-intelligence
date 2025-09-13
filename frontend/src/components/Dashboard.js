import React, { useState, useEffect } from 'react';
import EnergyChartEnhanced from './EnergyChart_Enhanced';
import TrendsChart from './TrendsChart';
import AnalysisForm from './AnalysisForm';
import OptimizationRecommendations from './OptimizationRecommendations';
import { energyApiService } from '../services/energyApiService';
import { mockPlatformData } from '../mockData';

const Dashboard = () => {
  const [energyData, setEnergyData] = useState([]);
  const [trends, setTrends] = useState(null);
  const [optimizationRecommendations, setOptimizationRecommendations] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [useMockData, setUseMockData] = useState(false); // Start with real API data to test detailed breakdown
  const [apiStatus, setApiStatus] = useState({ isAvailable: true, lastChecked: null });
  const [hasRealData, setHasRealData] = useState(false); // Track if we have loaded real analysis data

  const checkApiStatus = async () => {
    try {
      await energyApiService.client.get('/Health');
      setApiStatus({ isAvailable: true, lastChecked: new Date() });
      return true;
    } catch (err) {
      setApiStatus({ isAvailable: false, lastChecked: new Date() });
      return false;
    }
  };

  const fetchReportHistory = async (startDate, endDate) => {
    // This function now only handles real API calls
    // Mock data is handled entirely by the useEffect
    
    try {
      // Check API status first
      const isApiAvailable = await checkApiStatus();
      if (!isApiAvailable) {
        setError('API is not available. Please check your connection or use mock data.');
        return;
      }

      if (startDate && endDate) {
        const historyData = await energyApiService.getEnergyHistory(startDate, endDate);
        if (historyData && historyData.length > 0) {
          setTrends(historyData);
        }
      }
      // Remove automatic platform analysis - only load data when explicitly requested
      
    } catch (error) {
      console.error('Error fetching energy data:', error);
      setError('Failed to fetch energy data. You can use mock data instead.');
    }
  };

  const handleAnalysisResult = (response) => {
    const resultId = `result_${Date.now()}`;
    console.log(`Dashboard: [${resultId}] Received analysis result:`, response);
    
    try {
      // Check if response is valid and has the expected structure
      if (!response) {
        throw new Error('No response received from analysis');
      }
      
      // Handle the response structure directly (not making another API call)
      if (response.energyReport || response.EnergyReport) {
        const energyReport = response.energyReport || response.EnergyReport;
        console.log(`Dashboard: [${resultId}] Setting energy report data:`, energyReport);
        setEnergyData([energyReport]); // Wrap in array for chart component
        
        // Set trends if available (try both case styles)
        const trends = response.trends || response.Trends;
        if (trends) {
          console.log(`Dashboard: [${resultId}] Setting analysis trends data:`, trends);
          setTrends(trends);
        } else {
          console.log(`Dashboard: [${resultId}] No trends in analysis response`);
        }

        // Set optimization recommendations if available (try both case styles)
        const recommendations = response.optimizationRecommendations || response.OptimizationRecommendations;
        if (recommendations) {
          console.log(`Dashboard: [${resultId}] Setting optimization recommendations:`, recommendations);
          setOptimizationRecommendations(recommendations);
        }
        
        setError(null);
        setHasRealData(true); // Mark that we have real data loaded
        console.log(`Dashboard: [${resultId}] Analysis result processed successfully`);
      } else {
        console.error(`Dashboard: [${resultId}] Invalid response structure. Response:`, response);
        throw new Error(`Invalid response structure. Received: ${JSON.stringify(Object.keys(response || {}))}`);
      }
    } catch (err) {
      console.error(`Dashboard: [${resultId}] Error processing analysis result:`, err);
      setError('Error processing analysis result: ' + err.message);
    }
  };

  useEffect(() => {
    // Only check API status on mount, don't fetch data automatically
    if (!useMockData) {
      checkApiStatus();
    }
  }, [useMockData]); // Include useMockData dependency

  // Separate useEffect to handle mock data changes
  useEffect(() => {
    if (useMockData) {
      // Store current real data temporarily
      const realEnergyData = energyData;
      const realTrends = trends;
      const realOptimizations = optimizationRecommendations;
      
      setEnergyData([mockPlatformData]);
      setTrends(mockPlatformData.trends);
      setOptimizationRecommendations(null); // Mock data doesn't have optimization recommendations
      console.log('Dashboard: Setting mock trends data:', mockPlatformData.trends);
      setError(null);
      
      // Store real data for restoration
      window._dashboardRealData = {
        energyData: realEnergyData,
        trends: realTrends,
        optimizationRecommendations: realOptimizations
      };
    } else {
      // Switching back to real data
      if (hasRealData && window._dashboardRealData) {
        // Restore previously loaded real data
        console.log('Dashboard: Restoring previously loaded real data');
        setEnergyData(window._dashboardRealData.energyData);
        setTrends(window._dashboardRealData.trends);
        setOptimizationRecommendations(window._dashboardRealData.optimizationRecommendations);
        // Clean up temporary storage
        delete window._dashboardRealData;
      } else if (!hasRealData) {
        // No real data has been loaded yet, show empty state
        console.log('Dashboard: No real data available, showing empty state');
        setEnergyData([]);
        setTrends(null);
        setOptimizationRecommendations(null);
      }
      // Check API status when switching from mock to real data
      checkApiStatus();
    }
  }, [useMockData]);

  return (
    <>
      {/* Environmental Hero Section */}
      <div className="environmental-hero">
        <div className="floating-leaf">🍃</div>
        <div className="floating-leaf">🌿</div>
        <div className="floating-leaf">🍃</div>
        <div className="floating-leaf">🌱</div>
        
        <div className="container">
          <div className="hero-content text-center">
            <p className="hero-subtitle">
              Transforming mobility infrastructure through intelligent energy optimization
            </p>
            
            {/* Global Impact Statistics */}
            <div className="row stats-container">
              <div className="col-md-3 col-sm-6">
                <div className="stat-card">
                  <span className="stat-number">4%</span>
                  <div className="stat-label">of World's Energy</div>
                  <div className="stat-description">Used by data centers & digital infrastructure</div>
                </div>
              </div>
              <div className="col-md-3 col-sm-6">
                <div className="stat-card">
                  <span className="stat-number">30%</span>
                  <div className="stat-label">Energy Savings</div>
                  <div className="stat-description">Achievable through smart optimization</div>
                </div>
              </div>
              <div className="col-md-3 col-sm-6">
                <div className="stat-card">
                  <span className="stat-number">
                    {/* Show actual grid factor if available, otherwise default */}
                    {energyData && energyData.length > 0 && energyData[0]?.carbonKg && energyData[0]?.kilowattHours ? 
                      `${(energyData[0].carbonKg / energyData[0].kilowattHours).toFixed(2)}kg` : 
                      '0.24kg'}
                  </span>
                  <div className="stat-label">CO₂ per kWh</div>
                  <div className="stat-description">
                    {energyData && energyData.length > 0 && energyData[0]?.carbonKg && energyData[0]?.kilowattHours ? 
                      'Current region grid intensity' : 
                      'West Europe default grid intensity'}
                  </div>
                </div>
              </div>
              <div className="col-md-3 col-sm-6">
                <div className="stat-card">
                  <span className="stat-number">Our Goal</span>
                  <div className="stat-label">24/7 Live Monitoring</div>
                  <div className="stat-description">Real-time energy consumption tracking</div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div className="container mt-4">
        {loading && (
          <div className="position-fixed top-0 start-0 w-100 h-100 d-flex align-items-center justify-content-center" 
               style={{backgroundColor: 'rgba(255,255,255,0.8)', zIndex: 1050}}>
            <div className="text-center">
              <div className="spinner-border text-primary" role="status">
                <span className="visually-hidden">Loading...</span>
              </div>
              <div className="mt-2">Analyzing energy consumption...</div>
            </div>
          </div>
        )}
        
        {/* Current Energy Summary - Only show when there's actual data */}
        {(useMockData || (!loading && energyData && energyData.length > 0 && energyData[0] && 
         (energyData[0].kilowattHours > 0 || energyData[0].totalEnergyConsumption > 0))) && (
          <div className="row mb-4">
            <div className="col-md-6">
              <div className="card border-0 shadow-sm h-100" style={{background: 'linear-gradient(135deg, #e8f5e8 0%, #f0f9f0 100%)'}}>
                <div className="card-body text-center">
                  <div style={{fontSize: '3rem', color: '#4a7c59'}}>⚡</div>
                  <h3 className="text-success mb-2">
                    {/* Handle both mock data (totalEnergyConsumption) and real API data (kilowattHours) */}
                    {energyData[0]?.totalEnergyConsumption ? 
                      energyData[0].totalEnergyConsumption.toFixed(2) : 
                      energyData[0]?.kilowattHours ? 
                      energyData[0].kilowattHours.toFixed(2) : '0.00'} kWh
                  </h3>
                  <p className="text-muted mb-0">Total Energy Consumption</p>
                  <small className="text-success">
                    Calculated from {(() => {
                      if (energyData[0]?.microservices?.length) {
                        return energyData[0].microservices.length;
                      } else if (energyData[0]?.microserviceReports) {
                        return Object.keys(energyData[0].microserviceReports).length;
                      } else if (energyData[0]?.details) {
                        // Count unique microservices from details keys (e.g., "PaymentService_AppService" -> "PaymentService")
                        const microserviceNames = new Set();
                        Object.keys(energyData[0].details).forEach(key => {
                          if (!key.startsWith('Shared_')) {
                            const serviceName = key.split('_')[0];
                            microserviceNames.add(serviceName);
                          }
                        });
                        return microserviceNames.size;
                      }
                      return 0;
                    })()} active microservices
                  </small>
                </div>
              </div>
            </div>
            <div className="col-md-6">
              <div className="card border-0 shadow-sm h-100" style={{background: 'linear-gradient(135deg, #f0f8ff 0%, #e6f3ff 100%)'}}>
                <div className="card-body text-center">
                  <div style={{fontSize: '3rem', color: '#2196f3'}}>🌍</div>
                  <h3 className="text-info mb-2">
                    {/* Use the proper carbon calculation from backend that includes grid factors */}
                    {energyData[0]?.carbonKg ? 
                      energyData[0].carbonKg.toFixed(2) : 
                      energyData[0]?.totalEnergyConsumption ? 
                      (energyData[0].totalEnergyConsumption * 0.3).toFixed(2) : 
                      energyData[0]?.kilowattHours ? 
                      (energyData[0].kilowattHours * 0.24).toFixed(2) : '0.00'} kg CO₂
                  </h3>
                  <p className="text-muted mb-0">Carbon Footprint</p>
                  <small className="text-info">
                    {/* Show the actual grid intensity factor used */}
                    Based on region-specific grid intensity 
                    {energyData[0]?.carbonKg && energyData[0]?.kilowattHours ? 
                      ` (${(energyData[0].carbonKg / energyData[0].kilowattHours).toFixed(2)} kg CO₂/kWh)` : 
                      ' (estimated)'}
                  </small>
                </div>
              </div>
            </div>
          </div>
        )}
        
        <div className="d-flex justify-content-between align-items-center mb-4">
          <h2 className="text-success">Live Energy Dashboard</h2>
          <div className="d-flex align-items-center gap-3">
            <div className="form-check form-switch">
              <input
                className="form-check-input"
                type="checkbox"
                id="mockDataToggle"
                checked={useMockData}
                onChange={(e) => {
                  console.log('Dashboard: Mock data toggle changed to:', e.target.checked);
                  setUseMockData(e.target.checked);
                  // Let the useEffect handle all data management
                }}
              />
              <label className="form-check-label" htmlFor="mockDataToggle">
                Use Mock Data
              </label>
            </div>
            <div className="d-flex align-items-center">
              <span className="text-muted me-2">API Status:</span>
              <span className={`badge ${apiStatus.isAvailable ? 'bg-success' : 'bg-danger'}`}>
                {apiStatus.isAvailable ? 'Available' : 'Unavailable'}
              </span>
              {apiStatus.lastChecked && (
                <small className="text-muted ms-2">
                  Last checked: {apiStatus.lastChecked.toLocaleTimeString()}
                </small>
              )}
            </div>
          </div>
        </div>
      
      {error && (
        <div className="alert alert-danger" role="alert">
          {error}
          {!useMockData && (
            <button
              className="btn btn-link text-danger"
              onClick={() => {
                setUseMockData(true);
                setEnergyData([mockPlatformData]); // Wrap in array to match expected structure
                setTrends(mockPlatformData.trends);
                setError(null);
              }}
            >
              Switch to Mock Data
            </button>
          )}
        </div>
      )}
      
      <div className="row mb-4">
        <div className="col-md-4">
          <AnalysisForm onSubmit={handleAnalysisResult} />
        </div>
        <div className="col-md-8">
          <div className="card">
            <div className="card-body">
              <h5 className="card-title">Current Energy Consumption</h5>
              <EnergyChartEnhanced data={energyData} useMockData={useMockData} />
            </div>
          </div>
        </div>
      </div>

      {/* Trends Chart */}
      {trends && (
        <div className="row mt-4">
          <div className="col-12">
            <div className="card">
              <div className="card-body">
                <h5 className="card-title">Energy Consumption Trends</h5>
                <TrendsChart platformTrends={trends} />
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Optimization Recommendations */}
      {optimizationRecommendations && (
        <OptimizationRecommendations recommendations={optimizationRecommendations} />
      )}
      
      {/* Energy Calculation Methodology - Moved to bottom as informational content */}
      <div className="methodology-section mt-5">
        <h2 className="methodology-title">How We Calculate Your Energy Footprint</h2>
        <p className="text-muted">Our intelligent system analyzes real Azure microservices to provide accurate energy insights</p>
        
        <div className="methodology-grid">
          <div className="methodology-card">
            <div className="methodology-step">1️⃣</div>
            <h5 className="methodology-step-title">Azure Resource Discovery</h5>
            <p className="methodology-step-description">
              We connect to your Azure subscription and analyze active microservices including payment processors, session managers, and storage systems.
            </p>
          </div>
          
          <div className="methodology-card">
            <div className="methodology-step">2️⃣</div>
            <h5 className="methodology-step-title">Resource Utilization Analysis</h5>
            <p className="methodology-step-description">
              Each service's CPU, memory, and network usage is measured against industry-standard energy consumption baselines.
            </p>
          </div>
          
          <div className="methodology-card">
            <div className="methodology-step">3️⃣</div>
            <h5 className="methodology-step-title">Carbon Footprint Calculation</h5>
            <p className="methodology-step-description">
              Energy consumption is converted to CO₂ emissions using regional grid intensity factors (default: 0.3 kg CO₂/kWh).
            </p>
          </div>
          
          <div className="methodology-card">
            <div className="methodology-step">4️⃣</div>
            <h5 className="methodology-step-title">Trend Analysis & Forecasting</h5>
            <p className="methodology-step-description">
              Historical data creates patterns for daily, weekly, and monthly trends with seasonal variations and optimization recommendations.
            </p>
          </div>
        </div>
      </div>
    </div>
    </>
  );
};

export default Dashboard;
