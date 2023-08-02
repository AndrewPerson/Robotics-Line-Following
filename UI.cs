using System.Text.Json;
using WebSocketSharp;
using WebSocketSharp.Server;
using RoboMaster;

public class UI : IDisposable
{
    public event Action? OnClose;

    public Feed<float> BaseSpeed => server.BaseSpeed;
    public Feed<float> TargetX => server.TargetX;
    public Feed<float> PSensitivity => server.PSensitivity;
    public Feed<float> ISensitivity => server.ISensitivity;
    public Feed<float> DSensitivity => server.DSensitivity;
    public Feed<float> LookAheadSensitivityDropoff => server.LookAheadSensitivityDropoff;

    public RoboMasterClient Robot { get; }
    public Follower Follower { get; }

    private WebSocketServer? webSocketServer;
    private UIServer server = new();

    public UI(RoboMasterClient robot, Follower follower)
    {
        Robot = robot;
        Follower = follower;
    }

    public void Start()
    {
        webSocketServer = new WebSocketServer("ws://localhost:8080");
        webSocketServer.AddWebSocketService<UIServer>("/", () => server);
        webSocketServer.Start();

        server.Closed += e => OnClose?.Invoke();

        Robot.Line.Subscribe(line =>
        {
            var json = JsonSerializer.Serialize(new
            {
                type = "location",
                value = line.Points[0].X,
                time = DateTime.Now
            });

            server.BroadcastMessage(json);
        });

        Follower.WheelSpeed.Subscribe(speed =>
        {
            var json = JsonSerializer.Serialize(new
            {
                type = "speed",
                value = new
                {
                    right = speed.Item1,
                    left = speed.Item2
                },
                time = DateTime.Now
            });

            server.BroadcastMessage(json);
        });
    }

    public void Dispose()
    {
        webSocketServer?.Stop();
    }

    private class UIServer : WebSocketBehavior
    {
        public event Action<CloseEventArgs>? Closed;

        public Feed<float> BaseSpeed { get; } = new();
        public Feed<float> TargetX { get; } = new();
        public Feed<float> PSensitivity { get; } = new();
        public Feed<float> ISensitivity { get; } = new();
        public Feed<float> DSensitivity { get; } = new();
        public Feed<float> LookAheadSensitivityDropoff { get; } = new();

        protected override void OnMessage(MessageEventArgs e)
        {
            if (e.IsText)
            {
                var data = JsonDocument.Parse(e.Data).RootElement;

                var type = data.GetProperty("type").GetString();

                if (type == "baseSpeed")
                {
                    BaseSpeed.Notify(data.GetProperty("value").GetSingle());
                }
                else if (type == "targetX")
                {
                    TargetX.Notify(data.GetProperty("value").GetSingle());
                }
                else if (type == "pSensitivity")
                {
                    PSensitivity.Notify(data.GetProperty("value").GetSingle());
                }
                else if (type == "iSensitivity")
                {
                    ISensitivity.Notify(data.GetProperty("value").GetSingle());
                }
                else if (type == "dSensitivity")
                {
                    DSensitivity.Notify(data.GetProperty("value").GetSingle());
                }
                else if (type == "lookAheadSensitivityDropoff")
                {
                    LookAheadSensitivityDropoff.Notify(data.GetProperty("value").GetSingle());
                }
            }
        }

        protected override void OnClose(CloseEventArgs e)
        {
            if (Sessions.Count == 0) Closed?.Invoke(e);
        }

        public void BroadcastMessage(string msg)
        {
            Send(msg);
        }
    }
}