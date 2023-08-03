using System.Text.Json;
using System.Reactive.Linq;
using WebSocketSharp;
using WebSocketSharp.Server;
using RoboMaster;

public class UI : IDisposable
{
    public event Action? OnClose;
    public event Action? Pause;
    public event Action? Resume;

    public Feed<float> BaseSpeed => server.BaseSpeed;
    public Feed<float> TargetX => server.TargetX;
    public Feed<float> PSensitivity => server.PSensitivity;
    public Feed<float> ISensitivity => server.ISensitivity;
    public Feed<float> DSensitivity => server.DSensitivity;

    public RoboMasterClient Robot { get; }
    public Follower Follower { get; }

    private WebSocketServer? webSocketServer;
    private UIServer server = new();

    private bool paused = false;

    public UI(RoboMasterClient robot, Follower follower)
    {
        Robot = robot;
        Follower = follower;
    }

    public void Start()
    {
        server.Init += () =>
        {
            server.BroadcastMessage(JsonSerializer.Serialize(new
            {
                type = "baseSpeed",
                value = Follower.BaseWheelSpeed
            }));

            server.BroadcastMessage(JsonSerializer.Serialize(new
            {
                type = "targetX",
                value = Follower.TargetX
            }));

            server.BroadcastMessage(JsonSerializer.Serialize(new
            {
                type = "pSensitivity",
                value = Follower.PSensitivity
            }));

            server.BroadcastMessage(JsonSerializer.Serialize(new
            {
                type = "iSensitivity",
                value = Follower.ISensitivity
            }));

            server.BroadcastMessage(JsonSerializer.Serialize(new
            {
                type = "dSensitivity",
                value = Follower.DSensitivity
            }));
        };

        server.Closed += _ => OnClose?.Invoke();

        server.Pause += () => Pause?.Invoke();
        server.Pause += () => paused = true;

        server.Resume += () => Resume?.Invoke();
        server.Resume += () => paused = false;

        webSocketServer = new WebSocketServer("ws://localhost:8080");
        webSocketServer.AddWebSocketService("/", () => server);
        webSocketServer.Start();

        Robot.Line.SkipWhile((_, _) => paused).Sample(new TimeSpan(0, 0, 0, 0, 100)).Subscribe(line =>
        {
            if (line.Points.Length > 0)
            {
                var json = JsonSerializer.Serialize(new
                {
                    type = "location",
                    value = line.Points[0].X,
                    time = DateTime.Now
                });

                server.BroadcastMessage(json);
            }
            else
            {
                var json = JsonSerializer.Serialize(new
                {
                    type = "location",
                    value = 0,
                    time = DateTime.Now
                });

                server.BroadcastMessage(json);
            }
        });

        Follower.WheelSpeed.SkipWhile((_, _) => paused).Sample(new TimeSpan(0, 0, 0, 0, 100)).Subscribe(speed =>
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

        Follower.PError.Zip(Follower.IError, Follower.DError).SkipWhile(_ => paused).Sample(new TimeSpan(0, 0, 0, 0, 100)).Subscribe(error =>
        {
            var json = JsonSerializer.Serialize(new
            {
                type = "error",
                value = new
                {
                    p = error.First,
                    i = error.Second,
                    d = error.Third
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
        public event Action? Init;
        public event Action<CloseEventArgs>? Closed;
        public event Action? Pause;
        public event Action? Resume;

        public Feed<float> BaseSpeed { get; } = new();
        public Feed<float> TargetX { get; } = new();
        public Feed<float> PSensitivity { get; } = new();
        public Feed<float> ISensitivity { get; } = new();
        public Feed<float> DSensitivity { get; } = new();
        public Feed<float> LookAheadSensitivityDropoff { get; } = new();

        protected override void OnOpen()
        {
            Init?.Invoke();
        }

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
                else if (type == "pause")
                {
                    Pause?.Invoke();
                }
                else if (type == "resume")
                {
                    Resume?.Invoke();
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