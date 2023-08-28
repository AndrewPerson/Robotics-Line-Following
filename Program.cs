using System.Reactive.Linq;
using System.Diagnostics;

using RoboMaster;
using Stateless;

using PathFinding;

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

var intersectionCount = 0;
var currentConnection = route[0];

var robot = await RoboMasterClient.Connect(RoboMasterClient.DIRECT_CONNECT_IP);

#region State Machine
var robotState = new StateMachine<RobotState, RobotTrigger>(RobotState.RedStopped);

robotState.OnTransitioned(transition =>
{
    Console.WriteLine($"Transitioning to {transition.Destination} because ${transition.Trigger}");
});

robotState.Configure(RobotState.FollowingRedLine)
    .OnEntryAsync(async () =>
    {
        var actions = new Actions(robot, robotState);

        // actions.LookForObstacles();
        await actions.FollowLine(LineColour.Red);
    })

    .OnExit(transition =>
    {
        if (transition.Trigger == RobotTrigger.IntersectionDetected)
        {
            currentConnection = route[intersectionCount];
            intersectionCount++;
        }
    })
    .OnExitAsync(async () => await robot.SetWheelSpeed(0))

    .Permit(RobotTrigger.NoLineDetected, RobotState.RedStopped)
    .Permit(RobotTrigger.ObstacleTooClose, RobotState.RedStopped)
    .Permit(RobotTrigger.Pause, RobotState.RedStopped)
    .Permit(RobotTrigger.IntersectionDetected, RobotState.NavigatingIntersection);

robotState.Configure(RobotState.NavigatingIntersection)
    .OnEntry(() => Task.Run(async () =>
    {
        Console.WriteLine(currentConnection);
        Console.WriteLine(intersectionCount);

        if (currentConnection == ConnectionType.CollectBox)
        {
            _ = Task.Run(() => robotState.SafeFireAsync(RobotTrigger.CollectBox));
        }
        else if (currentConnection == ConnectionType.DropBox)
        {
            _ = Task.Run(() => robotState.SafeFireAsync(RobotTrigger.DropBox));
        }
        else if (currentConnection == ConnectionType.BlueLine)
        {
            _ = Task.Run(() => robotState.SafeFireAsync(RobotTrigger.FollowBlueLine));
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

            Console.WriteLine("I HAVE FINISHED MOVING!");

            _ = Task.Run(() => robotState.SafeFireAsync(RobotTrigger.FinishedNavigatingIntersection));
        }
    }))

    .Permit(RobotTrigger.CollectBox, RobotState.CollectingBox)
    .Permit(RobotTrigger.DropBox, RobotState.DroppingBox)
    .Permit(RobotTrigger.FollowBlueLine, RobotState.FollowingBlueLine)
    .Permit(RobotTrigger.FinishedNavigatingIntersection, RobotState.FollowingRedLine);

robotState.Configure(RobotState.FollowingBlueLine)
    .SubstateOf(RobotState.NavigatingIntersection)

    .OnEntryAsync(async () =>
    {
        var actions = new Actions(robot, robotState);

        // actions.LookForObstacles();
        await actions.FollowLine(LineColour.Blue);
    })

    .OnExitAsync(async transition =>
    {
        if (transition.Trigger == RobotTrigger.IntersectionDetected)
        {
            await robot.Move(0.2f, 0, 0);
            await Task.Delay(2000);
        }

        await robot.SetWheelSpeed(0);
    })

    .Permit(RobotTrigger.ObstacleTooClose, RobotState.BlueStopped)
    .Permit(RobotTrigger.Pause, RobotState.BlueStopped)
    .Permit(RobotTrigger.NoLineDetected, RobotState.FollowingRedLine)
    .Permit(RobotTrigger.IntersectionDetected, RobotState.FollowingRedLine);

robotState.Configure(RobotState.CollectingBox)
    .InitialTransition(RobotState.MovingToBox);

