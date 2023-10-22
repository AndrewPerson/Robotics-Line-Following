using System.Reactive.Linq;
using System.Diagnostics;

using Polly;

using RoboMaster;

using PathFinding;

var robot = await RoboMasterClient.Connect(RoboMasterClient.DIRECT_CONNECT_IP);
var actions = new Actions(robot);

Console.Write("Enter direct control mode? (y/n, default n): ");

if (Console.ReadLine() == "y")
{
    Console.WriteLine();
    Console.WriteLine("Direct control mode enabled");

    Console.Write("Enter command: ");
    var command = (Console.ReadLine() ?? "").Split(' ');

    while (command.Length != 0)
    {
        await robot.Do(command.Select(arg => new CommandArg(arg)).ToArray());

        Console.Write("Enter command: ");
        command = (Console.ReadLine() ?? "").Split(' ');
    }

    return;
}

var trackText = await File.ReadAllTextAsync("track.txt");
var track = TrackParser.ParseTrack(trackText);

var route = GetUserRouteInput(track);
Console.WriteLine($"Route: {string.Join(", ", route)}");

var intersectionCount = -1;

await robot.OpenGripper();

Console.WriteLine("Paused. Press any key to start...");
await Task.Run(Console.ReadKey);

await robot.SetLineRecognitionEnabled();
await robot.SetIrEnabled();

while (true)
{
    await Policy
        .Handle<NoLineException>().Or<ObstacleTooCloseException>()
        .RetryForeverAsync(async ex =>
        {
            await robot.SetWheelSpeed(0);

            if (ex is NoLineException) await actions.FindLine(LineColour.Red);
            else if (ex is ObstacleTooCloseException) await actions.LookForNoObstacles();
        })
        .ExecuteAsync(async () => await actions.FollowLine(LineColour.Red));

    intersectionCount++;
    Console.WriteLine(intersectionCount);

    if (intersectionCount == route.Count)
    {
        await robot.SetWheelSpeed(0);

        route = GetUserRouteInput(track);
        Console.WriteLine($"Route: {string.Join(", ", route)}");

        intersectionCount = 0;
    }

    var (currentConnection, _) = route[intersectionCount];
    var currentCorrection = intersectionCount == 0 ? CorrectionType.None : route[intersectionCount - 1].Item2;

    Console.WriteLine(currentConnection);

    if (currentCorrection == CorrectionType.Left)
    {
        await robot.MoveForward(30);
        await robot.Move(0, 0, -90);
        await Task.Delay(3000);
        await actions.FindLineHorizontally(LineColour.Red, 0.4f);
    }
    else if (currentCorrection == CorrectionType.Right)
    {
        await robot.MoveForward(30);
        await robot.Move(0, 0, 90);
        await Task.Delay(3000);
        await actions.FindLineHorizontally(LineColour.Red, 0.4f);
    }

    if (currentConnection == ConnectionType.CollectBox)
    {
        await actions.MoveToDepot();
        
        await robot.SetArmPosition(180, 100);
        await Task.Delay(1000);

        await robot.CloseGripper();
        await Task.Delay(2000);

        await robot.SetArmPosition(73, 159);
        await Task.Delay(1000);

        await actions.ReturnFromDepot();
    }
    else if (currentConnection == ConnectionType.DropBox)
    {
        await actions.MoveToDepot();
        
        await robot.SetArmPosition(180, 100);
        await Task.Delay(1000);

        await robot.OpenGripper();
        await Task.Delay(2000);

        await robot.SetArmPosition(73, 159);
        await Task.Delay(1000);

        await actions.ReturnFromDepot();
    }
    else if (currentConnection == ConnectionType.BlueLine)
    {
        await Policy
            .Handle<ObstacleTooCloseException>()
            .RetryForeverAsync(async ex =>
            {
                await robot.SetWheelSpeed(0);

                if (ex is ObstacleTooCloseException) await actions.LookForNoObstacles();
            })
            .WrapAsync(Policy.Handle<NoLineException>().FallbackAsync((_) => Task.CompletedTask))
            .ExecuteAsync(async () => await actions.FollowLine(LineColour.Blue));
    }
    else
    {
        await robot.MoveForward(currentConnection switch
        {
            ConnectionType.Left or ConnectionType.Right => 30,
            _ => 0
        });

        await robot.Move(0, 0, currentConnection switch
        {
            ConnectionType.Left => -90,
            ConnectionType.Right => 90,
            _ => 0
        });

        await Task.Delay(currentConnection switch
        {
            ConnectionType.Left or ConnectionType.Right => 3000,
            _ => 0
        });
    }
}

static List<(ConnectionType, CorrectionType)> GetUserRouteInput(Dictionary<string, PathFinding.AStar.Node<(ConnectionType, CorrectionType)>> track)
{
    Console.Write("Enter start intersection: ");
    var start = Console.ReadLine();

    Debug.Assert(start != null, "Start intersection name is null");
    Debug.Assert(track.ContainsKey(start), "Start intersection not found");

    Console.Write("Enter pickup intersection: ");
    var pickup = Console.ReadLine();

    Debug.Assert(pickup != null, "Pickup intersection name is null");
    Debug.Assert(track.ContainsKey(pickup), "Pickup intersection not found");

    Console.Write("Enter drop intersection: ");
    var drop = Console.ReadLine();

    Debug.Assert(drop != null, "Drop intersection name is null");
    Debug.Assert(track.ContainsKey(drop), "Drop intersection not found");

    return RoutePlanner.FindRoute(track[start], track[pickup], track[drop]);
}