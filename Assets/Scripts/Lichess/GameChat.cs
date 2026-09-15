using System.Collections.Generic;

// A single chat message as it arrived from the board stream
public struct ChatMessage
{
    public string Room;       // "player" or "spectator"
    public string Username;
    public string Text;
}

// Accumulated chat for the current game

// Stream sends each line once and never re-sends, so we hold the list rather than derive it
// Owned by the session as a plain object, reached via _session.Chat; cleared each game
public class GameChat
{
    private readonly List<ChatMessage> _messages = new List<ChatMessage>();

    public IReadOnlyList<ChatMessage> Messages => _messages;

    public void Clear() => _messages.Clear();

    public void Append(ChatLineEvent line)
    {
        if (line == null || string.IsNullOrEmpty(line.text)) return;

        _messages.Add(new ChatMessage
        {
            Room = line.room,
            Username = line.username,
            Text = line.text,
        });
    }
}