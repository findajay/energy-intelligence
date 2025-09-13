export const mockPlatformData = {
    platformName: "Energy Platform",
    totalEnergyConsumption: 29.25, // Updated to include shared resources
    kilowattHours: 29.25, // API format compatibility
    carbonKg: 7.02, // API format compatibility (29.25 * 0.24 for West Europe grid)
    microservices: ["PaymentService", "SessionsService"], // Correct service names
    // API-compatible details structure for EnergyChart with detailed shared resources
    details: {
        "PaymentService_AppService": 4.25,
        "PaymentService_Functions": 3.80,
        "PaymentService_ServiceBus": 1.20,
        "PaymentService_Database": 1.20,
        "SessionsService_AppService": 3.15,
        "SessionsService_Functions": 2.85,
        "SessionsService_ServiceBus": 1.10,
        "SessionsService_Database": 1.20,
        // Detailed shared resources breakdown for better transparency
        "Shared_Storage_energy-storage": 2.15,
        "Shared_Redis_session-cache": 1.85,
        "Shared_CosmosDB_global-db": 3.45,
        "Shared_KeyVault_app-secrets": 0.25,
        "Shared_ApplicationInsights_monitoring": 0.85,
        "Shared_LoadBalancer_main-lb": 1.20,
        "Shared_CDN_content-delivery": 0.75
    },
    microserviceReports: {
        "PaymentService": {
            microserviceName: "PaymentService",
            totalEnergyConsumption: 10.45, // Realistic for a payment service
            resourceBreakdown: {
                appServiceEnergy: 4.25,
                functionAppsEnergy: 3.80,
                serviceBusEnergy: 1.20,
                databaseEnergy: 1.20
            },
            detailedMetrics: [
                {
                    resourceId: "/subscriptions/123/resourceGroups/payment/providers/Microsoft.Web/sites/payment-api",
                    resourceType: "AppService",
                    resourceName: "payment-api",
                    energyConsumption: 4.25, // Realistic AppService consumption
                    detailedMetrics: {
                        cpuPercentage: 65.4,
                        memoryPercentage: 72.3,
                        http2xx: 15234,
                        http4xx: 123,
                        http5xx: 12
                    }
                },
                {
                    resourceId: "/subscriptions/123/resourceGroups/payment/providers/Microsoft.Web/sites/payment-functions",
                    resourceType: "Functions",
                    resourceName: "payment-functions",
                    energyConsumption: 3.80,
                    detailedMetrics: {
                        cpuPercentage: 45.2,
                        memoryPercentage: 58.1,
                        executions: 25430,
                        errors: 15,
                        duration: 245.7
                    }
                },
                {
                    resourceId: "/subscriptions/123/resourceGroups/payment/providers/Microsoft.ServiceBus/namespaces/payment-bus",
                    resourceType: "ServiceBus",
                    resourceName: "payment-bus",
                    energyConsumption: 1.20,
                    detailedMetrics: {
                        messagesIn: 12840,
                        messagesOut: 12798,
                        deadLetters: 5,
                        activeConnections: 18
                    }
                },
                {
                    resourceId: "/subscriptions/123/resourceGroups/payment/providers/Microsoft.Sql/servers/payment-db",
                    resourceType: "Database",
                    resourceName: "payment-db",
                    energyConsumption: 1.20,
                    detailedMetrics: {
                        dtuPercentage: 32.8,
                        connectionCount: 45,
                        deadlocks: 0,
                        blockedProcesses: 2
                    }
                }
            ]
        },
        "SessionsService": {
            microserviceName: "SessionsService",
            totalEnergyConsumption: 8.30, // Realistic for a sessions service
            resourceBreakdown: {
                appServiceEnergy: 3.15,
                functionAppsEnergy: 2.85,
                serviceBusEnergy: 1.10,
                databaseEnergy: 1.20
            },
            detailedMetrics: [
                {
                    resourceId: "/subscriptions/123/resourceGroups/sessions/providers/Microsoft.Web/sites/sessions-api",
                    resourceType: "AppService",
                    resourceName: "sessions-api",
                    energyConsumption: 3.15, // Realistic AppService consumption
                    detailedMetrics: {
                        cpuPercentage: 58.7,
                        memoryPercentage: 68.9,
                        http2xx: 12456,
                        http4xx: 98,
                        http5xx: 8
                    }
                },
                {
                    resourceId: "/subscriptions/123/resourceGroups/sessions/providers/Microsoft.Web/sites/sessions-functions",
                    resourceType: "Functions",
                    resourceName: "sessions-functions",
                    energyConsumption: 2.85,
                    detailedMetrics: {
                        cpuPercentage: 42.1,
                        memoryPercentage: 55.7,
                        executions: 18790,
                        errors: 8,
                        duration: 198.4
                    }
                },
                {
                    resourceId: "/subscriptions/123/resourceGroups/sessions/providers/Microsoft.ServiceBus/namespaces/sessions-bus",
                    resourceType: "ServiceBus",
                    resourceName: "sessions-bus",
                    energyConsumption: 1.10,
                    detailedMetrics: {
                        messagesIn: 9840,
                        messagesOut: 9815,
                        deadLetters: 2,
                        activeConnections: 14
                    }
                },
                {
                    resourceId: "/subscriptions/123/resourceGroups/sessions/providers/Microsoft.Sql/servers/sessions-db",
                    resourceType: "Database",
                    resourceName: "sessions-db",
                    energyConsumption: 1.20,
                    detailedMetrics: {
                        dtuPercentage: 28.5,
                        connectionCount: 38,
                        deadlocks: 0,
                        blockedProcesses: 1
                    }
                }
            ]
        }
    },
    sharedResourcesSummary: {
        appServicesEnergy: 7.40, // Combined AppServices energy
        functionAppsEnergy: 6.65, // Combined Function Apps energy  
        serviceBusEnergy: 2.30,   // Combined Service Bus energy
        databasesEnergy: 2.40     // Combined Database energy
    },
    trends: {
        // API format: daily, weekly, monthly arrays with date, totalEnergy, microservices
        daily: Array.from({ length: 30 }, (_, i) => ({
            date: new Date(Date.now() - (29 - i) * 24 * 60 * 60 * 1000).toISOString().split('T')[0],
            totalEnergy: 15 + Math.random() * 8, // Daily consumption between 15-23 kWh
            microservices: {
                "PaymentService": 8 + Math.random() * 4,
                "SessionsService": 6 + Math.random() * 3
            }
        })),
        weekly: Array.from({ length: 12 }, (_, i) => ({
            date: new Date(Date.now() - (11 - i) * 7 * 24 * 60 * 60 * 1000).toISOString().split('T')[0],
            totalEnergy: 105 + Math.random() * 56, // Weekly consumption around 105-161 kWh
            microservices: {
                "PaymentService": 56 + Math.random() * 28,
                "SessionsService": 42 + Math.random() * 21
            }
        })),
        monthly: Array.from({ length: 12 }, (_, i) => ({
            date: new Date(Date.now() - (11 - i) * 30 * 24 * 60 * 60 * 1000).toISOString().split('T')[0],
            totalEnergy: 450 + Math.random() * 240, // Monthly consumption around 450-690 kWh
            microservices: {
                "PaymentService": 240 + Math.random() * 120,
                "SessionsService": 180 + Math.random() * 90
            }
        })),
        // Keep the old format for backward compatibility but it won't be used by TrendsChart
        platformName: "Energy Platform",
        dailyTrends: Array.from({ length: 30 }, (_, i) => ({
            timestamp: new Date(Date.now() - (29 - i) * 24 * 60 * 60 * 1000),
            value: 15 + Math.random() * 8 // Daily consumption between 15-23 kWh
        })),
        weeklyTrends: Array.from({ length: 12 }, (_, i) => ({
            timestamp: new Date(Date.now() - (11 - i) * 7 * 24 * 60 * 60 * 1000),
            value: 105 + Math.random() * 56 // Weekly consumption around 105-161 kWh (15-23 kWh/day * 7)
        })),
        monthlyTrends: Array.from({ length: 12 }, (_, i) => ({
            timestamp: new Date(Date.now() - (11 - i) * 30 * 24 * 60 * 60 * 1000),
            value: 450 + Math.random() * 240 // Monthly consumption around 450-690 kWh
        })),
        microserviceTrends: {
            "PaymentService": {
                microserviceName: "PaymentService",
                dailyTrends: Array.from({ length: 30 }, (_, i) => ({
                    timestamp: new Date(Date.now() - (29 - i) * 24 * 60 * 60 * 1000),
                    value: 8 + Math.random() * 4 // Daily 8-12 kWh for payment service
                })),
                weeklyTrends: Array.from({ length: 12 }, (_, i) => ({
                    timestamp: new Date(Date.now() - (11 - i) * 7 * 24 * 60 * 60 * 1000),
                    value: 56 + Math.random() * 28 // Weekly 56-84 kWh
                })),
                monthlyTrends: Array.from({ length: 12 }, (_, i) => ({
                    timestamp: new Date(Date.now() - (11 - i) * 30 * 24 * 60 * 60 * 1000),
                    value: 240 + Math.random() * 120 // Monthly 240-360 kWh
                }))
            },
            "SessionsService": {
                microserviceName: "SessionsService",
                dailyTrends: Array.from({ length: 30 }, (_, i) => ({
                    timestamp: new Date(Date.now() - (29 - i) * 24 * 60 * 60 * 1000),
                    value: 6 + Math.random() * 3 // Daily 6-9 kWh for sessions service
                })),
                weeklyTrends: Array.from({ length: 12 }, (_, i) => ({
                    timestamp: new Date(Date.now() - (11 - i) * 7 * 24 * 60 * 60 * 1000),
                    value: 42 + Math.random() * 21 // Weekly 42-63 kWh
                })),
                monthlyTrends: Array.from({ length: 12 }, (_, i) => ({
                    timestamp: new Date(Date.now() - (11 - i) * 30 * 24 * 60 * 60 * 1000),
                    value: 180 + Math.random() * 90 // Monthly 180-270 kWh
                }))
            }
        },
        averageDailyConsumption: {
            appServicesEnergy: 7.40,  // Realistic average daily consumption
            functionAppsEnergy: 6.65,
            serviceBusEnergy: 2.30,
            databasesEnergy: 2.40
        }
    },
    startTime: new Date(Date.now() - 24 * 60 * 60 * 1000),
    endTime: new Date()
};
