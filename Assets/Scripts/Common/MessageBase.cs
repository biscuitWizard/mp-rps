using System;
using UnityEngine;

/// <summary>
/// Base class for all network messages. Carries a type discriminator for
/// server-side routing and a nonce for client-side request/response correlation.
///
/// Subclasses should be marked [Serializable] and add their own public fields.
/// The Type field is auto-populated from the class name so new message types
/// need zero registration boilerplate.
/// </summary>
[Serializable]
public class MessageBase
{
    public string Type;
    public string Nonce;

    public MessageBase()
    {
        Type = GetType().Name;
        Nonce = Guid.NewGuid().ToString();
    }
}
