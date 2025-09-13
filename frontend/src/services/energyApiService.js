import axios from 'axios';

const API_BASE_URL = process.env.REACT_APP_API_URL || 'http://localhost:5117';

class EnergyApiService {
    constructor() {
        this.client = axios.create({
            baseURL: API_BASE_URL
        });
    }

    async analyzePlatformEnergy(request) {
        try {
            const callId = `api_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
            console.log(`EnergyApiService: [${callId}] Sending platform energy analysis request:`, request);
            const response = await this.client.post('/api/Energy/analyze/platform', request);
            console.log(`EnergyApiService: [${callId}] Platform energy analysis response:`, response.data);
            return response.data;
        } catch (error) {
            console.error('EnergyApiService: Error analyzing platform energy:', error);
            throw error;
        }
    }

    async validateResourcesForAnalysis(microservices) {
        try {
            const response = await this.client.post('/api/Energy/validate-resources', {
                microservices
            });
            return response.data;
        } catch (error) {
            console.error('Error validating resources:', error);
            // Return fallback validation
            return microservices.map(service => ({
                serviceName: service.MicroserviceName,
                hasAppService: Boolean(service.AppServiceResourceId),
                hasFunctions: Boolean(service.FunctionAppResourceIds?.length),
                hasServiceBus: Boolean(service.ServiceBusResourceIds?.length),
                hasDatabase: Boolean(service.DatabaseResourceIds?.length),
                canAnalyze: Boolean(service.AppServiceResourceId || 
                          service.FunctionAppResourceIds?.length || 
                          service.ServiceBusResourceIds?.length || 
                          service.DatabaseResourceIds?.length)
            }));
        }
    }

    async getResourceMetrics(resourceIds, timeRange) {
        try {
            const response = await this.client.post('/api/Energy/resource-metrics', {
                resourceIds,
                startTime: timeRange.startTime,
                endTime: timeRange.endTime
            });
            return response.data;
        } catch (error) {
            console.error('Error fetching resource metrics:', error);
            // Return estimated metrics as fallback
            return this.generateEstimatedMetrics(resourceIds);
        }
    }

    generateEstimatedMetrics(resourceIds) {
        const metrics = {};
        resourceIds.forEach(resourceId => {
            const resourceType = this.extractResourceType(resourceId);
            metrics[resourceId] = {
                resourceType,
                estimatedMetrics: this.getBaselineMetrics(resourceType),
                confidence: 'estimated',
                timestamp: new Date().toISOString()
            };
        });
        return metrics;
    }

    extractResourceType(resourceId) {
        if (resourceId.includes('/Microsoft.Web/sites') && !resourceId.includes('functions')) {
            return 'AppService';
        } else if (resourceId.includes('/Microsoft.Web/sites') && resourceId.includes('functions')) {
            return 'Functions';
        } else if (resourceId.includes('/Microsoft.ServiceBus/')) {
            return 'ServiceBus';
        } else if (resourceId.includes('/Microsoft.Sql/') || resourceId.includes('/Microsoft.DocumentDB/')) {
            return 'Database';
        }
        return 'Unknown';
    }

    getBaselineMetrics(resourceType) {
        const baselines = {
            AppService: {
                cpuPercentage: 45 + Math.random() * 30,
                memoryPercentage: 55 + Math.random() * 25,
                requestsPerMinute: Math.floor(100 + Math.random() * 500),
                responseTime: 150 + Math.random() * 100
            },
            Functions: {
                executions: Math.floor(15000 + Math.random() * 10000),
                duration: 180 + Math.random() * 120,
                errors: Math.floor(Math.random() * 20),
                memoryUsage: 128 + Math.random() * 256
            },
            ServiceBus: {
                messagesIn: Math.floor(8000 + Math.random() * 5000),
                messagesOut: Math.floor(7500 + Math.random() * 4500),
                deadLetters: Math.floor(Math.random() * 10),
                activeConnections: Math.floor(10 + Math.random() * 20)
            },
            Database: {
                dtuPercentage: 25 + Math.random() * 40,
                connectionCount: Math.floor(20 + Math.random() * 50),
                transactionsPerSecond: Math.floor(50 + Math.random() * 200),
                ioPercentage: 15 + Math.random() * 30
            }
        };
        return baselines[resourceType] || {};
    }

    async fetchEnergyHistory(startDate, endDate) {
        try {
            const response = await this.client.get('/api/Energy/reports/history', {
                params: { startDate, endDate }
            });
            return response.data;
        } catch (error) {
            console.error('Error fetching energy history:', error);
            throw error;
        }
    }

    async getResourceList() {
        try {
                const response = await this.client.get('/api/Resources/shared');
            return response.data;
        } catch (error) {
            console.error('Error fetching resource list:', error);
            throw error;
        }
    }

    async getSharedResources() {
        try {
                const response = await this.client.get('/api/Resources/shared');
            return response.data;
        } catch (error) {
            console.error('Error fetching shared resources:', error);
            throw error;
        }
    }

    async getMicroservices() {
        try {
                const response = await this.client.get('/api/Resources/microservices');
            return response.data;
        } catch (error) {
            console.error('Error fetching microservices:', error);
            throw error;
        }
    }

    async fetchDiscoveryData(endpoint) {
        try {
            const response = await this.client.get(`/api/ResourceDiscovery/${endpoint}`);
            return response.data;
        } catch (error) {
            console.error(`Error fetching discovery data from ${endpoint}:`, error);
            throw error;
        }
    }

    async testAzureConnection() {
        try {
            const response = await this.client.get('/api/ResourceDiscovery/test-connection');
            return response.data;
        } catch (error) {
            console.error('Error testing Azure connection:', error);
            throw error;
        }
    }

    async discoverAllResources() {
        try {
            const response = await this.client.get('/api/ResourceDiscovery/resources');
            return response.data;
        } catch (error) {
            console.error('Error discovering all resources:', error);
            throw error;
        }
    }

    async discoverMicroservices() {
        try {
            const response = await this.client.get('/api/ResourceDiscovery/microservices');
            return response.data;
        } catch (error) {
            console.error('Error discovering microservices:', error);
            throw error;
        }
    }

    async getDiscoverySummary() {
        try {
            const response = await this.client.get('/api/ResourceDiscovery/summary');
            return response.data;
        } catch (error) {
            console.error('Error getting discovery summary:', error);
            throw error;
        }
    }

    async fetchCostSummary() {
        try {
            const response = await this.client.get('/api/Resources/costs/summary');
            return response.data;
        } catch (error) {
            console.error('Error fetching cost summary:', error);
            throw error;
        }
    }

    async getResourceGroupCost(resourceGroupName) {
        try {
            const response = await this.client.get(`/api/Resources/costs/resource-group/${resourceGroupName}`);
            return response.data;
        } catch (error) {
            console.error('Error fetching resource group cost:', error);
            throw error;
        }
    }
}

export const energyApiService = new EnergyApiService();

// Export individual methods for convenience
export const fetchDiscoveryData = (endpoint) => energyApiService.fetchDiscoveryData(endpoint);
export const testAzureConnection = () => energyApiService.testAzureConnection();
export const discoverAllResources = () => energyApiService.discoverAllResources();
export const discoverMicroservices = () => energyApiService.discoverMicroservices();
export const getDiscoverySummary = () => energyApiService.getDiscoverySummary();
