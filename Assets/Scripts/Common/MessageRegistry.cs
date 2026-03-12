using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Maps message type name strings to their System.Type for deserialization.
/// Populated once via reflection, scanning all loaded assemblies for MessageBase subclasses.
/// This lets you add a new message class without touching any registration code.
/// </summary>
public static class MessageRegistry
{
    private static readonly Dictionary<string, Type> _registry = new();
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsSubclassOf(typeof(MessageBase)) && !type.IsAbstract)
                        _registry[type.Name] = type;
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // Some assemblies can't be introspected; safe to skip.
            }
        }

        Debug.Log($"[MessageRegistry] Registered {_registry.Count} message types");
    }

    /// <summary>
    /// Resolve a message type name (e.g. "LoginMessage") to its System.Type.
    /// Returns null if the type is unknown.
    /// </summary>
    public static Type Resolve(string typeName)
    {
        if (!_initialized) Initialize();
        return _registry.TryGetValue(typeName, out var type) ? type : null;
    }

    /// <summary>
    /// Two-pass deserialization: first extract the type discriminator from
    /// the JSON envelope, then deserialize the full payload into the concrete class.
    /// </summary>
    public static MessageBase Deserialize(string json)
    {
        if (!_initialized) Initialize();

        var envelope = JsonUtility.FromJson<MessageBase>(json);
        if (envelope == null || string.IsNullOrEmpty(envelope.Type))
            return null;

        var type = Resolve(envelope.Type);
        if (type == null)
        {
            Debug.LogWarning($"[MessageRegistry] Unknown message type '{envelope.Type}'");
            return null;
        }

        return (MessageBase)JsonUtility.FromJson(json, type);
    }

    public static string Serialize(MessageBase message)
    {
        return JsonUtility.ToJson(message);
    }
}
