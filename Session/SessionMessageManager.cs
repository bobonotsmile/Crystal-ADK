using Crystal.Adk.Abstractions;

namespace Crystal.Adk.Session;

public sealed class SessionMessageManager
{
    private readonly List<RuntimeMessage> _messages = new();

    public SessionMessageManager()
    {
    }

    public SessionMessageManager(IEnumerable<RuntimeMessage> seedMessages)
    {
        _messages.AddRange(seedMessages.Select(CloneMessage));
    }

    public IReadOnlyList<RuntimeMessage> Messages => _messages;

    public void Add(RuntimeMessage message)
    {
        _messages.Add(CloneMessage(message));
    }

    public void AddSystem(string content)
    {
        _messages.Add(new RuntimeMessage { Role = "system", Content = content });
    }

    public void AddUser(string content)
    {
        _messages.Add(new RuntimeMessage { Role = "user", Content = content });
    }

    public void AddAssistant(string content)
    {
        _messages.Add(new RuntimeMessage { Role = "assistant", Content = content });
    }

    public void AddAssistant(string content, string? thinkingContent)
    {
        _messages.Add(new RuntimeMessage
        {
            Role = "assistant",
            Content = content,
            ThinkingContent = thinkingContent
        });
    }

    public List<RuntimeMessage> Export()
    {
        return _messages.Select(CloneMessage).ToList();
    }

    public bool UpdateFirstSystem(string newContent)
    {
        var message = _messages.FirstOrDefault(x => string.Equals(x.Role, "system", StringComparison.OrdinalIgnoreCase));
        if (message is null)
        {
            return false;
        }

        message.Content = newContent;
        return true;
    }

    public bool UpdateLatestUser(string newContent)
    {
        return UpdateLatestByRole("user", newContent);
    }

    public bool UpdateLatestAssistant(string newContent)
    {
        return UpdateLatestByRole("assistant", newContent);
    }

    public bool RemoveLatestUser()
    {
        return RemoveLatestByRole("user");
    }

    public bool RemoveLatestAssistant()
    {
        return RemoveLatestByRole("assistant");
    }

    public void Clear()
    {
        _messages.Clear();
    }

    private bool UpdateLatestByRole(string role, string newContent)
    {
        for (var i = _messages.Count - 1; i >= 0; i--)
        {
            if (string.Equals(_messages[i].Role, role, StringComparison.OrdinalIgnoreCase))
            {
                _messages[i].Content = newContent;
                return true;
            }
        }

        return false;
    }

    private bool RemoveLatestByRole(string role)
    {
        for (var i = _messages.Count - 1; i >= 0; i--)
        {
            if (string.Equals(_messages[i].Role, role, StringComparison.OrdinalIgnoreCase))
            {
                _messages.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    private static RuntimeMessage CloneMessage(RuntimeMessage message)
    {
        return new RuntimeMessage
        {
            Role = message.Role,
            Content = message.Content,
            ThinkingContent = message.ThinkingContent
        };
    }
}
