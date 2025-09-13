import React, { useState } from 'react';
import { energyApiService } from '../services/energyApiService';
import './ResourceDiscovery.css';

const ResourceDiscovery = () => {
    const [discoveryData, setDiscoveryData] = useState(null);
    const [costData, setCostData] = useState(null);
    const [loading, setLoading] = useState(false);
    const [progress, setProgress] = useState(0);
    const [currentStep, setCurrentStep] = useState('');
    const [error, setError] = useState(null);
    const [showAllMicroservices, setShowAllMicroservices] = useState(false);
    const [showAllResourceTypes, setShowAllResourceTypes] = useState(false);

    const discoverInfrastructure = async () => {
        setLoading(true);
        setProgress(0);
        setCurrentStep('Initializing discovery...');
        setError(null);
        
        try {
            // Step 1: Summary
            setProgress(10);
            setCurrentStep('Discovering summary data...');
            const summary = await energyApiService.fetchDiscoveryData('summary');
            
            setProgress(40);
            setCurrentStep('Analyzing microservices...');
            const microservices = await energyApiService.fetchDiscoveryData('microservices');
            
            setProgress(70);
            setCurrentStep('Cataloging resources...');
            const resources = await energyApiService.fetchDiscoveryData('resources');
            
            setProgress(85);
            setCurrentStep('Calculating costs...');
            const costs = await energyApiService.fetchCostSummary();
            
            setProgress(90);
            setCurrentStep('Finalizing discovery...');
            
            setDiscoveryData({
                summary,
                microservices,
                resources
            });
            
            setCostData(costs);
            
            setProgress(100);
            setCurrentStep('Discovery complete!');
            
            // Clear progress after a short delay
            setTimeout(() => {
                setProgress(0);
                setCurrentStep('');
            }, 1000);
            
        } catch (err) {
            setError(`Failed to discover infrastructure: ${err.message}`);
            setProgress(0);
            setCurrentStep('');
        } finally {
            setLoading(false);
        }
    };

    const renderInfrastructureOverview = () => {
        if (!discoveryData) return null;

        const { summary, microservices, resources } = discoveryData;

        return (
            <div className="infrastructure-overview">
                {/* Summary Cards */}
                <div className="row mb-4">
                    <div className="col-md-3">
                        <div className="card summary-card bg-primary text-white">
                            <div className="card-body text-center">
                                <h5 className="card-title">🏗️ Resource Groups</h5>
                                <h3 className="resource-count-display">{summary?.subscription?.totalResourceGroups || 0}</h3>
                            </div>
                        </div>
                    </div>
                    <div className="col-md-3">
                        <div className="card summary-card bg-success text-white">
                            <div className="card-body text-center">
                                <h5 className="card-title">🌐 Microservices</h5>
                                <h3 className="resource-count-display">{microservices?.totalCount || summary?.microservices?.count || 0}</h3>
                            </div>
                        </div>
                    </div>
                    <div className="col-md-3">
                        <div className="card summary-card bg-info text-white">
                            <div className="card-body text-center">
                                <h5 className="card-title">🔧 Total Resources</h5>
                                <h3 className="resource-count-display">{resources?.totalCount || summary?.subscription?.totalResources || 0}</h3>
                            </div>
                        </div>
                    </div>
                    <div className="col-md-3">
                        <div className="card summary-card bg-warning text-white">
                            <div className="card-body text-center">
                                <h5 className="card-title">📍 Regions</h5>
                                <h3 className="resource-count-display">{summary?.subscription?.locations?.length || 0}</h3>
                            </div>
                        </div>
                    </div>
                </div>

                {/* Cost Summary Section */}
                {costData && (
                    <div className="row mb-4">
                        <div className="col-12">
                            <div className="card">
                                <div className="card-header">
                                    <h5 className="mb-0">💰 Monthly Cost Breakdown</h5>
                                    <small className="text-muted">Estimated monthly Azure costs by category</small>
                                </div>
                                <div className="card-body">
                                    <div className="row">
                                        <div className="col-md-8">
                                            <div className="row">
                                                {Object.entries(costData.summary || {}).map(([category, amount]) => (
                                                    <div key={category} className="col-md-6 col-lg-4 mb-3">
                                                        <div className="card resource-type-compact border-0 bg-light h-100">
                                                            <div className="card-body text-center p-3">
                                                                <h6 className="card-title mb-2">{category}</h6>
                                                                <div className="h5 text-primary fw-bold">${amount.toFixed(2)}</div>
                                                                <small className="text-muted">per month</small>
                                                            </div>
                                                        </div>
                                                    </div>
                                                ))}
                                            </div>
                                        </div>
                                        <div className="col-md-4">
                                            <div className="card resource-type-featured border-0 h-100">
                                                <div className="card-body text-center">
                                                    <h5 className="card-title">Total Monthly Cost</h5>
                                                    <div className="display-4 text-success mb-3">💵</div>
                                                    <div className="resource-count-display">${costData.totalMonthly?.toFixed(2) || '0.00'}</div>
                                                    <small className="text-muted discovery-stats">per month</small>
                                                    <div className="mt-2">
                                                        <small className="text-muted">
                                                            ~${(costData.totalMonthly * 12)?.toFixed(0) || '0'}/year
                                                        </small>
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                )}

                {/* Microservices Section */}
                <div className="card mb-4">
                    <div className="card-header">
                        <h5 className="mb-0">🌐 Discovered Microservices</h5>
                    </div>
                    <div className="card-body">
                        {(microservices?.microservices || summary?.microservices?.services) && 
                         Array.isArray(microservices?.microservices || summary?.microservices?.services) ? (
                            <div className="row">
                                {(microservices?.microservices || summary?.microservices?.services).slice(0, showAllMicroservices ? undefined : 8).map((service, index) => (
                                    <div key={index} className="col-md-6 col-lg-3 mb-3">
                                        <div className="card microservice-card h-100">
                                            <div className="card-body">
                                                <h6 className="text-success fw-bold">{service.name || service.Name}</h6>
                                                <p className="text-muted mb-2">
                                                    <small>{service.totalResourceCount || service.TotalResourceCount || service.resourceCount || 0} resources</small>
                                                </p>
                                                {(service.totalCost || service.TotalCost) && (
                                                    <div className="mb-2">
                                                        <small className="text-primary fw-bold">
                                                            ${(service.totalCost?.monthlyCost || service.TotalCost?.MonthlyCost || 0).toFixed(2)}/month
                                                        </small>
                                                    </div>
                                                )}
                                                <div className="mt-2">
                                                    {(service.appServices?.length > 0 || service.breakdown?.appServices > 0) && (
                                                        <span className="badge badge-count bg-primary me-1 mb-1">
                                                            App: {service.appServices?.length || service.breakdown?.appServices || 0}
                                                        </span>
                                                    )}
                                                    {(service.appServices?.length > 0 || service.breakdown?.appServices > 0) && (
                                                        <span className="badge badge-count bg-primary me-1 mb-1">
                                                            App: {service.appServices?.length || service.breakdown?.appServices || 0}
                                                        </span>
                                                    )}
                                                    {(service.functionApps?.length > 0 || service.breakdown?.functionApps > 0) && (
                                                        <span className="badge badge-count bg-warning me-1 mb-1">
                                                            Func: {service.functionApps?.length || service.breakdown?.functionApps || 0}
                                                        </span>
                                                    )}
                                                    {(service.databases?.length > 0 || service.breakdown?.databases > 0) && (
                                                        <span className="badge badge-count bg-info me-1 mb-1">
                                                            DB: {service.databases?.length || service.breakdown?.databases || 0}
                                                        </span>
                                                    )}
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                ))}
                                {(microservices?.microservices || summary?.microservices?.services)?.length > 8 && (
                                    <div className="col-12">
                                        <button 
                                            className="btn btn-outline-primary btn-sm"
                                            onClick={() => setShowAllMicroservices(!showAllMicroservices)}
                                        >
                                            {showAllMicroservices 
                                                ? 'Show Less' 
                                                : `Show All ${(microservices?.microservices || summary?.microservices?.services).length} Microservices`
                                            }
                                        </button>
                                    </div>
                                )}
                            </div>
                        ) : (
                            <div className="alert alert-info">
                                <h6>No microservices detected</h6>
                                <p className="mb-0">This might be because your resources don't follow typical microservice naming patterns, or they're organized differently.</p>
                            </div>
                        )}
                    </div>
                </div>

                {/* Section Divider */}
                <hr className="section-divider" />

                {/* Resource Types Breakdown */}
                <div className="card">
                    <div className="card-header">
                        <h5 className="mb-0">🔧 Resource Types Breakdown</h5>
                        <small className="text-muted">Top Azure resource types in your subscription</small>
                    </div>
                    <div className="card-body">
                        {(resources?.resources || summary?.resourceTypes) ? (
                            <div>
                                {/* Top 3 Resource Types - Featured */}
                                <div className="row mb-4">
                                    {(resources?.resources || summary?.resourceTypes)?.slice(0, 3).map((item, index) => (
                                        <div key={index} className="col-md-4 mb-3">
                                            <div className="card resource-type-featured border-0 shadow-sm h-100">
                                                <div className="card-body text-center">
                                                    <div className="display-4 text-primary mb-3">
                                                        {index === 0 ? '🥇' : index === 1 ? '🥈' : '🥉'}
                                                    </div>
                                                    <h5 className="card-title text-truncate" title={(item.resourceType || item.type || 'Unknown')}>
                                                        {(item.resourceType || item.type || 'Unknown').replace('Microsoft.', '')}
                                                    </h5>
                                                    <div className="resource-count-display">{item.count}</div>
                                                    <small className="text-muted discovery-stats">resources</small>
                                                </div>
                                            </div>
                                        </div>
                                    ))}
                                </div>

                                {/* Remaining Resource Types - Compact List */}
                                {(resources?.resources || summary?.resourceTypes)?.length > 3 && (
                                    <div>
                                        <h6 className="mb-3 text-muted">Other Resource Types</h6>
                                        <div className="row">
                                            {(resources?.resources || summary?.resourceTypes)?.slice(3, showAllResourceTypes ? undefined : 9).map((item, index) => (
                                                <div key={index + 3} className="col-md-6 col-lg-4 mb-2">
                                                    <div className="resource-type-compact d-flex justify-content-between align-items-center p-2 border rounded bg-light">
                                                        <span className="text-truncate me-2" title={(item.resourceType || item.type || 'Unknown')}>
                                                            <small>{(item.resourceType || item.type || 'Unknown').replace('Microsoft.', '')}</small>
                                                        </span>
                                                        <span className="badge badge-count bg-primary">{item.count}</span>
                                                    </div>
                                                </div>
                                            ))}
                                        </div>
                                        
                                        {/* Show More/Less Button */}
                                        {(resources?.resources || summary?.resourceTypes)?.length > 9 && (
                                            <div className="text-center mt-3">
                                                <button 
                                                    className="btn btn-outline-secondary btn-sm"
                                                    onClick={() => setShowAllResourceTypes(!showAllResourceTypes)}
                                                >
                                                    {showAllResourceTypes 
                                                        ? 'Show Less Resource Types' 
                                                        : `Show All ${(resources?.resources || summary?.resourceTypes).length} Resource Types`
                                                    }
                                                </button>
                                            </div>
                                        )}
                                    </div>
                                )}
                            </div>
                        ) : (
                            <div className="alert alert-info">
                                <i className="fas fa-info-circle me-2"></i>
                                <strong>Resource breakdown will appear here after discovery.</strong>
                                <p className="mb-0 mt-2">Click "Discover Infrastructure" to see the distribution of Azure resource types in your subscription.</p>
                            </div>
                        )}
                    </div>
                </div>
            </div>
        );
    };

    return (
        <div className="resource-discovery-container container-fluid mt-4">
            <div className="row">
                <div className="col-12">
                    <div className="card discovery-card">
                        <div className="card-header discovery-header bg-primary text-white">
                            <h4 className="mb-0">🔍 Azure Infrastructure Discovery</h4>
                            <small>Automatically discover and catalog your Azure resources and microservices</small>
                        </div>
                        <div className="card-body">
                            {!discoveryData && (
                                <div className="text-center py-5">
                                    <h5>Ready to discover your Azure infrastructure?</h5>
                                    <p className="text-muted mb-4">
                                        This will scan your Azure subscription and automatically identify:
                                        <br/>• Microservices and applications
                                        <br/>• Resource groups and regions  
                                        <br/>• All Azure resources by type
                                    </p>
                                    <button 
                                        className="btn btn-primary btn-lg shadow-sm" 
                                        onClick={discoverInfrastructure}
                                        disabled={loading}
                                    >
                                        {loading ? (
                                            <>
                                                🔍 Discovering Infrastructure...
                                            </>
                                        ) : (
                                            <>
                                                🚀 Discover Infrastructure
                                            </>
                                        )}
                                    </button>
                                    
                                    {loading && (
                                        <div className="mt-3">
                                            <div className="progress" style={{ height: '8px' }}>
                                                <div 
                                                    className="progress-bar progress-bar-striped progress-bar-animated" 
                                                    role="progressbar" 
                                                    style={{ width: `${progress}%` }}
                                                    aria-valuenow={progress} 
                                                    aria-valuemin="0" 
                                                    aria-valuemax="100"
                                                ></div>
                                            </div>
                                            <small className="text-muted mt-2 d-block">
                                                {currentStep} ({progress}%)
                                            </small>
                                        </div>
                                    )}
                                </div>
                            )}

                            {error && (
                                <div className="alert alert-danger">
                                    <h6>Discovery Failed</h6>
                                    <p className="mb-0">{error}</p>
                                    <button 
                                        className="btn btn-outline-danger btn-sm mt-2" 
                                        onClick={discoverInfrastructure}
                                    >
                                        Try Again
                                    </button>
                                </div>
                            )}

                            {discoveryData && (
                                <>
                                    <div className="d-flex justify-content-between align-items-center mb-4">
                                        <h5 className="mb-0">✅ Infrastructure Discovery Complete</h5>
                                        <button 
                                            className="btn btn-outline-primary" 
                                            onClick={discoverInfrastructure}
                                            disabled={loading}
                                        >
                                            🔄 Refresh Discovery
                                        </button>
                                    </div>
                                    {renderInfrastructureOverview()}
                                </>
                            )}
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default ResourceDiscovery;