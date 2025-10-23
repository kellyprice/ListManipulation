<?xml version="1.0" encoding="utf-8"?>

<configuration xmlns:xdt="http://schemas.microsoft.com/XML-Document-Transform">
	<configBuilders xdt:Transform="InsertBefore(connectionStrings)">
		<builders>
			<add name="AzureAppConfig" endpoint="__ConfigBuilders.AppConfigurationUrl__" type="Microsoft.Configuration.ConfigurationBuilders.AzureAppConfigurationBuilder, Microsoft.Configuration.ConfigurationBuilders.AzureAppConfiguration, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35" />
			<add name="AzureKeyVault" vaultName="__ConfigBuilders.KeyVaultName__" type="Microsoft.Configuration.ConfigurationBuilders.AzureKeyVaultConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Azure, Version=2.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35" />
		</builders>
	</configBuilders>
	
	<connectionStrings xdt:Transform="SetAttributes(configBuilders)" configBuilders="AzureKeyVault" >
		<add name="BCEDatabase" connectionString="__ConnectionStrings.IdentityServerSecurityService.BceConnectionString__" xdt:Transform="SetAttributes" xdt:Locator="Match(name)" />
		<add name="IdentityServerDatabase" connectionString="__ConnectionStrings.IdentityServerSecurityService.IdentityServerConnectionString__" xdt:Transform="SetAttributes" xdt:Locator="Match(name)" />
		<add name="DevHubDatabase" connectionString="__ConnectionStrings.IdentityServerSecurityService.DevHubConnectionString__" xdt:Transform="SetAttributes" xdt:Locator="Match(name)" />
		<add name="Crm" connectionString="__ConnectionStrings.IdentityServerSecurityService.Crm__" xdt:Transform="SetAttributes" xdt:Locator="Match(name)" />
		<!--<add name="ApplicationInsights" connectionString="__ConnectionStrings.IdentityServerSecurityService.ApplicationInsightsConnectionString__" xdt:Transform="SetAttributes" xdt:Locator="Match(name)" />-->
	</connectionStrings>

	<appSettings xdt:Transform="SetAttributes(configBuilders)" configBuilders="Environment,AzureAppConfig">
		<!--<add key="AzureAppConfig" value="__ConfigBuilders.AppConfigurationUrl__" xdt:Transform="SetAttributes" xdt:Locator="Match(key)" />
		<add key="AzureKeyVault" value="__ConfigBuilders.KeyVaultName__" xdt:Transform="SetAttributes" xdt:Locator="Match(key)" />-->
		<add key="profile" value="__AppSettings.IdentityServerSecurityService.Profile__" xdt:Transform="SetAttributes" xdt:Locator="Match(key)"/>
		<add key="crm:org-uri" value="__AppSettings.IdentityServerSecurityService.Crm:org-uri__" xdt:Transform="SetAttributes" xdt:Locator="Match(key)"/>
		<add key="crm:org-helper" value="__AppSettings.IdentityServerSecurityService.Crm:org-helper__" xdt:Transform="SetAttributes" xdt:Locator="Match(key)"/>
		<add key="UseDirectSmtp" value="__AppSettings.IdentityServerSecurityService.UseDirectSmtp__" xdt:Transform="SetAttributes" xdt:Locator="Match(key)"/>
		<add key="UseEmailDomainWhitelist" value="__AppSettings.IdentityServerSecurityService.UseEmailDomainWhitelist__" xdt:Transform="SetAttributes" xdt:Locator="Match(key)"/>
		<add key="FeatureToggleUseCloudCrm" value="__AppSettings.IdentityServerSecurityService.FeatureToggleUseCloudCrm__" xdt:Transform="SetAttributes" xdt:Locator="Match(key)"/>
	</appSettings>

	<system.web>
    <compilation xdt:Transform="RemoveAttributes(debug)" />
  </system.web>

</configuration>



