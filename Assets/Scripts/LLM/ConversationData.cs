using System.Collections.Generic;

[System.Serializable]
public class ConversationSummary
{
    public string _id;
    public string title;
    public string updatedAt;
}

[System.Serializable]
public class FullConversation
{
    public string _id;
    public string systemContext;
    public List<MessageData> messages;
}

[System.Serializable]
public class MessageData
{
    public string role;
    public string content;
}