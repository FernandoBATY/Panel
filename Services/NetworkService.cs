using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Panel.Models;

namespace Panel.Services;

public class NetworkService
{
    private const int TCP_PORT = 5000;
    private const int UDP_PORT = 5001;
    
    private TcpListener? _server;
    private TcpClient? _client;
    private UdpClient? _udpClient;
    private NetworkStream? _stream;
    
    private readonly List<ConnectedClient> _connectedClients = new();
    private bool _isRunning;

    public event EventHandler<SyncMessage>? MessageReceived;
    public event EventHandler<NodeIdentity>? ClientConnected;
    public event EventHandler<string>? ClientDisconnected;

    // Lista de nodos conectados (para UI)
    public IReadOnlyList<NodeIdentity> ConnectedNodes => 
        _connectedClients.Select(c => c.Identity).ToList();

    #region Server Methods (Admin)

    public async Task StartServerAsync()
    {
        if (_isRunning) return;

        _server = new TcpListener(IPAddress.Any, TCP_PORT);
        _server.Start();
        _isRunning = true;

        Console.WriteLine($"[SERVER] Iniciado en puerto {TCP_PORT}");

        // Aceptar conexiones en background
        _ = Task.Run(AcceptClientsAsync);
        
        // Responder a discovery broadcasts
        _ = Task.Run(RespondToDiscoveryAsync);
    }

    private async Task AcceptClientsAsync()
    {
        while (_isRunning && _server != null)
        {
            try
            {
                var client = await _server.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClientAsync(client));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SERVER] Error aceptando cliente: {ex.Message}");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        var stream = client.GetStream();
        var buffer = new byte[8192];
        ConnectedClient? connectedClient = null;

        try
        {
            while (_isRunning)
            {
                var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                var json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                var message = JsonSerializer.Deserialize<SyncMessage>(json);

                if (message == null) continue;

                // Primer mensaje debe ser HELLO
                if (message.Operation == SyncOperation.Hello && connectedClient == null)
                {
                    connectedClient = new ConnectedClient
                    {
                        Client = client,
                        Stream = stream,
                        Identity = message.Sender!
                    };
                    
                    _connectedClients.Add(connectedClient);
                    ClientConnected?.Invoke(this, message.Sender!);
                    
                    Console.WriteLine($"[SERVER] Cliente conectado: {message.Sender!.Username} ({message.Sender.Role})");
                    
                    // Enviar FullSync al nuevo cliente
                    await SendFullSyncToClient(connectedClient);
                }
                else
                {
                    // Reenviar mensaje a todos los demás clientes
                    await BroadcastMessageAsync(message, connectedClient?.Identity.NodeId);
                    
                    // Notificar a la aplicación
                    MessageReceived?.Invoke(this, message);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SERVER] Error manejando cliente: {ex.Message}");
        }
        finally
        {
            if (connectedClient != null)
            {
                _connectedClients.Remove(connectedClient);
                ClientDisconnected?.Invoke(this, connectedClient.Identity.NodeId);
            }
            client.Close();
        }
    }

    private async Task SendFullSyncToClient(ConnectedClient client)
    {
        // TODO: Obtener snapshot completo de la DB y enviarlo
        // Por ahora solo confirmamos la conexión
        var welcomeMsg = new SyncMessage
        {
            Operation = SyncOperation.FullSync,
            Sender = SessionService.CurrentIdentity,
            EntityType = "Welcome",
            EntityJson = JsonSerializer.Serialize(ConnectedNodes)
        };

        await SendToClientAsync(client, welcomeMsg);
    }

    public async Task BroadcastMessageAsync(SyncMessage message, string? excludeNodeId = null)
    {
        var json = JsonSerializer.Serialize(message);
        var data = Encoding.UTF8.GetBytes(json);

        foreach (var client in _connectedClients.ToList())
        {
            if (client.Identity.NodeId == excludeNodeId) continue;

            try
            {
                await client.Stream.WriteAsync(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SERVER] Error enviando a {client.Identity.Username}: {ex.Message}");
            }
        }
    }

    private async Task SendToClientAsync(ConnectedClient client, SyncMessage message)
    {
        var json = JsonSerializer.Serialize(message);
        var data = Encoding.UTF8.GetBytes(json);
        await client.Stream.WriteAsync(data, 0, data.Length);
    }

    private async Task RespondToDiscoveryAsync()
    {
        _udpClient = new UdpClient(UDP_PORT);
        
        while (_isRunning)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync();
                var message = Encoding.UTF8.GetString(result.Buffer);

                if (message == "JAZER_DISCOVER")
                {
                    var response = $"JAZER_SERVER:{GetLocalIPAddress()}";
                    var responseData = Encoding.UTF8.GetBytes(response);
                    await _udpClient.SendAsync(responseData, responseData.Length, result.RemoteEndPoint);
                    
                    Console.WriteLine($"[SERVER] Discovery respondido a {result.RemoteEndPoint}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SERVER] Error en discovery: {ex.Message}");
            }
        }
    }

