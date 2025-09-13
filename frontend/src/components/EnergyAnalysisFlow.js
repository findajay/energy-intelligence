// Enhanced Energy Analysis Flow for Discovered Services
// This component provides a better flow between resource discovery and energy analysis

import React, { useState, useEffect } from 'react';
import { energyApiService } from '../services/energyApiService';
import './ResourceDiscovery.css';

const EnergyAnalysisFlow = ({ discoveredResources, microservices }) => {
    const [analysisState, setAnalysisState] = useState({
        step: 'ready', // ready, analyzing, complete, error
        progress: 0,
        currentTask: '',
        results: null,
        error: null
    });
    
    const [analysisConfig, setAnalysisConfig] = useState({
        timeRange: {
            startTime: new Date(Date.now() - 24 * 60 * 60 * 1000), // Last 24 hours
            endTime: new Date()
        },
        utilizationAssumption: 70, // Default 70% utilization
        includeSharedResources: true,
        enableRealTimeMetrics: false // Toggle for real vs estimated metrics
    });

    const analyzeEnergyConsumption = async () => {
        setAnalysisState({
            step: 'analyzing',
            progress: 0,
            currentTask: 'Preparing energy analysis...',
            results: null,
            error: null
        });

        try {
            // Step 1: Validate discovered resources (10%)
            setAnalysisState(prev => ({ 
                ...prev, 
                progress: 10, 
                currentTask: 'Validating discovered resources...' 
            }));
            
            const validatedResources = await validateResourcesForAnalysis(microservices);
            
            // Step 2: Gather resource metrics (30%)
            setAnalysisState(prev => ({ 
                ...prev, 
                progress: 30, 
                currentTask: 'Gathering resource metrics...' 
            }));
            
            const resourceMetrics = await gatherResourceMetrics(validatedResources);
            
            // Step 3: Calculate energy consumption (60%)
            setAnalysisState(prev => ({ 
                ...prev, 
                progress: 60, 
                currentTask: 'Calculating energy consumption...' 
            }));
            
            const energyCalculation = await calculateActualEnergyUsage(resourceMetrics);
            
            // Step 4: Generate insights and recommendations (80%)
            setAnalysisState(prev => ({ 
                ...prev, 
                progress: 80, 
                currentTask: 'Generating insights...' 
            }));
            
            const insights = await generateEnergyInsights(energyCalculation);
            
            // Step 5: Complete analysis (100%)
            setAnalysisState({
                step: 'complete',
                progress: 100,
                currentTask: 'Analysis complete!',
                results: {
                    energyCalculation,
                    insights,
                    timestamp: new Date(),
                    confidence: calculateConfidenceScore(resourceMetrics)
                },
                error: null
            });

        } catch (error) {
            setAnalysisState({
                step: 'error',
                progress: 0,
                currentTask: '',
                results: null,
                error: error.message
            });
        }
    };

    const validateResourcesForAnalysis = async (microservices) => {
        // Check if we have the necessary resource IDs and types for energy analysis
        const validatedServices = [];
        
        for (const service of microservices) {
            const validation = {
                serviceName: service.MicroserviceName,
                hasAppService: Boolean(service.AppServiceResourceId),
                hasFunctions: Boolean(service.FunctionAppResourceIds?.length),
                hasServiceBus: Boolean(service.ServiceBusResourceIds?.length),
                hasDatabase: Boolean(service.DatabaseResourceIds?.length),
                canAnalyze: false
            };
            
            // A service can be analyzed if it has at least one resource
            validation.canAnalyze = validation.hasAppService || 
                                  validation.hasFunctions || 
                                  validation.hasServiceBus || 
                                  validation.hasDatabase;
            
            validatedServices.push(validation);
        }
        
        await new Promise(resolve => setTimeout(resolve, 500)); // Simulate validation time
        return validatedServices;
    };

    const gatherResourceMetrics = async (validatedResources) => {
        const metrics = {};
        
        for (const service of validatedResources) {
            if (!service.canAnalyze) continue;
            
            metrics[service.serviceName] = {
                estimatedMetrics: {
                    // Use realistic baseline metrics when real metrics aren't available
                    appServiceCpu: service.hasAppService ? 45 + Math.random() * 30 : 0, // 45-75%
                    appServiceMemory: service.hasAppService ? 55 + Math.random() * 25 : 0, // 55-80%
                    functionExecutions: service.hasFunctions ? Math.floor(15000 + Math.random() * 10000) : 0,
                    serviceBusMessages: service.hasServiceBus ? Math.floor(8000 + Math.random() * 5000) : 0,
                    databaseDTU: service.hasDatabase ? 25 + Math.random() * 40 : 0, // 25-65%
                },
                actualMetrics: null, // Will be populated if Azure Monitor is available
                confidence: analysisConfig.enableRealTimeMetrics ? 'high' : 'estimated'
            };
        }
        
        await new Promise(resolve => setTimeout(resolve, 1000)); // Simulate metrics gathering
        return metrics;
    };

    const calculateActualEnergyUsage = async (resourceMetrics) => {
        const platformEnergyRequest = {
            StartTime: analysisConfig.timeRange.startTime,
            EndTime: analysisConfig.timeRange.endTime,
            Utilization: analysisConfig.utilizationAssumption,
            Microservices: microservices,
            SharedResourceIds: analysisConfig.includeSharedResources ? 
                discoveredResources?.sharedResources?.map(r => r.resourceId) || [] : []
        };

        // Call the enhanced energy analysis API
        const response = await energyApiService.analyzePlatformEnergy(platformEnergyRequest);
        
        // Enhance with our metrics analysis
        const enhancedResults = {
            ...response.energyReport,
            resourceMetrics,
            actualTotalEnergy: response.energyReport.kilowattHours,
            estimatedRange: {
                low: response.energyReport.kilowattHours * 0.8,
                high: response.energyReport.kilowattHours * 1.2
            },
            breakdown: response.energyReport.details,
            trends: response.trends
        };
        
        return enhancedResults;
    };

    const generateEnergyInsights = async (energyCalculation) => {
        const insights = {
            summary: {
                totalEnergy: energyCalculation.actualTotalEnergy,
                carbonFootprint: energyCalculation.carbonKg,
                servicesAnalyzed: microservices.length,
                confidence: calculateConfidenceScore(energyCalculation.resourceMetrics)
            },
            recommendations: [],
            comparisons: {},
            alerts: []
        };

        // Generate recommendations based on energy usage patterns
        const avgEnergyPerService = energyCalculation.actualTotalEnergy / microservices.length;
        
        if (avgEnergyPerService > 15) {
            insights.recommendations.push({
                type: 'optimization',
                priority: 'high',
                title: 'High Energy Consumption Detected',
                description: `Average energy per service (${avgEnergyPerService.toFixed(1)} kWh) is above recommended threshold`,
                action: 'Consider optimizing resource allocation and scaling policies'
            });
        }

        // Check for unbalanced energy distribution
        const energyValues = Object.values(energyCalculation.breakdown);
        const maxEnergy = Math.max(...energyValues);
        const minEnergy = Math.min(...energyValues.filter(v => v > 0));
        
        if (maxEnergy / minEnergy > 5) {
            insights.recommendations.push({
                type: 'balancing',
                priority: 'medium',
                title: 'Unbalanced Energy Distribution',
                description: 'Some services consume significantly more energy than others',
                action: 'Review resource allocation and consider load balancing'
            });
        }

        // Sustainability insights
        if (energyCalculation.carbonKg > 10) {
            insights.recommendations.push({
                type: 'sustainability',
                priority: 'medium',
                title: 'Carbon Footprint Optimization',
                description: `Current carbon footprint: ${energyCalculation.carbonKg.toFixed(1)} kg CO₂`,
                action: 'Consider using Azure regions with renewable energy sources'
            });
        }

        await new Promise(resolve => setTimeout(resolve, 800)); // Simulate analysis time
        return insights;
    };

    const calculateConfidenceScore = (resourceMetrics) => {
        if (!resourceMetrics) return 0;
        
        const totalServices = Object.keys(resourceMetrics).length;
        const highConfidenceServices = Object.values(resourceMetrics)
            .filter(m => m.confidence === 'high').length;
        
        return Math.round((highConfidenceServices / totalServices) * 100);
    };

    return (
        <div className="energy-analysis-flow">
            <div className="card border-0 shadow-sm">
                <div className="card-header bg-gradient-primary text-white">
                    <div className="d-flex justify-content-between align-items-center">
                        <div>
                            <h5 className="mb-1">⚡ Energy Analysis</h5>
                            <small>Analyze energy consumption for discovered services</small>
                        </div>
                        <div className="text-end">
                            <small>{microservices.length} services ready for analysis</small>
                        </div>
                    </div>
                </div>
                
                <div className="card-body">
                    {/* Analysis Configuration */}
                    {analysisState.step === 'ready' && (
                        <div className="analysis-config mb-4">
                            <h6 className="text-muted mb-3">📊 Analysis Configuration</h6>
                            
                            <div className="row g-3">
                                <div className="col-md-6">
                                    <label className="form-label">Time Range</label>
                                    <select 
                                        className="form-select" 
                                        value="24h"
                                        onChange={(e) => {
                                            const hours = parseInt(e.target.value);
                                            setAnalysisConfig(prev => ({
                                                ...prev,
                                                timeRange: {
                                                    startTime: new Date(Date.now() - hours * 60 * 60 * 1000),
                                                    endTime: new Date()
                                                }
                                            }));
                                        }}
                                    >
                                        <option value="24">Last 24 hours</option>
                                        <option value="72">Last 3 days</option>
                                        <option value="168">Last week</option>
                                        <option value="720">Last month</option>
                                    </select>
                                </div>
                                
                                <div className="col-md-6">
                                    <label className="form-label">Utilization Assumption</label>
                                    <div className="input-group">
                                        <input 
                                            type="range" 
                                            className="form-range" 
                                            min="10" 
                                            max="100" 
                                            value={analysisConfig.utilizationAssumption}
                                            onChange={(e) => setAnalysisConfig(prev => ({
                                                ...prev,
                                                utilizationAssumption: parseInt(e.target.value)
                                            }))}
                                        />
                                        <span className="input-group-text">{analysisConfig.utilizationAssumption}%</span>
                                    </div>
                                </div>
                            </div>
                            
                            <div className="form-check mt-3">
                                <input 
                                    className="form-check-input" 
                                    type="checkbox" 
                                    checked={analysisConfig.includeSharedResources}
                                    onChange={(e) => setAnalysisConfig(prev => ({
                                        ...prev,
                                        includeSharedResources: e.target.checked
                                    }))}
                                />
                                <label className="form-check-label">
                                    Include shared resources in analysis
                                </label>
                            </div>
                        </div>
                    )}
                    
                    {/* Analysis Progress */}
                    {analysisState.step === 'analyzing' && (
                        <div className="analysis-progress">
                            <div className="d-flex justify-content-between align-items-center mb-2">
                                <span className="text-muted">{analysisState.currentTask}</span>
                                <span className="badge bg-primary">{analysisState.progress}%</span>
                            </div>
                            <div className="progress mb-3">
                                <div 
                                    className="progress-bar progress-bar-striped progress-bar-animated" 
                                    style={{ width: `${analysisState.progress}%` }}
                                ></div>
                            </div>
                        </div>
                    )}
                    
                    {/* Analysis Results */}
                    {analysisState.step === 'complete' && analysisState.results && (
                        <div className="analysis-results">
                            <div className="row g-3 mb-4">
                                <div className="col-md-3">
                                    <div className="card bg-success text-white">
                                        <div className="card-body text-center">
                                            <h4 className="mb-1">{analysisState.results.energyCalculation.actualTotalEnergy.toFixed(1)}</h4>
                                            <small>kWh Total Energy</small>
                                        </div>
                                    </div>
                                </div>
                                <div className="col-md-3">
                                    <div className="card bg-info text-white">
                                        <div className="card-body text-center">
                                            <h4 className="mb-1">{analysisState.results.insights.summary.servicesAnalyzed}</h4>
                                            <small>Services Analyzed</small>
                                        </div>
                                    </div>
                                </div>
                                <div className="col-md-3">
                                    <div className="card bg-warning text-white">
                                        <div className="card-body text-center">
                                            <h4 className="mb-1">{analysisState.results.energyCalculation.carbonKg.toFixed(1)}</h4>
                                            <small>kg CO₂ Impact</small>
                                        </div>
                                    </div>
                                </div>
                                <div className="col-md-3">
                                    <div className="card bg-secondary text-white">
                                        <div className="card-body text-center">
                                            <h4 className="mb-1">{analysisState.results.confidence}%</h4>
                                            <small>Confidence</small>
                                        </div>
                                    </div>
                                </div>
                            </div>
                            
                            {/* Recommendations */}
                            {analysisState.results.insights.recommendations.length > 0 && (
                                <div className="recommendations mb-4">
                                    <h6 className="text-muted mb-3">💡 Recommendations</h6>
                                    {analysisState.results.insights.recommendations.map((rec, index) => (
                                        <div key={index} className={`alert alert-${rec.priority === 'high' ? 'warning' : 'info'} mb-2`}>
                                            <div className="d-flex justify-content-between">
                                                <div>
                                                    <strong>{rec.title}</strong>
                                                    <p className="mb-1">{rec.description}</p>
                                                    <small className="text-muted">{rec.action}</small>
                                                </div>
                                                <span className={`badge bg-${rec.priority === 'high' ? 'warning' : 'info'}`}>
                                                    {rec.priority}
                                                </span>
                                            </div>
                                        </div>
                                    ))}
                                </div>
                            )}
                        </div>
                    )}
                    
                    {/* Error State */}
                    {analysisState.step === 'error' && (
                        <div className="alert alert-danger">
                            <h6>❌ Analysis Failed</h6>
                            <p className="mb-0">{analysisState.error}</p>
                        </div>
                    )}
                    
                    {/* Action Buttons */}
                    <div className="text-center">
                        {analysisState.step === 'ready' && (
                            <button 
                                className="btn btn-primary btn-lg"
                                onClick={analyzeEnergyConsumption}
                                disabled={microservices.length === 0}
                            >
                                <i className="fas fa-bolt me-2"></i>
                                Start Energy Analysis
                            </button>
                        )}
                        
                        {analysisState.step === 'complete' && (
                            <div className="btn-group">
                                <button 
                                    className="btn btn-outline-primary"
                                    onClick={() => setAnalysisState(prev => ({ ...prev, step: 'ready' }))}
                                >
                                    <i className="fas fa-redo me-2"></i>
                                    Analyze Again
                                </button>
                                <button className="btn btn-success">
                                    <i className="fas fa-download me-2"></i>
                                    Export Report
                                </button>
                            </div>
                        )}
                        
                        {analysisState.step === 'error' && (
                            <button 
                                className="btn btn-outline-primary"
                                onClick={() => setAnalysisState(prev => ({ ...prev, step: 'ready' }))}
                            >
                                <i className="fas fa-retry me-2"></i>
                                Try Again
                            </button>
                        )}
                    </div>
                </div>
            </div>
        </div>
    );
};

export default EnergyAnalysisFlow;
