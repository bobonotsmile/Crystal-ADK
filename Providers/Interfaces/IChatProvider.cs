using Crystal.Adk.Abstractions;

namespace Crystal.Adk.Providers;

public interface IChatProvider
{
    Task<RuntimeMessage> CompleteAsync(
        IReadOnlyList<RuntimeMessage> messages,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<StreamingChatChunk> StreamAsync(
        IReadOnlyList<RuntimeMessage> messages,
        CancellationToken cancellationToken = default);
}
