# Yaml pipeline to build IdentityServer
# This is used to check Azure Services are working ok in the NonProd subscription before deploying any further
trigger: none

resources:
  repositories:
  - repository: IdentityServer
    type: git
    name: IdentityServer 
    ref: ${{ variables.branchName }}
    trigger: 
      branches:
        include:
        - refs/heads/master
        - releases/*

name: $(BuildDefinitionName)_$(date:yyyyMMdd)$(rev:.r)

variables:
  # isMainBranch: True
  # isReleaseBranch: True
  # isMainOrReleaseBranch: True
  # environmentPrefix: 'TESTING-'

  isMainBranch: $[eq(resources.repositories['IdentityServer'].ref, 'refs/heads/master')]
  isReleaseBranch: $[startsWith(variables['Build.SourceBranch'], 'refs/heads/releases/')]
  isMainOrReleaseBranch: $[or(eq(resources.repositories['IdentityServer'].ref, 'refs/heads/master'), startsWith(variables['Build.SourceBranch'], 'refs/heads/releases/'))]
  environmentPrefix: ''

  Pipeline.Azure.Subscription.NonProd: NonProd-Admin
  Pipeline.Azure.Subscription.PreProd: PreProd-Admin
  Pipeline.Azure.Subscription.Prod: Prod-Admin

  Pipeline.Azure.ShortRegionCode: we
  Pipeline.Azure.WebAppServiceType.WebApi: web-api
  Pipeline.Azure.WebAppName: identityserver

  Pipeline.Artifacts.BicepFolder: $(System.DefaultWorkingDirectory)\Infrastructure\Bicep
  Pipeline.Artifacts.PowerShellFolder: $(System.DefaultWorkingDirectory)/Pipelines/PowerShell
  Pipeline.Artifacts.ApplicationArtifactName: 'IdentityServerSecurityService' 
  Pipeline.Artifacts.Name.IAC: 'IaC'
  Pipeline.Artifacts.Category: Applications

  Application.BuildParameters.Solution: '**\*.sln'
  Application.BuildParameters.BuildConfiguration: 'Release' 
  Application.BuildParameters.BuildPlatform: 'any cpu'

  Azure.TemplateSpec.Subscription.NonProd: NonProd
  Azure.TemplateSpec.Subscription.PreProd: PreProd
  Azure.TemplateSpec.Subscription.Prod: Prod
  
  Azure.TemplateSpec.Location: WestEurope
  Azure.TemplateSpec.ResourceGroupName: templateSpec-rg
  Azure.TemplateSpec.Name: IdentityServer
  
stages:

- template: /Pipelines/Yaml/Shared/Templates/Stage Templates/stageTemplate-InitialiseListVariablesValidateBuildIaC.yml
  parameters: 
    pipelineAgentPool:       AzBuildAgents
    templateSpecNamePrefix:  'IdentityServer'
    templateSpecNamePostfix: 'PostDeployment'

- stage: 'BuildApplication' 
  displayName: 'Build Application'  
  dependsOn: 'Initialise'
  
  jobs:
  - job: Build 
    continueOnError: false
    workspace:
      clean: all
    pool:
      name: AzBuildAgents
    
    steps: 
    - checkout: IdentityServer
      clean: true

    - task: FileTransform@1
      displayName: 'Transform *.Release.config to Web.Config' 
      inputs:
        folderPath: $(System.DefaultWorkingDirectory)/
        enableXmlTransform: true
        xmlTransformationRules: >-
           -transform $(System.DefaultWorkingDirectory)\src\IdentityServer.Connect.Web\*.Release.config -xml $(System.DefaultWorkingDirectory)\src\IdentityServer.Connect.Web\web.config
           -transform $(System.DefaultWorkingDirectory)\src\IdentityServer.SecurityService.Wcf\*.Release.config -xml $(System.DefaultWorkingDirectory)\src\IdentityServer.SecurityService.Wcf\web.config
        fileType: xml
    
    - task: NuGetToolInstaller@1
      displayName: 'Install latest version of NuGet'
      inputs:
        checkLatest: true
       
    - task: NuGetCommand@2
      displayName: NuGet restore
      inputs:
        solution: $(Application.BuildParameters.Solution)
        feedRestore: 655e1e6e-ceb4-4af4-985f-4c97d16d696e/80e6581d-c0c2-437a-993b-0b39f6e26e35
    
    - task: VSBuild@1
      displayName: Build solution
      inputs:
        msbuildArgs: /p:WebPublishMethod=FileSystem  /p:OutDir=$(build.stagingDirectory)  /p:DeployIisAppPath=/
        platform: $(Application.BuildParameters.BuildPlatform)
        configuration: $(Application.BuildParameters.BuildConfiguration)
        clean: true

    - task: VSTest@2
      displayName: 'VsTest - Run tests for Solution'
      inputs:
        testAssemblyVer2: |
          **\$(Application.BuildParameters.BuildConfiguration)\**\*UnitTests*.dll
                  runInParallel: true
        platform: $(Application.BuildParameters.BuildPlatform)
        configuration: $(Application.BuildParameters.BuildConfiguration)

    - task: PublishBuildArtifacts@1
      displayName: Publish Application Artifact
      #condition: and(succeeded(), eq(variables.isMasterOrReleaseBranch, true))
      inputs:
        PathtoPublish: $(Build.StagingDirectory)
        ArtifactName: $(Pipeline.Artifacts.ApplicationArtifactName)
        publishLocation: 'Container'

- stage: 'AppDeploymentDEVEnvironment'
  displayName: 'App Deployment (Dev Environment)'
  dependsOn: 
  - Initialise
  - BuildIaC
  - BuildApplication
  condition: and(succeeded(), eq(true, true))
  variables:
    TemplateSpecVersion: $[stageDependencies.Initialise.SetVersion.outputs['PSTemplateSpecVersion.Azure.TemplateSpec.Version'] ]
    PartialSubscriptionId: d190
  jobs:
  - template: /Pipelines/Yaml/Shared/Templates/Job Templates/deployIACToAzure/jobTemplate-publishTemplateSpecToAzure.yml
    parameters:
       jobName: PublishTemplateSpec_IdentityServerPostDeployment
       pipelineAgentPool: AzNonProdWestEuropeReleaseAgents
       azureSubscriptionServicePrinciple: $(Pipeline.Azure.Subscription.NonProd)
       artifact: $(Pipeline.Artifacts.Name.IAC)
       templateSpecFileName: $(System.ArtifactsDirectory)\$(Pipeline.Artifacts.Name.IAC)\Json\IdentityServer-PostDeployment.json
       templateSpecName: IdentityServer-PostDeployment
       templateSpecVersion: $(TemplateSpecVersion)
       templateSpecDescription: Updates a subscription so it contains the resource groups and resources for the IdentityServer-PostDeployment script

  - template: /Pipelines/Yaml/Applications/IdentityServer/Job Templates/jobTemplate-deployApplicationToAzure.yml
    parameters:
       jobName: Deploy_Application_to_Dev_environment
       namePrefix: "Dev_"
       AppConfigurationUrl: https://identityserver-dev-d190-appcfg.azconfig.io
       KeyVaultName: idserver-dev-we-d190-kv
       KeyVaultEndpoint: https://idserver-dev-we-d190-kv.vault.azure.net/
       pipelineAgentPool: AzNonProdWestEuropeReleaseAgents
       azureSubscriptionServicePrinciple: $(Pipeline.Azure.Subscription.NonProd)
       environment: dev
       pipelineEnvironment: $(environmentPrefix)Azure-IdentityServer-Dev
       deploymentResourceGroupName: identityserver-nonprod-dev-we-rg
       dependsOn:  
         - PublishTemplateSpec_IdentityServerPostDeployment

- stage: 'AppDeploymentUAT2'
  displayName: 'App Deployment (UAT2)'
  dependsOn: 
  - Initialise
  - BuildIaC
  - BuildApplication
  condition: and(succeeded(), eq(variables.isMainBranch, true))
  variables:
    TemplateSpecVersion: $[stageDependencies.Initialise.SetVersion.outputs['PSTemplateSpecVersion.Azure.TemplateSpec.Version'] ]
    PartialSubscriptionId: d190
  jobs:
  - template: /Pipelines/Yaml/Shared/Templates/Job Templates/deployIACToAzure/jobTemplate-publishTemplateSpecToAzure.yml
    parameters:
       jobName: PublishTemplateSpec_IdentityServerPostDeployment
       pipelineAgentPool: AzNonProdWestEuropeReleaseAgents
       azureSubscriptionServicePrinciple: $(Pipeline.Azure.Subscription.NonProd)
       artifact: $(Pipeline.Artifacts.Name.IAC)
       templateSpecFileName: $(System.ArtifactsDirectory)\$(Pipeline.Artifacts.Name.IAC)\Json\IdentityServer-PostDeployment.json
       templateSpecName: IdentityServer-PostDeployment
       templateSpecVersion: $(TemplateSpecVersion)
       templateSpecDescription: Updates a subscription so it contains the resource groups and resources for the IdentityServer-PostDeployment script

  - template: /Pipelines/Yaml/Applications/IdentityServer/Job Templates/jobTemplate-deployApplicationToAzure.yml
    parameters:
       jobName: Deploy_Application_to_Uat2_environment
       namePrefix: "UAT2_"
       AppConfigurationUrl: https://identityserver-uat2-d190-appcfg.azconfig.io
       KeyVaultName: idserver-uat2-we-d190-kv
       KeyVaultEndpoint: https://idserver-uat2-we-d190-kv.vault.azure.net/
       pipelineAgentPool: AzNonProdWestEuropeReleaseAgents
       azureSubscriptionServicePrinciple: $(Pipeline.Azure.Subscription.NonProd)
       environment: uat2
       pipelineEnvironment: $(environmentPrefix)Azure-IdentityServer-UAT2
       deploymentResourceGroupName: identityserver-nonprod-uat2-we-rg
       dependsOn:  
         - PublishTemplateSpec_IdentityServerPostDeployment

- stage: 'AppDeploymentUAT3'
  displayName: 'App Deployment (UAT3)'
  dependsOn: 
  - Initialise
  - BuildIaC
  - BuildApplication
  condition: and(succeeded(), eq(variables.isMainBranch, true))
  variables:
    TemplateSpecVersion: $[stageDependencies.Initialise.SetVersion.outputs['PSTemplateSpecVersion.Azure.TemplateSpec.Version'] ]
    PartialSubscriptionId: d190
  jobs:
  - template: /Pipelines/Yaml/Shared/Templates/Job Templates/deployIACToAzure/jobTemplate-publishTemplateSpecToAzure.yml
    parameters:
       jobName: PublishTemplateSpec_IdentityServerPostDeployment
       pipelineAgentPool: AzNonProdWestEuropeReleaseAgents
       azureSubscriptionServicePrinciple: $(Pipeline.Azure.Subscription.NonProd)
       artifact: $(Pipeline.Artifacts.Name.IAC)
       templateSpecFileName: $(System.ArtifactsDirectory)\$(Pipeline.Artifacts.Name.IAC)\Json\IdentityServer-PostDeployment.json
       templateSpecName: IdentityServer-PostDeployment
       templateSpecVersion: $(TemplateSpecVersion)
       templateSpecDescription: Updates a subscription so it contains the resource groups and resources for the IdentityServer-PostDeployment script

  - template: /Pipelines/Yaml/Applications/IdentityServer/Job Templates/jobTemplate-deployApplicationToAzure.yml
    parameters:
       jobName: Deploy_Application_to_Uat3_environment
       namePrefix: "UAT3_"
       AppConfigurationUrl: https://identityserver-uat3-d190-appcfg.azconfig.io
       KeyVaultName: idserver-uat3-we-d190-kv
       KeyVaultEndpoint: https://idserver-uat3-we-d190-kv.vault.azure.net/
       pipelineAgentPool: AzNonProdWestEuropeReleaseAgents
       azureSubscriptionServicePrinciple: $(Pipeline.Azure.Subscription.NonProd)
       environment: uat3
       pipelineEnvironment: $(environmentPrefix)Azure-IdentityServer-UAT3
       deploymentResourceGroupName: identityserver-nonprod-uat3-we-rg
       dependsOn:  
         - PublishTemplateSpec_IdentityServerPostDeployment

- stage: 'AppDeploymentUAT4'
  displayName: 'App Deployment (UAT4)'
  dependsOn: 
  - Initialise
  - BuildIaC
  - BuildApplication
  condition: and(succeeded(), eq(true, true))
  variables:
    TemplateSpecVersion: $[stageDependencies.Initialise.SetVersion.outputs['PSTemplateSpecVersion.Azure.TemplateSpec.Version'] ]
    PartialSubscriptionId: d190
  jobs:
  - template: /Pipelines/Yaml/Shared/Templates/Job Templates/deployIACToAzure/jobTemplate-publishTemplateSpecToAzure.yml
    parameters:
       jobName: PublishTemplateSpec_IdentityServerPostDeployment
       pipelineAgentPool: AzNonProdWestEuropeReleaseAgents
       azureSubscriptionServicePrinciple: $(Pipeline.Azure.Subscription.NonProd)
       artifact: $(Pipeline.Artifacts.Name.IAC)
       templateSpecFileName: $(System.ArtifactsDirectory)\$(Pipeline.Artifacts.Name.IAC)\Json\IdentityServer-PostDeployment.json
       templateSpecName: IdentityServer-PostDeployment
       templateSpecVersion: $(TemplateSpecVersion)
       templateSpecDescription: Updates a subscription so it contains the resource groups and resources for the IdentityServer-PostDeployment script

  - template: /Pipelines/Yaml/Applications/IdentityServer/Job Templates/jobTemplate-deployApplicationToAzure.yml
    parameters:
       jobName: Deploy_Application_to_Uat4_environment
       namePrefix: "UAT4_"
       AppConfigurationUrl: https://identityserver-uat4-d190-appcfg.azconfig.io
       KeyVaultName: idserver-uat4-we-d190-kv
       KeyVaultEndpoint: https://idserver-uat4-we-d190-kv.vault.azure.net/
       pipelineAgentPool: AzNonProdWestEuropeReleaseAgents
       azureSubscriptionServicePrinciple: $(Pipeline.Azure.Subscription.NonProd)
       environment: uat4
       pipelineEnvironment: $(environmentPrefix)Azure-IdentityServer-UAT4
       deploymentResourceGroupName: identityserver-nonprod-uat4-we-rg
       dependsOn:  
         - PublishTemplateSpec_IdentityServerPostDeployment

- stage: 'AppDeploymentUAT1'
  displayName: 'App Deployment (UAT1)'
  dependsOn: 
  - Initialise
  - BuildIaC
  - BuildApplication
  condition: and(succeeded(), eq(variables.isReleaseBranch, true))
  variables:
    TemplateSpecVersion: $[stageDependencies.Initialise.SetVersion.outputs['PSTemplateSpecVersion.Azure.TemplateSpec.Version'] ]
    PartialSubscriptionId: d190
  jobs:
  - template: /Pipelines/Yaml/Shared/Templates/Job Templates/deployIACToAzure/jobTemplate-publishTemplateSpecToAzure.yml
    parameters:
       jobName: PublishTemplateSpec_IdentityServerPostDeployment
       pipelineAgentPool: AzNonProdWestEuropeReleaseAgents
       azureSubscriptionServicePrinciple: $(Pipeline.Azure.Subscription.NonProd)
       artifact: $(Pipeline.Artifacts.Name.IAC)
       templateSpecFileName: $(System.ArtifactsDirectory)\$(Pipeline.Artifacts.Name.IAC)\Json\IdentityServer-PostDeployment.json
       templateSpecName: IdentityServer-PostDeployment
       templateSpecVersion: $(TemplateSpecVersion)
       templateSpecDescription: Updates a subscription so it contains the resource groups and resources for the IdentityServer-PostDeployment script

  - template: /Pipelines/Yaml/Applications/IdentityServer/Job Templates/jobTemplate-deployApplicationToAzure.yml
    parameters:
       jobName: Deploy_Application_to_Uat1_environment
       namePrefix: "UAT1_"
       AppConfigurationUrl: https://identityserver-uat1-d190-appcfg.azconfig.io
       KeyVaultName: idserver-uat1-we-d190-kv
       KeyVaultEndpoint: https://idserver-uat1-we-d190-kv.vault.azure.net/
       pipelineAgentPool: AzNonProdWestEuropeReleaseAgents
       azureSubscriptionServicePrinciple: $(Pipeline.Azure.Subscription.NonProd)
       environment: uat1
       pipelineEnvironment: $(environmentPrefix)Azure-IdentityServer-UAT1
       deploymentResourceGroupName: identityserver-nonprod-uat4-we-rg
       dependsOn:  
         - PublishTemplateSpec_IdentityServerPostDeployment
