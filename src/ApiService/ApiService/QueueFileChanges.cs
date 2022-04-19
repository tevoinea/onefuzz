using Microsoft.Azure.Functions.Worker;
using System.Text.Json;
using Microsoft.OneFuzz.Service.OneFuzzLib.Orm;

namespace Microsoft.OneFuzz.Service;

public class QueueFileChanges
{
    // The number of time the function will be retried if an error occurs
    // https://docs.microsoft.com/en-us/azure/azure-functions/functions-bindings-storage-queue-trigger?tabs=csharp#poison-messages
    const int MAX_DEQUEUE_COUNT = 5;

    private readonly ILogTracer _log;

    private readonly IStorage _storage;

    private readonly INotificationOperations _notificationOperations;

    public QueueFileChanges(ILogTracer log, IStorage storage, INotificationOperations notificationOperations)
    {
        _log = log;
        _storage = storage;
        _notificationOperations = notificationOperations;
    }

    [Function("QueueFileChanges")]
    public async Async.Task Run(
        [QueueTrigger("file-changes-refactored", Connection = "AzureWebJobsStorage")] string msg,
        int dequeueCount)
    {
        var fileChangeEvent = JsonSerializer.Deserialize<Dictionary<string, string>>(msg, EntityConverter.GetJsonSerializerOptions());
        var lastTry = dequeueCount == MAX_DEQUEUE_COUNT;

        var _ = fileChangeEvent ?? throw new ArgumentException("Unable to parse queue trigger as JSON");

        // check type first before calling Azure APIs
        const string eventType = "eventType";
        if (!fileChangeEvent.ContainsKey(eventType)
            || fileChangeEvent[eventType] != "Microsoft.Storage.BlobCreated")
        {
            return;
        }

        const string topic = "topic";
        if (!fileChangeEvent.ContainsKey(topic)
            || !_storage.CorpusAccounts().Contains(fileChangeEvent[topic]))
        {
            return;
        }

        await file_added(_log, fileChangeEvent, lastTry);
    }

    private async Async.Task file_added(ILogTracer log, Dictionary<string, string> fileChangeEvent, bool failTaskOnTransientError)
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(fileChangeEvent["data"], EntityConverter.GetJsonSerializerOptions())!;
        var url = data["url"];
        var parts = url.Split("/").Skip(3).ToList();

        var container = parts[0];
        var path = string.Join('/', parts.Skip(1));

        log.Info($"file added container: {container} - path: {path}");
        await _notificationOperations.NewFiles(new Container(container), path, failTaskOnTransientError);
    }
}
