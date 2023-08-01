using System.Text;
using System.Reactive.Linq;
using RoboMaster;
using OpenCvSharp;

var ui = new UI();

var uiCompletion = new TaskCompletionSource();
ui.OnClose += () => uiCompletion.SetResult();

var uiThread = new Thread(() =>
{
    ui.Run();
});

uiThread.IsBackground = true;
uiThread.Start();

var robot = await RoboMasterClient.Connect(RoboMasterClient.DIRECT_CONNECT_IP);

var follower = new Follower(robot);

ui.Robot = robot;
ui.Follower = follower;

CancellationTokenSource? previousWheelSpeedCanceller = null;

follower.WheelSpeed.Subscribe(speed =>
{
    var wheelSpeedCanceller = new CancellationTokenSource();
    Task.Run(() => robot.SetWheelSpeed(speed.Item1, speed.Item2, wheelSpeedCanceller.Token));

    previousWheelSpeedCanceller?.Cancel();

    previousWheelSpeedCanceller = wheelSpeedCanceller;
});

var irFeed = new Feed<float>();

var irThread = new Thread(async () =>
{
    await robot.SetIrEnabled();
    while (true)
    {
        irFeed.Notify(await robot.GetIRDistance(1));
    }
});

irThread.IsBackground = true;
irThread.Start();

robot.Line.CombineLatest(irFeed)
            .Select(data => new FollowerData(data.First, data.Second))
            .Subscribe(follower);

var lineFile = File.Open("line.csv", FileMode.OpenOrCreate);
robot.Line.Subscribe(line =>
{
    if (line.Points.Length == 0)
    {
        lineFile.WriteAsync(Encoding.Default.GetBytes("-1\n"));
    }
    else
    {
        lineFile.WriteAsync(Encoding.Default.GetBytes($"{line.Points[0].X.ToString()}\n"));
    }
});

robot.Video.Subscribe(frame =>
{
    Cv2.ImShow("Robot Camera", frame);
});

await robot.SetLineRecognitionColour(LineColour.Red);
await robot.SetLineRecognitionEnabled();

Console.WriteLine("Press any key to stop...");

await Task.WhenAny(Task.Run(Console.ReadKey), uiCompletion.Task);

ui.Dispose();
robot.Dispose();
