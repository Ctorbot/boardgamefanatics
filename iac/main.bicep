@description('Base name for all resources.')
param appName string = 'boardgamefanatics'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Supabase connection string.')
@secure()
param connectionString string

@description('Email address to notify when budget thresholds are reached.')
param alertEmail string

// ACR names must be globally unique, alphanumeric only
var acrName = '${replace(appName, '-', '')}${uniqueString(resourceGroup().id)}'

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: true
  }
}

resource containerEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${appName}-env'
  location: location
  properties: {}
}

// Container App is provisioned with a placeholder image on first deploy.
// The CI/CD workflow updates the image to the real build after each push.
resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: appName
  location: location
  properties: {
    managedEnvironmentId: containerEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
      }
      registries: [
        {
          server: acr.properties.loginServer
          username: acr.listCredentials().username
          passwordSecretRef: 'acr-password'
        }
      ]
      secrets: [
        {
          name: 'acr-password'
          value: acr.listCredentials().passwords[0].value
        }
        {
          name: 'db-connection'
          value: connectionString
        }
      ]
    }
    template: {
      containers: [
        {
          name: appName
          image: 'mcr.microsoft.com/dotnet/aspnet:10.0'
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              name: 'ConnectionStrings__DefaultConnection'
              secretRef: 'db-connection'
            }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/healthz'
                port: 8080
              }
              initialDelaySeconds: 15
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/healthz'
                port: 8080
              }
              initialDelaySeconds: 5
              periodSeconds: 10
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 1
      }
    }
  }
}

// Budget alerts only — Azure does not automatically stop resources when exceeded.
resource budget 'Microsoft.Consumption/budgets@2021-10-01' = {
  name: '${appName}-budget'
  properties: {
    category: 'Cost'
    amount: 10
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: '2026-03-01'
    }
    filter: {
      dimensions: {
        name: 'ResourceGroupName'
        operator: 'In'
        values: [resourceGroup().name]
      }
    }
    notifications: {
      alertAt80Percent: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 80
        thresholdType: 'Actual'
        contactEmails: [alertEmail]
      }
      alertAt100Percent: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 100
        thresholdType: 'Actual'
        contactEmails: [alertEmail]
      }
    }
  }
}

output acrName string = acr.name
output acrLoginServer string = acr.properties.loginServer
output containerAppName string = containerApp.name
output containerAppUrl string = containerApp.properties.configuration.ingress.fqdn