robotState.Configure(RobotState.MovingToBox)
    .SubstateOf(RobotState.CollectingBox)

    .OnEntry(() => Task.Run(async () =>
    {
        await robot.Move(0, 0, -90);
        await Task.Delay(3000);

        await robot.Move(0.1f, 0.1f, 0);
        await Task.Delay(2000);

        await Utils.CenterOnLine(robot, LineColour.Red, 0.35f, 0.05f);

        var actions = new Actions(robot, robotState);

        actions.LookForObstacles(15);
        await actions.FollowLine(LineColour.Red, 40);
    }))

    .OnExitAsync(async () => await robot.SetWheelSpeed(0))

    .Permit(RobotTrigger.ObstacleTooClose, RobotState.GrabbingBox)
    .Permit(RobotTrigger.NoLineDetected, RobotState.GrabbingBox)
    .Permit(RobotTrigger.IntersectionDetected, RobotState.GrabbingBox);

robotState.Configure(RobotState.GrabbingBox)
    .SubstateOf(RobotState.CollectingBox)

    .OnEntry(() => Task.Run(async () =>
    {
        await robot.SetArmPosition(160, 100);
        await Task.Delay(2000);

        await robot.CloseGripper();
        await Task.Delay(2000);

        await robot.SetArmPosition(73, 159);
        await Task.Delay(2000);

        _ = Task.Run(() => robotState.SafeFireAsync(RobotTrigger.GrabbedBox));
    }))

    .Permit(RobotTrigger.GrabbedBox, RobotState.ReturningWithBox);

robotState.Configure(RobotState.ReturningWithBox)
    .SubstateOf(RobotState.CollectingBox)

    .OnEntry(() => Task.Run(async () =>
    {
        await robot.Move(0, 0, 180);
        await Task.Delay(10000);

        await robot.Move(0.3f, 0, 0);
        await Task.Delay(2000);

        await robot.Move(0, 0, -90);
        await Task.Delay(3000);

        _ = Task.Run(() => robotState.SafeFireAsync(RobotTrigger.ReturnedWithBox));
    }))

    .Permit(RobotTrigger.ReturnedWithBox, RobotState.FollowingRedLine);

robotState.Configure(RobotState.DroppingBox)
    .InitialTransition(RobotState.MovingToDropPoint);

robotState.Configure(RobotState.MovingToDropPoint)
    .SubstateOf(RobotState.DroppingBox)

    .OnEntry(() => Task.Run(async () =>
    {
        await robot.Move(0, 0, -90);
        await Task.Delay(3000);

        await robot.Move(0.1f, 0.1f, 0);
        await Task.Delay(2000);

        await Utils.CenterOnLine(robot, LineColour.Red, 0.35f, 0.05f);

        var actions = new Actions(robot, robotState);

        actions.LookForObstacles(15);
        await actions.FollowLine(LineColour.Red, 40);
    }))

    .OnExitAsync(async () => await robot.SetWheelSpeed(0))

    .Permit(RobotTrigger.ObstacleTooClose, RobotState.PlacingBox)
    .Permit(RobotTrigger.NoLineDetected, RobotState.PlacingBox)
    .Permit(RobotTrigger.IntersectionDetected, RobotState.PlacingBox);

robotState.Configure(RobotState.PlacingBox)
    .SubstateOf(RobotState.DroppingBox)

    .OnEntry(() => Task.Run(async () =>
    {
        await robot.SetArmPosition(160, 100);
        await Task.Delay(2000);

        await robot.OpenGripper();
        await Task.Delay(2000);

        await robot.SetArmPosition(73, 159);
        await Task.Delay(2000);

        _ = Task.Run(() => robotState.SafeFireAsync(RobotTrigger.DroppedBox));
    }))

    .Permit(RobotTrigger.DroppedBox, RobotState.ReturningDropPoint);