public static async Task Get()
{
    // the client that owns the connection and can be used to create senders and receivers
    ServiceBusClient client;

    // the processor that reads and processes messages from the queue
    ServiceBusProcessor processor;

    // The Service Bus client types are safe to cache and use as a singleton for the lifetime
    // of the application, which is best practice when messages are being published or read
    // regularly.
    //
    // Set the transport type to AmqpWebSockets so that the ServiceBusClient uses port 443. 
    // If you use the default AmqpTcp, make sure that ports 5671 and 5672 are open.

    // TODO: Replace the <NAMESPACE-CONNECTION-STRING> and <QUEUE-NAME> placeholders
    var clientOptions = new ServiceBusClientOptions()
    {
        TransportType = ServiceBusTransportType.AmqpWebSockets
    };
    
    client = new ServiceBusClient("Endpoint=sb://kellytest.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=v4oJiRF5EHdlHKfLmX1WtB2smQ6mleEyc+ASbAe", clientOptions);

    // create a processor that we can use to process the messages
    // TODO: Replace the <QUEUE-NAME> placeholder
    processor = client.CreateProcessor("kellytestq", new ServiceBusProcessorOptions());

    try
    {
        // add handler to process messages
        processor.ProcessMessageAsync += MessageHandler;

        // add handler to process any errors
        processor.ProcessErrorAsync += ErrorHandler;

        // start processing 
        await processor.StartProcessingAsync();

        Console.WriteLine("Wait for a minute and then press any key to end the processing");
        Console.ReadKey();

        // stop processing 
        Console.WriteLine("\nStopping the receiver...");
        await processor.StopProcessingAsync();
        Console.WriteLine("Stopped receiving messages");
    }
    finally
    {
        // Calling DisposeAsync on client types is required to ensure that network
        // resources and other unmanaged objects are properly cleaned up.
        await processor.DisposeAsync();
        await client.DisposeAsync();
    }
}

static async Task MessageHandler(ProcessMessageEventArgs args)
{
    string body = args.Message.Body.ToString();
    Console.WriteLine($"Received: {body}");

    // complete the message. message is deleted from the queue. 
    await args.CompleteMessageAsync(args.Message);
}

// handle any errors when receiving messages
static Task ErrorHandler(ProcessErrorEventArgs args)
{
    Console.WriteLine(args.Exception.ToString());
    return Task.CompletedTask;
}

public static async Task EnqueueMessage()
{
    ServiceBusClient client;

    // the sender used to publish messages to the queue
    ServiceBusSender sender;

    // number of messages to be sent to the queue
    const int numOfMessages = 3;

    // The Service Bus client types are safe to cache and use as a singleton for the lifetime
    // of the application, which is best practice when messages are being published or read
    // regularly.
    //
    // set the transport type to AmqpWebSockets so that the ServiceBusClient uses the port 443. 
    // If you use the default AmqpTcp, you will need to make sure that the ports 5671 and 5672 are open

    // TODO: Replace the <NAMESPACE-CONNECTION-STRING> and <QUEUE-NAME> placeholders
    var clientOptions = new ServiceBusClientOptions()
    {
        TransportType = ServiceBusTransportType.AmqpWebSockets
    };

    client = new ServiceBusClient("Endpoint=sb://kellytest.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=v4oJiRF5EHdlHKfLmX1WtB2smQ6mleEyc+ASbAe", clientOptions);
    sender = client.CreateSender("kellytestq");

    // create a batch 
    using (ServiceBusMessageBatch messageBatch = await sender.CreateMessageBatchAsync())
    {
        for (int i = 1; i <= numOfMessages; i++)
        {
            // try adding a message to the batch
            if (!messageBatch.TryAddMessage(new ServiceBusMessage($"Message {i}")))
            {
                // if it is too large for the batch
                throw new Exception($"The message {i} is too large to fit in the batch.");
            }
        }

        try
        {
            // Use the producer client to send the batch of messages to the Service Bus queue
            await sender.SendMessagesAsync(messageBatch);
            Console.WriteLine($"A batch of {numOfMessages} messages has been published to the queue.");
        }
        finally
        {
            // Calling DisposeAsync on client types is required to ensure that network
            // resources and other unmanaged objects are properly cleaned up.
            await sender.DisposeAsync();
            await client.DisposeAsync();
        }
    }
while deploying in azure, the build failed with this error: Parsing error(s): {"events":[{"level":"Informational","event":"ParsingXMLStarted","message":"Started parsing XML"},{"level":"Informational","event":"ParsingXMLComplete","message":"Completed parsing XML"},{"level":"Verbose","event":"WsdlImportRuleVerifyWadl11Schema","message":"WSDL validated against XML Schema"},{"level":"Informational","event":"WsdlPrecheckComplete","message":"Completed WSDL verification. WSDL is considered valid."},{"level":"Informational","event":"WsdlParsingStarted","message":"Service : Endpoint : "}]} (Code:ValidationError)
    Console.WriteLine("Press any key to end the application");
    Console.ReadKey();
}
