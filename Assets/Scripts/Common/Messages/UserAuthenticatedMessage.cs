using System;

[Serializable]
public class UserAuthenticatedMessage : MessageBase
{
    public bool Success;
    public string SessionId;
    public string Username;
}
