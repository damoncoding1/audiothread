using System.Threading.Channels;
using AudioThread.Application.Features.AudioFiles.Jobs;
using Microsoft.Extensions.Options;

namespace AudioThread.Infrastructure.Processing;

public sealed class AudioProcessingJobQueue : IAudioProcessingJobQueue
{
    private readonly Channel<AudioProcessingWorkItem> _channel;

    public AudioProcessingJobQueue(IOptions<AudioProcessingOptions> options)
    {
        _channel = Channel.CreateBounded<AudioProcessingWorkItem>(new BoundedChannelOptions(
            options.Value.QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });
    }

    public ValueTask EnqueueAsync(AudioProcessingWorkItem workItem, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(workItem, cancellationToken);

    public IAsyncEnumerable<AudioProcessingWorkItem> DequeueAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
