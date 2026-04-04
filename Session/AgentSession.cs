using System.Text;
using Crystal.Adk.Abstractions;
using Crystal.Adk.Providers;

namespace Crystal.Adk.Session;

public sealed class AgentSession
{
    private readonly IChatProvider _provider;
    private readonly SessionMessageManager _messageManager;

    public AgentSession(
        IChatProvider provider,
        SessionMessageManager messageManager)
    {
        _provider = provider;
        _messageManager = messageManager;
    }

    public SessionMessageManager History => _messageManager;

    public IReadOnlyList<RuntimeMessage> Messages => _messageManager.Messages;

    public async Task<RuntimeMessage> RunAsync(
        string userInput,
        CancellationToken cancellationToken = default)
    {
        var requestMessages = BuildRequestMessages(userInput);
        var assistant = await _provider.CompleteAsync(requestMessages, cancellationToken);

        _messageManager.AddUser(userInput);
        _messageManager.Add(assistant);
        return assistant;
    }

    public async IAsyncEnumerable<TextChunk> StreamTextAsync(
        string userInput,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestMessages = BuildRequestMessages(userInput);
        var fullText = new StringBuilder();
        var fullThinking = new StringBuilder();

        await foreach (var chunk in _provider.StreamAsync(requestMessages, cancellationToken))
        {
            var hasText = !string.IsNullOrEmpty(chunk.TextDelta);
            var hasThinking = !string.IsNullOrEmpty(chunk.ThinkingDelta);

            if (hasText)
            {
                fullText.Append(chunk.TextDelta);
            }

            if (hasThinking)
            {
                fullThinking.Append(chunk.ThinkingDelta);
            }

            if (!hasText && !hasThinking)
            {
                continue;
            }

            yield return new TextChunk
            {
                Text = chunk.TextDelta ?? string.Empty,
                ThinkingText = chunk.ThinkingDelta ?? string.Empty
            };
        }

        _messageManager.AddUser(userInput);
        _messageManager.AddAssistant(
            fullText.ToString(),
            fullThinking.Length > 0 ? fullThinking.ToString() : null);
    }

    public void Reset()
    {
        _messageManager.Clear();
    }

    private List<RuntimeMessage> BuildRequestMessages(string userInput)
    {
        var messages = _messageManager.Export();
        messages.Add(new RuntimeMessage
        {
            Role = "user",
            Content = userInput
        });

        return messages;
    }
}
