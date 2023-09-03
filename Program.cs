using System.Reactive.Linq;
using System.Diagnostics;

using Polly;

using RoboMaster;

using PathFinding;

var robot = await RoboMasterClient.Connect(RoboMasterClient.DIRECT_CONNECT_IP);
var actions = new Actions(robot);

Console.Write("Enter direct control mode? (y/n, default n): ");

if (Console.ReadKey().KeyChar == 'y')
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
    #region Follow Red Line
    {
        await Policy
            .Handle<NoLineException>().Or<ObstacleTooCloseException>()
            .RetryForeverAsync(async ex =>
            {
                if (ex is NoLineException) await actions.FindLine(LineColour.Red);
                else if (ex is ObstacleTooCloseException) await actions.LookForNoObstacles();
            })
            .ExecuteAsync(async () => await actions.FollowLine(LineColour.Red));
    }
    #endregion

    intersectionCount++;

    if (intersectionCount == route.Count)
    {
        route = GetUserRouteInput(track);
        Console.WriteLine($"Route: {string.Join(", ", route)}");

        intersectionCount = 0;
    }

    var currentConnection = route[intersectionCount];

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
            .Handle<NoLineException>().Or<ObstacleTooCloseException>()
            .RetryForeverAsync(async ex =>
            {
                if (ex is NoLineException) await actions.FindLine(LineColour.Blue);
                else if (ex is ObstacleTooCloseException) await actions.LookForNoObstacles();
            })
            .ExecuteAsync(async () => await actions.FollowLine(LineColour.Blue));
    }
    else
    {
        await robot.Move(currentConnection switch
        {
            ConnectionType.Left or ConnectionType.Right => 0.3f,
            ConnectionType.Forward => 0.2f,
            _ => 0
        }, 0, 0);

        await Task.Delay(currentConnection switch
        {
            ConnectionType.Left or ConnectionType.Right => 3000,
            ConnectionType.Forward => 2000,
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

static List<ConnectionType> GetUserRouteInput(Dictionary<string, PathFinding.AStar.Node<ConnectionType>> track)
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