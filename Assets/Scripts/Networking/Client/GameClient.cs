using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

/// <summary>
/// TCP client that connects to the game server. Sends messages as
/// newline-delimited JSON and receives responses on a background thread,
/// routing them through the EventBus on the main thread.
///
/// Implements ITransport so the EventBus can push outbound messages
/// without knowing anything about TCP.
/// </summary>
public class GameClient : ITransport
{
    private TcpClient _client;
    private StreamReader _reader;
    private StreamWriter _writer;
    private Thread _readThread;
    private readonly ConcurrentQueue<string> _incomingQueue = new();
    private readonly object _writeLock = new();
    private volatile bool _connected;

    public bool Connected => _connected;

    public void Connect(string host, int port)
    {
        try
        {
            _client = new TcpClient();
            _client.Connect(host, port);
            _connected = true;

            var stream = _client.GetStream();
            _reader = new StreamReader(stream);
            _writer = new StreamWriter(stream) { AutoFlush = true };

            _readThread = new Thread(ReadLoop) { IsBackground = true };
            _readThread.Start();

            Debug.Log($"[GameClient] Connected to {host}:{port}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameClient] Connection failed: {e.Message}");
            _connected = false;
        }
    }

    /// <summary>
    /// Serialize and send a message to the server.
    /// Called by the EventBus via the ITransport interface.
    /// </summary>
    public void Send(MessageBase message)
    {
        if (!_connected) return;

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
            Debug.LogWarning($"[GameClient] Send failed: {e.Message}");
            Disconnect();
        }
    }

    /// <summary>
    /// Drain the incoming queue and route each message through the EventBus.
    /// Must be called from the Unity main thread (NetworkManager.Update).
    /// </summary>
    public void Update()
    {
        while (_incomingQueue.TryDequeue(out var json))
        {
            var message = MessageRegistry.Deserialize(json);
            if (message != null)
                EventBus.Instance.ReceiveMessage(message);
        }
    }

    public void Disconnect()
    {
        _connected = false;
        try { _client?.Close(); }
        catch { /* already closed */ }

        Debug.Log("[GameClient] Disconnected");
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
                    _incomingQueue.Enqueue(line);
            }
        }
        catch (Exception)
        {
            // Stream closed or broken pipe — expected on disconnect.
        }

        _connected = false;
    }
}
