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

    Console.WriteLine("Press any key to end the application");
    Console.ReadKey();
}
