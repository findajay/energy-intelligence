import React, { useState, useEffect } from 'react';
import { energyApiService } from '../services/energyApiService';

const AnalysisForm = ({ onSubmit }) => {
  const [microservices, setMicroservices] = useState([]);
  const [sharedResources, setSharedResources] = useState([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);
  const [startDate, setStartDate] = useState(new Date().toISOString().split('T')[0]);
  const [endDate, setEndDate] = useState(new Date().toISOString().split('T')[0]);

  useEffect(() => {
    console.log('AnalysisForm: useEffect triggered, loading existing resources...');
    const loadResources = async () => {
      try {
        setLoading(true);
        setError(null);

        console.log('AnalysisForm: Fetching microservices...');
        const microservicesData = await energyApiService.getMicroservices();
        console.log('AnalysisForm: Raw microservices response:', microservicesData);
        
        if (microservicesData && microservicesData.length > 0) {
          setMicroservices(microservicesData);
          console.log('AnalysisForm: Set microservices count:', microservicesData.length);
        }

        console.log('AnalysisForm: Fetching shared resources...');
        const sharedResourcesData = await energyApiService.getSharedResources();
        console.log('AnalysisForm: Raw shared resources response:', sharedResourcesData);
        
        if (sharedResourcesData && sharedResourcesData.length > 0) {
          setSharedResources(sharedResourcesData);
          console.log('AnalysisForm: Set shared resources count:', sharedResourcesData.length);
        }
      } catch (error) {
        console.error('AnalysisForm: Error loading resources:', error);
        setError('Failed to load existing resources: ' + error.message);
      } finally {
        setLoading(false);
        console.log('AnalysisForm: Loading complete');
      }
    };

    loadResources();
  }, []);

  const handleSubmit = async (e) => {
    e.preventDefault();
    e.stopPropagation();
    
    // Prevent duplicate submissions
    if (submitting) {
      console.log('AnalysisForm: Submission already in progress, ignoring duplicate');
      return;
    }
    
    setSubmitting(true);
    const requestId = `analysis_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
    console.log(`AnalysisForm: [${requestId}] Starting analysis submission`);
    
    // Create analysis request - if no microservices, create a generic subscription analysis
    const analysisRequest = microservices.length > 0 ? {
      Microservices: microservices
        .filter(ms => {
          // Include microservices that have at least one App Service
          const hasAppService = (ms.appServices || ms.AppServices || []).length > 0;
          if (!hasAppService) {
            console.log(`AnalysisForm: Filtering out ${ms.name} - no App Service found`);
          }
          return hasAppService;
        })
        .map(ms => ({
          MicroserviceName: ms.name || ms.Name, // API returns lowercase 'name' 
          AppServiceResourceId: ms.appServices?.[0]?.id || ms.AppServices?.[0]?.Id || "",
          FunctionAppResourceIds: (ms.functionApps || ms.FunctionApps || []).map(fa => fa.id || fa.Id),
          ServiceBusResourceIds: (ms.serviceBus || ms.ServiceBus || []).map(sb => sb.id || sb.Id),
          DatabaseResourceIds: (ms.databases || ms.Databases || []).map(db => db.id || db.Id)
        })),
      SharedResourceIds: sharedResources.map(r => r.id || r.Id),
      StartTime: new Date(startDate),
      EndTime: new Date(endDate)
      // Note: Removed manual utilization - will use actual resource metrics
    } : {
      // Direct Azure subscription analysis when no microservices found
      Microservices: [{
        MicroserviceName: "Azure-Subscription-Resources",
        AppServiceResourceId: "", // Will be auto-discovered by backend
        FunctionAppResourceIds: [],
        ServiceBusResourceIds: [],
        DatabaseResourceIds: []
      }],
      SharedResourceIds: sharedResources.map(r => r.id || r.Id),
      StartTime: new Date(startDate),
      EndTime: new Date(endDate),
      AnalyzeAllResources: true // Flag to tell backend to analyze all subscription resources
    };

    console.log(`AnalysisForm: [${requestId}] Total discovered microservices:`, microservices.length);
    console.log(`AnalysisForm: [${requestId}] Microservices with App Services:`, analysisRequest.Microservices.length);
    console.log(`AnalysisForm: [${requestId}] Sample microservice data:`, microservices[0]);
    console.log(`AnalysisForm: [${requestId}] Sample analysis request microservice:`, analysisRequest.Microservices[0]);

    try {
      console.log(`AnalysisForm: [${requestId}] Starting API call with request:`, analysisRequest);
      const response = await energyApiService.analyzePlatformEnergy(analysisRequest);
      console.log(`AnalysisForm: [${requestId}] Received API response:`, response);
      console.log(`AnalysisForm: [${requestId}] Response structure check - energyReport exists:`, !!response?.energyReport);
      console.log(`AnalysisForm: [${requestId}] Response structure check - trends exists:`, !!response?.trends);
      console.log(`AnalysisForm: [${requestId}] Response structure check - optimizationRecommendations exists:`, !!response?.optimizationRecommendations);
      
      console.log(`AnalysisForm: [${requestId}] Calling onSubmit with response`);
      onSubmit(response);
      console.log(`AnalysisForm: [${requestId}] onSubmit completed`);
    } catch (error) {
      console.error(`AnalysisForm: [${requestId}] Error submitting analysis:`, error);
      setError('Failed to submit analysis: ' + error.message);
    } finally {
      console.log(`AnalysisForm: [${requestId}] Setting submitting to false`);
      setSubmitting(false);
    }
  };

  return (
    <div className="card">
      <div className="card-body">
        <h5 className="card-title">Analysis Parameters</h5>
        {loading && <div className="alert alert-info">Loading resources...</div>}
        {error && <div className="alert alert-danger">{error}</div>}
        {!loading && !error && microservices.length > 0 && (
          <div className="alert alert-success">
            <div className="d-flex justify-content-between align-items-start">
              <div>
                <strong>✅ Ready for Enhanced Analysis</strong>
                <div className="mt-2">
                  Found {microservices.length} microservices and {sharedResources.length} shared resources
                </div>
              </div>
            </div>
          </div>
        )}
        {!loading && !error && microservices.length === 0 && (
          <div className="alert alert-info">
            <div className="d-flex justify-content-between align-items-start">
              <div>
                <strong>📊 Direct Azure Resources Analysis</strong>
                <div className="mt-2">
                  No microservices detected. You can still analyze your Azure subscription resources directly.
                  <br />
                  <small className="text-muted">This will analyze all Azure resources in your subscription for energy consumption.</small>
                </div>
              </div>
            </div>
          </div>
        )}
        <form onSubmit={handleSubmit}>
          <div className="mb-3">
            <label htmlFor="startDate" className="form-label">Start Date</label>
            <input
              type="date"
              className="form-control"
              id="startDate"
              value={startDate}
              onChange={(e) => setStartDate(e.target.value)}
            />
          </div>
          <div className="mb-3">
            <label htmlFor="endDate" className="form-label">End Date</label>
            <input
              type="date"
              className="form-control"
              id="endDate"
              value={endDate}
              onChange={(e) => setEndDate(e.target.value)}
            />
          </div>
          <button type="submit" className="btn btn-primary" disabled={loading || submitting}>
            {submitting ? 'Analyzing...' : loading ? 'Loading...' : microservices.length === 0 ? 'Analyze Azure Subscription Resources' : `Analyze ${microservices.length} Microservices`}
          </button>
        </form>
      </div>
    </div>
  );
};

export default AnalysisForm;
