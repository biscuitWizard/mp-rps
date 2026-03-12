using System;

[Serializable]
public class LoginMessage : MessageBase
{
    public string Username;
    public string Password;
}