    #endregion

    #region Client Methods (Contador/Admin secundario)

    public async Task<bool> DiscoverAndConnectAsync()
    {
        try
        {
            using var udp = new UdpClient();
            udp.EnableBroadcast = true;
            
            var discoverMsg = Encoding.UTF8.GetBytes("JAZER_DISCOVER");
            await udp.SendAsync(discoverMsg, discoverMsg.Length, new IPEndPoint(IPAddress.Broadcast, UDP_PORT));

            udp.Client.ReceiveTimeout = 5000;
            var result = await udp.ReceiveAsync();
            var response = Encoding.UTF8.GetString(result.Buffer);

            if (response.StartsWith("JAZER_SERVER:"))
            {
                var serverIp = response.Replace("JAZER_SERVER:", "");
                return await ConnectToServerAsync(serverIp);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CLIENT] Error en discovery: {ex.Message}");
        }

        return false;
    }

    public async Task<bool> ConnectToServerAsync(string serverIp)
    {
        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(serverIp, TCP_PORT);
            _stream = _client.GetStream();
            _isRunning = true;

            Console.WriteLine($"[CLIENT] Conectado a servidor {serverIp}");

            // Enviar HELLO con identidad
            var helloMsg = new SyncMessage
            {
                Operation = SyncOperation.Hello,
                Sender = SessionService.CurrentIdentity
            };

            await SendMessageAsync(helloMsg);

            // Escuchar mensajes del servidor
            _ = Task.Run(ReceiveMessagesAsync);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CLIENT] Error conectando: {ex.Message}");
            return false;
        }
    }

    private async Task ReceiveMessagesAsync()
    {
        var buffer = new byte[8192];

        while (_isRunning && _stream != null)
        {
            try
            {
                var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                var json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                var message = JsonSerializer.Deserialize<SyncMessage>(json);

                if (message != null)
                {
                    MessageReceived?.Invoke(this, message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CLIENT] Error recibiendo: {ex.Message}");
                break;
            }
        }

        Disconnect();
    }

    public async Task SendMessageAsync(SyncMessage message)
    {
        if (_stream == null) return;

        var json = JsonSerializer.Serialize(message);
        var data = Encoding.UTF8.GetBytes(json);
        await _stream.WriteAsync(data, 0, data.Length);
    }

    #endregion

    #region Common Methods

    public void Disconnect()
    {
        _isRunning = false;
        _stream?.Close();
        _client?.Close();
        _server?.Stop();
        _udpClient?.Close();
        
        Console.WriteLine("[NETWORK] Desconectado");
    }

    public string? GetTailscaleIP()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            
            // Buscar IP en rango Tailscale (100.64.0.0/10)
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    var bytes = ip.GetAddressBytes();
                    // Tailscale usa 100.64.0.0 - 100.127.255.255
                    if (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127)
                    {
                        return ip.ToString();
                    }
                }
            }
            
            // Si no hay Tailscale, devolver IP local normal
            return GetLocalIPAddress();
        }
        catch
        {
            return null;
        }
    }

    private string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        return "127.0.0.1";
    }

    #endregion

    private class ConnectedClient
    {
        public TcpClient Client { get; set; } = null!;
        public NetworkStream Stream { get; set; } = null!;
        public NodeIdentity Identity { get; set; } = null!;
    }
}
