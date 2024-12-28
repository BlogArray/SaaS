using Azure.Core.Extensions;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Extensions.Azure;

internal static class AzureClientFactoryBuilderExtensions
{
    public static IAzureClientBuilder<BlobServiceClient, BlobClientOptions> AddBlobServiceClient(this AzureClientFactoryBuilder builder, string serviceUriOrConnectionString, bool preferMsi = true)
    {
        return preferMsi && Uri.TryCreate(serviceUriOrConnectionString, UriKind.Absolute, out Uri? serviceUri)
            ? builder.AddBlobServiceClient(serviceUri)
            : BlobClientBuilderExtensions.AddBlobServiceClient(builder, serviceUriOrConnectionString);
    }

    public static IAzureClientBuilder<QueueServiceClient, QueueClientOptions> AddQueueServiceClient(this AzureClientFactoryBuilder builder, string serviceUriOrConnectionString, bool preferMsi = true)
    {
        return preferMsi && Uri.TryCreate(serviceUriOrConnectionString, UriKind.Absolute, out Uri? serviceUri)
            ? builder.AddQueueServiceClient(serviceUri)
            : QueueClientBuilderExtensions.AddQueueServiceClient(builder, serviceUriOrConnectionString);
    }

    public static IAzureClientBuilder<TableServiceClient, TableClientOptions> AddTableServiceClient(this AzureClientFactoryBuilder builder, string serviceUriOrConnectionString, bool preferMsi = true)
    {
        return preferMsi && Uri.TryCreate(serviceUriOrConnectionString, UriKind.Absolute, out Uri? serviceUri)
            ? builder.AddTableServiceClient(serviceUri)
            : TableClientBuilderExtensions.AddTableServiceClient(builder, serviceUriOrConnectionString);
    }
}
