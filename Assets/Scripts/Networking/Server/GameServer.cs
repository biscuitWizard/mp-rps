using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

/// <summary>
/// TCP server that accepts client connections and dispatches their messages
/// through the MessageDispatcher. The accept loop runs on a background thread;
/// incoming messages are queued and drained on the Unity main thread in Update().
/// </summary>
public class GameServer
{
    private TcpListener _listener;
    private Thread _acceptThread;
    private readonly List<ClientSession> _sessions = new();
    private readonly ConcurrentQueue<(ClientSession Session, string Json)> _incomingQueue = new();
    private readonly MessageDispatcher _dispatcher = new();
    private volatile bool _running;

    public int Port { get; private set; }
    public bool Running => _running;

    public void Start(int port)
    {
        Port = port;
        _dispatcher.Initialize();

        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        _running = true;

        _acceptThread = new Thread(AcceptLoop) { IsBackground = true };
        _acceptThread.Start();

        Debug.Log($"[GameServer] Listening on port {port}");
    }

    public void Stop()
    {
        _running = false;
        _listener?.Stop();

        lock (_sessions)
        {
            foreach (var session in _sessions)
                session.Disconnect();
            _sessions.Clear();
        }

        Debug.Log("[GameServer] Stopped");
    }

    /// <summary>
    /// Drain the incoming message queue and run each through the dispatcher.
    /// Must be called from the Unity main thread (NetworkManager.Update).
    /// </summary>
    public void Update()
    {
        while (_incomingQueue.TryDequeue(out var item))
        {
            var responses = _dispatcher.Dispatch(item.Session, item.Json);
            if (responses == null) continue;

            foreach (var response in responses)
            {
                if (response != null)
                    item.Session.Send(response);
            }
        }

        CleanupDisconnected();
    }

    /// <summary>
    /// Send a message to every connected client.
    /// </summary>
    public void Broadcast(MessageBase message)
    {
        lock (_sessions)
        {
            foreach (var session in _sessions)
            {
                if (session.Connected)
                    session.Send(message);
            }
        }
    }

    private void AcceptLoop()
    {
        while (_running)
        {
            try
            {
                var tcpClient = _listener.AcceptTcpClient();
                var session = new ClientSession(tcpClient, _incomingQueue);

                lock (_sessions)
                    _sessions.Add(session);

                Debug.Log($"[GameServer] Client connected from {tcpClient.Client.RemoteEndPoint}");
            }
            catch (SocketException)
            {
                // Listener was stopped; exit gracefully.
            }
        }
    }

    private void CleanupDisconnected()
    {
        lock (_sessions)
            _sessions.RemoveAll(s => !s.Connected);
    }
}
