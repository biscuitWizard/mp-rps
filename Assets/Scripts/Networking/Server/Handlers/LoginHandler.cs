using System;
using UnityEngine;

/// <summary>
/// Validates login credentials against a hardcoded user.
/// Returns Success = false for anything that doesn't match.
/// </summary>
public class LoginHandler : MessageHandler<LoginMessage>
{
    private const string ValidUsername = "Gamer";
    private const string ValidPassword = "13Games";

    protected override MessageBase[] HandleMessage(ClientSession sender, LoginMessage message)
    {
        if (!string.Equals(message.Username, ValidUsername, StringComparison.OrdinalIgnoreCase)
            || message.Password != ValidPassword)
        {
            Debug.Log($"[LoginHandler] Failed login attempt for '{message.Username}'");

            return new MessageBase[]
            {
                new UserAuthenticatedMessage
                {
                    Nonce = message.Nonce,
                    Success = false,
                    Username = message.Username
                }
            };
        }

        sender.Username = message.Username;
        sender.SessionId = Guid.NewGuid().ToString();
        sender.IsAuthenticated = true;

        Debug.Log($"[LoginHandler] Authenticated '{message.Username}' (session: {sender.SessionId})");

        return new MessageBase[]
        {
            new UserAuthenticatedMessage
            {
                Nonce = message.Nonce,
                Success = true,
                SessionId = sender.SessionId,
                Username = message.Username
            }
        };
    }
}
