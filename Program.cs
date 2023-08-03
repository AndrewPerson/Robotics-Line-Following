﻿using System.Text;
using System.Reactive.Linq;
using RoboMaster;
using OpenCvSharp;

var robot = await RoboMasterClient.Connect(RoboMasterClient.DIRECT_CONNECT_IP);
var follower = new Follower(robot);

var isPaused = new Feed<bool>();

#region UI
var ui = new UI(robot, follower);

var uiCompletion = new TaskCompletionSource();
ui.OnClose += () => uiCompletion.SetResult();

ui.BaseSpeed.Subscribe(speed => follower.BaseWheelSpeed = speed);
ui.TargetX.Subscribe(targetX => follower.TargetX = targetX);
ui.PSensitivity.Subscribe(sensitivity => follower.PSensitivity = sensitivity);
ui.ISensitivity.Subscribe(sensitivity => follower.ISensitivity = sensitivity);
ui.DSensitivity.Subscribe(sensitivity => follower.DSensitivity = sensitivity);

ui.Pause += () => isPaused.Notify(true);
ui.Resume += () => isPaused.Notify(false);

ui.Start();

System.Diagnostics.Process.Start("http://localhost:5500");
#endregion

#region Wheel Speeds
CancellationTokenSource? previousWheelSpeedCanceller = null;

follower.WheelSpeed.Subscribe(speed =>
{
    var wheelSpeedCanceller = new CancellationTokenSource();
    Task.Run(() => robot.SetWheelSpeed(speed.Item1, speed.Item2, wheelSpeedCanceller.Token));

    previousWheelSpeedCanceller?.Cancel();

    previousWheelSpeedCanceller = wheelSpeedCanceller;
});
#endregion

#region IR Distance
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
#endregion

#region Line Following
robot.Line.CombineLatest(irFeed, isPaused)
            .Select(data => new FollowerData(data.First, data.Second, data.Third))
            .Subscribe(follower);
#endregion

#region Line Saving
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
#endregion

#region Video
robot.Video.Subscribe(frame =>
{
    Cv2.ImShow("Robot Camera", frame);
});
#endregion

await robot.SetLineRecognitionColour(LineColour.Red);
await robot.SetLineRecognitionEnabled();

Console.WriteLine("Press any key to stop...");

await Task.WhenAny(Task.Run(Console.ReadKey), uiCompletion.Task);

ui.Dispose();
robot.Dispose();
