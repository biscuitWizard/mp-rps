using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

/// <summary>
/// Represents a single client connection on the server side.
/// Reads newline-delimited JSON on a background thread and enqueues
/// raw strings for main-thread processing via the GameServer.
/// </summary>
public class ClientSession
{
    private readonly TcpClient _client;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly ConcurrentQueue<(ClientSession, string)> _incomingQueue;
    private readonly Thread _readThread;
    private readonly object _writeLock = new();
    private volatile bool _connected;

    public string SessionId { get; set; }
    public string Username { get; set; }
    public bool IsAuthenticated { get; set; }
    public bool Connected => _connected && _client.Connected;

    public ClientSession(TcpClient client, ConcurrentQueue<(ClientSession, string)> incomingQueue)
    {
        _client = client;
        _incomingQueue = incomingQueue;
        _connected = true;

        var stream = client.GetStream();
        _reader = new StreamReader(stream);
        _writer = new StreamWriter(stream) { AutoFlush = true };

        _readThread = new Thread(ReadLoop) { IsBackground = true };
        _readThread.Start();
    }

    /// <summary>
    /// Serialize and send a message to this client. Thread-safe.
    /// </summary>
    public void Send(MessageBase message)
    {
        if (!Connected) return;

        try
        {
            var json = MessageRegistry.Serialize(message);
            lock (_writeLock)
            {
                _writer.WriteLine(json);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ClientSession] Send failed: {e.Message}");
            Disconnect();
        }
    }

    public void Disconnect()
    {
        _connected = false;
        try { _client?.Close(); }
        catch { /* already closed */ }
    }

    private void ReadLoop()
    {
        try
        {
            while (_connected)
            {
                var line = _reader.ReadLine();
                if (line == null) break;

                if (!string.IsNullOrWhiteSpace(line))
                    _incomingQueue.Enqueue((this, line));
            }
        }
        catch (Exception)
        {
            // Stream closed or broken pipe — expected on disconnect.
        }

        _connected = false;
        Debug.Log($"[ClientSession] Client disconnected: {Username ?? "unknown"}");
    }
}
