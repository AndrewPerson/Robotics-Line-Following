using System.Reactive.Linq;
using System.Diagnostics;

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

var route = RoutePlanner.FindRoute(track[start], track[pickup], track[drop]);
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
        var cancellationTokenSource = new CancellationTokenSource();
        
        var stoppedReason = await await Task.WhenAny
        (
            actions.FollowLine(LineColour.Red, cancellationToken: cancellationTokenSource.Token),
            actions.LookForObstacles(cancellationToken: cancellationTokenSource.Token)
        );

        cancellationTokenSource.Cancel();
        
        while (stoppedReason == StopReason.TooClose || stoppedReason == StopReason.NoLine)
        {
            if (stoppedReason == StopReason.TooClose)
            {
                await actions.LookForNoObstacles();
            }
            else
            {
                await actions.FindLine(LineColour.Red);
            }

            cancellationTokenSource = new CancellationTokenSource();
        
            stoppedReason = await await Task.WhenAny
            (
                actions.FollowLine(LineColour.Red, cancellationToken: cancellationTokenSource.Token),
                actions.LookForObstacles(cancellationToken: cancellationTokenSource.Token)
            );

            cancellationTokenSource.Cancel();
        }
    }
    #endregion

    intersectionCount++;

    if (intersectionCount == route.Count) break;

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
        var cancellationTokenSource = new CancellationTokenSource();

        var stoppedReason = await await Task.WhenAny
        (
            actions.FollowLine(LineColour.Red, cancellationToken: cancellationTokenSource.Token),
            actions.LookForObstacles(cancellationToken: cancellationTokenSource.Token)
        );

        cancellationTokenSource.Cancel();

        while (stoppedReason == StopReason.TooClose || stoppedReason == StopReason.NoLine)
        {
            if (stoppedReason == StopReason.TooClose)
            {
                await actions.LookForNoObstacles();
            }
            else
            {
                await actions.FindLine(LineColour.Red);
            }

            cancellationTokenSource = new CancellationTokenSource();

            stoppedReason = await await Task.WhenAny
            (
                actions.FollowLine(LineColour.Red, cancellationToken: cancellationTokenSource.Token),
                actions.LookForObstacles(cancellationToken: cancellationTokenSource.Token)
            );

            cancellationTokenSource.Cancel();
        }
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