robotState.Configure(RobotState.ReturningDropPoint)
    .SubstateOf(RobotState.DroppingBox)

    .OnEntry(() => Task.Run(async () =>
    {
        await robot.Move(0, 0, 180);
        await Task.Delay(10000);

        await robot.Move(0.3f, 0, 0);
        await Task.Delay(2000);

        await robot.Move(0, 0, -90);
        await Task.Delay(3000);

        _ = Task.Run(() => robotState.SafeFireAsync(RobotTrigger.ReturnedFromDropPoint));
    }))

    .Permit(RobotTrigger.ReturnedFromDropPoint, RobotState.FollowingRedLine);

var stoppedReasons = new HashSet<RobotTrigger>() { RobotTrigger.Pause };
robotState.Configure(RobotState.Stopped)
    .OnExit(stoppedReasons.Clear)

    .OnEntryFrom(RobotTrigger.NoLineDetected, () => new Actions(robot, robotState).LookForLine())
    .OnEntryFrom(RobotTrigger.ObstacleTooClose, () => new Actions(robot, robotState).LookForNoObstacles())

    .OnEntryFromAndInternal(RobotTrigger.NoLineDetected, () => stoppedReasons.Add(RobotTrigger.NoLineDetected))
    .OnEntryFromAndInternal(RobotTrigger.ObstacleTooClose, () => stoppedReasons.Add(RobotTrigger.ObstacleTooClose))
    .OnEntryFromAndInternal(RobotTrigger.Pause, () => stoppedReasons.Add(RobotTrigger.Pause))

    .InternalTransitionIf(RobotTrigger.LineDetected, _ => stoppedReasons.Count >= 2, () => stoppedReasons.Remove(RobotTrigger.NoLineDetected))
    .InternalTransitionIf(RobotTrigger.NoObstacles, _ => stoppedReasons.Count >= 2, () => stoppedReasons.Remove(RobotTrigger.ObstacleTooClose))
    .InternalTransitionIf(RobotTrigger.Resume, _ => stoppedReasons.Count >= 2, () => stoppedReasons.Remove(RobotTrigger.Pause));

robotState.Configure(RobotState.RedStopped)
    .SubstateOf(RobotState.Stopped)

    .PermitIf(RobotTrigger.LineDetected, RobotState.FollowingRedLine, () => stoppedReasons.Count == 1 && stoppedReasons.Contains(RobotTrigger.NoLineDetected))
    .PermitIf(RobotTrigger.NoObstacles, RobotState.FollowingRedLine, () => stoppedReasons.Count == 1 && stoppedReasons.Contains(RobotTrigger.ObstacleTooClose))
    .PermitIf(RobotTrigger.Resume, RobotState.FollowingRedLine, () => stoppedReasons.Count == 1 && stoppedReasons.Contains(RobotTrigger.Pause));

robotState.Configure(RobotState.BlueStopped)
    .SubstateOf(RobotState.Stopped)

    .PermitIf(RobotTrigger.NoObstacles, RobotState.FollowingBlueLine, () => stoppedReasons.Count == 1 && stoppedReasons.Contains(RobotTrigger.ObstacleTooClose))
    .PermitIf(RobotTrigger.Resume, RobotState.FollowingBlueLine, () => stoppedReasons.Count == 1 && stoppedReasons.Contains(RobotTrigger.Pause));
#endregion

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
}
else
{
    await robot.OpenGripper();

    Console.WriteLine("Paused. Press any key to start...");
    await Task.WhenAny(Task.Run(Console.ReadKey));

    await robot.SetLineRecognitionEnabled();
    await robot.SetIrEnabled();
    await robotState.FireAsync(RobotTrigger.Resume);

    Console.WriteLine("Press any key to stop...");
    await Task.WhenAny(Task.Run(Console.ReadKey));

    await robotState.SafeFireAsync(RobotTrigger.Pause);
}

robot.Dispose();