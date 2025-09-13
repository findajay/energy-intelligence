import React from 'react';

const OptimizationRecommendations = ({ recommendations }) => {
  if (!recommendations) {
    return null;
  }

  const { currentSituation, recommendations: recommendationsList, summary } = recommendations;

  const getStatusBadgeClass = (status) => {
    switch (status) {
      case 'Highly Utilized': return 'badge bg-success';
      case 'Moderately Utilized': return 'badge bg-warning';
      case 'Under-utilized': return 'badge bg-danger';
      default: return 'badge bg-secondary';
    }
  };

  const getActionIcon = (action) => {
    if (action.includes('Scale Down')) return '📉';
    if (action.includes('Region')) return '🌍';
    if (action.includes('Right-size')) return '⚡';
    if (action.includes('Auto-scaling')) return '🔄';
    return '💡';
  };

  return (
    <div className="row mt-4">
      <div className="col-12">
        <div className="card">
          <div className="card-header bg-success text-white">
            <h5 className="mb-0">🎯 Energy Optimization Recommendations</h5>
            <small>AI-powered suggestions to minimize energy consumption</small>
          </div>
          <div className="card-body">
            {/* Current Situation */}
            <div className="row mb-4">
              <div className="col-12">
                <div className="card border-info">
                  <div className="card-header bg-light">
                    <h6 className="mb-0">📊 Current Infrastructure Analysis</h6>
                  </div>
                  <div className="card-body">
                    <div className="row">
                      <div className="col-md-3">
                        <div className="text-center">
                          <div className="h4 text-primary">{currentSituation.totalEnergy}</div>
                          <small className="text-muted">Total Energy</small>
                        </div>
                      </div>
                      <div className="col-md-3">
                        <div className="text-center">
                          <div className="h4 text-danger">{currentSituation.carbonFootprint}</div>
                          <small className="text-muted">Carbon Footprint</small>
                        </div>
                      </div>
                      <div className="col-md-3">
                        <div className="text-center">
                          <div className="h4 text-warning">{currentSituation.utilization}</div>
                          <small className="text-muted">Resource Utilization</small>
                        </div>
                      </div>
                      <div className="col-md-3">
                        <div className="text-center">
                          <span className={getStatusBadgeClass(currentSituation.status)}>
                            {currentSituation.status}
                          </span>
                          <div><small className="text-muted">Current Status</small></div>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            {/* Recommendations */}
            <div className="row">
              {recommendationsList && recommendationsList.map((rec, index) => (
                <div key={index} className="col-md-6 mb-3">
                  <div className="card h-100 border-success">
                    <div className="card-body">
                      <div className="d-flex align-items-start">
                        <div className="me-3" style={{ fontSize: '2rem' }}>
                          {getActionIcon(rec.action)}
                        </div>
                        <div className="flex-grow-1">
                          <h6 className="card-title text-success">{rec.action}</h6>
                          <p className="card-text small">{rec.description}</p>
                          
                          {rec.potentialSavings && (
                            <div className="mb-2">
                              <strong className="text-primary">💰 Energy Savings:</strong>
                              <div className="text-success">{rec.potentialSavings}</div>
                            </div>
                          )}
                          
                          {rec.carbonReduction && (
                            <div className="mb-2">
                              <strong className="text-primary">🌱 Carbon Reduction:</strong>
                              <div className="text-success">{rec.carbonReduction}</div>
                            </div>
                          )}

                          {rec.currentGrid && (
                            <div className="small text-muted">
                              <div><strong>Current:</strong> {rec.currentGrid}</div>
                              <div><strong>Optimal:</strong> {rec.optimalGrid}</div>
                            </div>
                          )}
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              ))}
            </div>

            {/* Summary */}
            <div className="row mt-4">
              <div className="col-12">
                <div className="card border-warning">
                  <div className="card-header bg-warning text-dark">
                    <h6 className="mb-0">📈 Optimization Potential</h6>
                  </div>
                  <div className="card-body">
                    <div className="row text-center">
                      <div className="col-md-4">
                        <div className="h4 text-success">{summary.maxPotentialSavings}</div>
                        <small className="text-muted">Maximum Energy Savings</small>
                      </div>
                      <div className="col-md-4">
                        <div className="h4 text-success">{summary.maxCarbonReduction}</div>
                        <small className="text-muted">Maximum Carbon Reduction</small>
                      </div>
                      <div className="col-md-4">
                        <div className="h4 text-primary">{summary.recommendedActions}</div>
                        <small className="text-muted">Priority Actions</small>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default OptimizationRecommendations;
