using UnityEngine;

/// <summary>
/// Singleton MonoBehaviour that owns the GameServer and GameClient and
/// manages their lifecycle. By default both run in the same process so a
/// single Unity instance acts as a listen-server. Toggle the inspector
/// flags to run headless server or client-only builds later.
/// </summary>
public class NetworkManager : MonoBehaviour
{
    private static NetworkManager _instance;
    public static NetworkManager Instance => _instance;

    [SerializeField]
    int _port = 7777;

    [SerializeField]
    string _host = "127.0.0.1";

    [SerializeField]
    bool _startServer = true;

    [SerializeField]
    bool _startClient = true;

    private GameServer _server;
    private GameClient _client;

    public GameServer Server => _server;
    public GameClient Client => _client;
    public bool IsServer => _startServer;
    public bool IsClient => _startClient;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        MessageRegistry.Initialize();

        if (_startServer)
        {
            _server = new GameServer();
            _server.Start(_port);
        }

        if (_startClient)
        {
            _client = new GameClient();
            EventBus.Instance.SetTransport(_client);
            _client.Connect(_host, _port);
        }
    }

    void Update()
    {
        if (_startServer)
            _server?.Update();

        if (_startClient)
            _client?.Update();
    }

    void OnDestroy()
    {
        _client?.Disconnect();
        _server?.Stop();
    }
}
