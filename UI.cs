using System.Text;
using System.Text.Json;
using System.Net.WebSockets;
using RoboMaster;

public class UI : IDisposable
{
    public event Action? OnClose;

    public RoboMasterClient? Robot { get; set; }
    public Follower? Follower { get; set; }

    private SocketsHttpHandler? handler;
    private ClientWebSocket? socket;

    public async Task Run()
    {
        handler = new SocketsHttpHandler();
        socket = new ClientWebSocket();

        await socket.ConnectAsync(new Uri("ws://localhost:8080"), new HttpMessageInvoker(handler), CancellationToken.None);

        var closeThread = new Thread(() =>
        {
            while (true)
            {
                if (socket.CloseStatus.HasValue)
                {
                    OnClose?.Invoke();
                    break;
                }
            }
        });

        closeThread.IsBackground = true;
        closeThread.Start();

        var isSending = false;
        
        Robot!.Line.Subscribe(async line =>
        {
            if (isSending)
            {
                return;
            }

            isSending = true;

            var json = JsonSerializer.Serialize(line);
            var bytes = Encoding.UTF8.GetBytes(json);
            var buffer = new ArraySegment<byte>(bytes);

            await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);

            isSending = false;
        });

        Follower!.WheelSpeed.Subscribe(async speed =>
        {
            if (isSending)
            {
                return;
            }

            isSending = true;

            var json = JsonSerializer.Serialize(speed);
            var bytes = Encoding.UTF8.GetBytes(json);
            var buffer = new ArraySegment<byte>(bytes);

            await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);

            isSending = false;
        });
    }

    public void Dispose()
    {
        socket?.Dispose();
        handler?.Dispose();
    }
}