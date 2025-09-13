import React, { useState, useEffect } from 'react';
import { Line } from 'react-chartjs-2';
import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  Title,
  Tooltip,
  Legend,
} from 'chart.js';

ChartJS.register(
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  Title,
  Tooltip,
  Legend
);

const TrendsChart = ({ platformTrends }) => {
  const [timeframe, setTimeframe] = useState('daily');
  const [selectedMicroservice, setSelectedMicroservice] = useState('all');
  const [selectedMetric, setSelectedMetric] = useState('energy'); // 'energy' or 'co2'
  const [availableMicroservices, setAvailableMicroservices] = useState([]);

  // CO2 conversion factor (kg CO2 per kWh) - typical for West Europe grid
  const CO2_FACTOR = 0.24;

  useEffect(() => {
    console.log('TrendsChart: useEffect platformTrends changed:', platformTrends);
    if (platformTrends) {
      const microservices = getAvailableMicroservices();
      console.log('TrendsChart: Available microservices:', microservices);
      setAvailableMicroservices(microservices);
    }
  }, [platformTrends]);

  const generateForecast = (historicalData, timeframe, periods = 3) => {
    if (!historicalData || historicalData.length < 2) return [];
    
    const lastTwo = historicalData.slice(-2);
    const growthRate = (lastTwo[1] - lastTwo[0]) / lastTwo[0];
    const lastValue = historicalData[historicalData.length - 1];
    const lastDate = new Date(platformTrends[timeframe][platformTrends[timeframe].length - 1].date);
    
    const forecast = [];
    for (let i = 1; i <= periods; i++) {
      const forecastDate = new Date(lastDate);
      if (timeframe === 'daily') {
        forecastDate.setDate(forecastDate.getDate() + i);
      } else if (timeframe === 'weekly') {
        forecastDate.setDate(forecastDate.getDate() + (i * 7));
      } else if (timeframe === 'monthly') {
        forecastDate.setMonth(forecastDate.getMonth() + i);
      }
      
      const forecastValue = lastValue * Math.pow(1 + growthRate, i);
      forecast.push({
        date: forecastDate.toISOString().split('T')[0],
        value: Math.max(0, forecastValue), // Ensure non-negative
        isForecast: true
      });
    }
    
    return forecast;
  };

  const getTrendData = () => {
    console.log('TrendsChart: Getting trend data for timeframe:', timeframe);
    console.log('TrendsChart: platformTrends:', platformTrends);
    
    if (!platformTrends || !platformTrends[timeframe]) {
      console.log('TrendsChart: No platformTrends or no data for timeframe', timeframe);
      return null;
    }

    const trends = platformTrends[timeframe];
    console.log('TrendsChart: trends for', timeframe, ':', trends);
    
    if (!Array.isArray(trends) || trends.length === 0) {
      console.log('TrendsChart: trends is not array or empty');
      return null;
    }

    // Get historical data values
    let historicalData;
    if (selectedMicroservice === 'all') {
      historicalData = trends.map(trend => trend.totalEnergy);
    } else {
      historicalData = trends.map(trend => {
        if (trend.microservices && trend.microservices[selectedMicroservice]) {
          return trend.microservices[selectedMicroservice];
        }
        return 0;
      });
    }

    // Convert to CO2 if needed
    if (selectedMetric === 'co2') {
      historicalData = historicalData.map(value => value * CO2_FACTOR);
    }

    // Generate forecast
    const forecastData = generateForecast(historicalData, timeframe, 3);
    
    // Combine historical and forecast labels
    const historicalLabels = trends.map(trend => {
      const date = new Date(trend.date);
      if (timeframe === 'daily') {
        return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
      } else if (timeframe === 'weekly') {
        return `Week of ${date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })}`;
      } else {
        return date.toLocaleDateString('en-US', { month: 'short', year: 'numeric' });
      }
    });

    const forecastLabels = forecastData.map(forecast => {
      const date = new Date(forecast.date);
      if (timeframe === 'daily') {
        return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
      } else if (timeframe === 'weekly') {
        return `Week of ${date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })}`;
      } else {
        return date.toLocaleDateString('en-US', { month: 'short', year: 'numeric' });
      }
    });

    const allLabels = [...historicalLabels, ...forecastLabels];
    const allData = [...historicalData, ...forecastData.map(f => f.value)];

    console.log('TrendsChart: Chart labels:', allLabels);
    console.log('TrendsChart: Chart data:', allData);
    console.log('TrendsChart: Forecast data:', forecastData);

    // For single data points, duplicate the point to make the chart render properly
    if (historicalLabels.length === 1 && forecastLabels.length === 0) {
      allLabels.push(allLabels[0] + ' (current)');
      allData.push(allData[0]);
      console.log('TrendsChart: Single data point detected, duplicating for visualization');
    }

    const metricLabel = selectedMetric === 'energy' ? 'Energy' : 'CO₂ Emissions';
    const unit = selectedMetric === 'energy' ? 'kWh' : 'kg CO₂';

    return {
      labels: allLabels,
      datasets: [
        // Historical data
        {
          label: selectedMicroservice === 'all' ? `Total ${metricLabel}` : `${selectedMicroservice} ${metricLabel}`,
          data: [...historicalData, ...Array(forecastData.length).fill(null)],
          borderColor: selectedMicroservice === 'all' ? '#28a745' : '#007bff',
          backgroundColor: selectedMicroservice === 'all' ? 'rgba(40, 167, 69, 0.1)' : 'rgba(0, 123, 255, 0.1)',
          borderWidth: 2,
          fill: true,
          tension: 0.1,
          pointRadius: 4,
        },
        // Forecast data
        {
          label: `${metricLabel} Forecast`,
          data: [...Array(historicalData.length).fill(null), ...forecastData.map(f => f.value)],
          borderColor: selectedMetric === 'energy' ? '#ffc107' : '#fd7e14',
          backgroundColor: selectedMetric === 'energy' ? 'rgba(255, 193, 7, 0.1)' : 'rgba(253, 126, 20, 0.1)',
          borderWidth: 2,
          borderDash: [5, 5], // Dashed line for forecast
          fill: false,
          tension: 0.1,
          pointRadius: 6,
          pointStyle: 'triangle',
        },
      ],
    };
  };

  const getAvailableMicroservices = () => {
    if (!platformTrends || !platformTrends.daily || !Array.isArray(platformTrends.daily)) {
      return [];
    }

    const firstData = platformTrends.daily[0];
    if (firstData && firstData.microservices) {
      return Object.keys(firstData.microservices);
    }
    
    return [];
  };

  const chartData = getTrendData();
  const metricUnit = selectedMetric === 'energy' ? 'kWh' : 'kg CO₂';

  const chartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        position: 'top',
      },
      title: {
        display: true,
        text: `${selectedMetric === 'energy' ? 'Energy Consumption' : 'CO₂ Emissions'} Trends - ${timeframe.charAt(0).toUpperCase() + timeframe.slice(1)}`,
      },
      tooltip: {
        callbacks: {
          label: function(context) {
            return `${context.dataset.label}: ${context.parsed.y.toFixed(2)} ${metricUnit}`;
          }
        }
      }
    },
    scales: {
      y: {
        beginAtZero: true,
        title: {
          display: true,
          text: selectedMetric === 'energy' ? 'Energy (kWh)' : 'CO₂ Emissions (kg)',
        },
      },
      x: {
        title: {
          display: true,
          text: timeframe === 'daily' ? 'Days' : timeframe === 'weekly' ? 'Weeks' : 'Months',
        },
      },
    },
    elements: {
      point: {
        radius: chartData && chartData.datasets[0]?.data.length <= 2 ? 8 : 4, // Larger points for single data points
      },
    },
  };

  if (!platformTrends) {
    return (
      <div className="text-center p-4">
        <div className="text-muted">
          <i className="fas fa-chart-line fa-3x mb-3"></i>
          <p>No trend data available</p>
        </div>
      </div>
    );
  }

  return (
    <div className="trends-chart-container">
      {/* Controls */}
      <div className="row mb-3">
        <div className="col-md-4">
          <label className="form-label">Metric:</label>
          <select 
            className="form-select" 
            value={selectedMetric} 
            onChange={(e) => setSelectedMetric(e.target.value)}
          >
            <option value="energy">Energy Consumption (kWh)</option>
            <option value="co2">CO₂ Emissions (kg)</option>
          </select>
        </div>
        <div className="col-md-4">
          <label className="form-label">Timeframe:</label>
          <select 
            className="form-select" 
            value={timeframe} 
            onChange={(e) => setTimeframe(e.target.value)}
          >
            <option value="daily">Daily</option>
            <option value="weekly">Weekly</option>
            <option value="monthly">Monthly</option>
          </select>
        </div>
        <div className="col-md-4">
          <label className="form-label">Microservice:</label>
          <select 
            className="form-select" 
            value={selectedMicroservice} 
            onChange={(e) => setSelectedMicroservice(e.target.value)}
          >
            <option value="all">All (Total)</option>
            {availableMicroservices.map(service => (
              <option key={service} value={service}>{service}</option>
            ))}
          </select>
        </div>
      </div>

      {/* Chart */}
      <div style={{ height: '400px' }}>
        {chartData ? (
          <>
            <Line data={chartData} options={chartOptions} />
            {chartData.datasets[0]?.data.length <= 2 && (
              <div className="mt-2">
                <div className="alert alert-info">
                  <small>
                    <i className="fas fa-info-circle"></i> Limited data available for {timeframe} view. 
                    {timeframe === 'weekly' && ' Weekly data shows aggregated consumption for the current week.'}
                    {timeframe === 'monthly' && ' Monthly data shows aggregated consumption for the current month.'}
                  </small>
                </div>
              </div>
            )}
          </>
        ) : (
          <div className="d-flex align-items-center justify-content-center h-100">
            <div className="text-center text-muted">
              <div style={{ fontSize: '3rem' }}>📊</div>
              <div>No data available for {timeframe} view</div>
              <small className="text-muted">
                {timeframe === 'weekly' && 'Weekly trends require multiple weeks of data'}
                {timeframe === 'monthly' && 'Monthly trends require multiple months of data'}
              </small>
            </div>
          </div>
        )}
      </div>

      {/* Summary */}
      {chartData && (
        <div className="mt-3">
          <div className="row">
            <div className="col-md-8">
              <div className="alert alert-info">
                <small>
                  <strong>Available microservices:</strong> {availableMicroservices.join(', ')}
                </small>
              </div>
            </div>
            <div className="col-md-4">
              <div className="alert alert-warning">
                <small>
                  <i className="fas fa-chart-line"></i> <strong>Forecast:</strong> 3-period projection based on recent trends
                  <br />
                  <i className="fas fa-info-circle"></i> CO₂ factor: {CO2_FACTOR} kg/kWh (West Europe grid)
                </small>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default TrendsChart;
