using System.Reactive.Linq;
using RoboMaster;
using Stateless;

using PathFinding;

// var trackText = await File.ReadAllTextAsync("track.txt");
// var track = TrackParser.ParseTrack(trackText);

// var route = RoutePlanner.FindRoute(track["1"], track["5"]);
var route = new List<ConnectionType>() { ConnectionType.Forward, ConnectionType.Forward, ConnectionType.BlueLine, ConnectionType.Left, ConnectionType.BlueLine, ConnectionType.BlueLine, ConnectionType.CollectBox };

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

        if (currentConnection == ConnectionType.CollectBox)
        {
            await robotState.SafeFireAsync(RobotTrigger.CollectBox);
        }
        else if (currentConnection == ConnectionType.BlueLine)
        {
            await robotState.SafeFireAsync(RobotTrigger.FollowBlueLine);
        }
        else
        {
            await robot.Move(0, 0, currentConnection switch
            {
                ConnectionType.Left => -90,
                ConnectionType.Right => 90,
                _ => 0
            });

            await Task.Delay(2000);

            await robot.Move(0.4f, 0, 0);

            await Task.Delay(2000);

            await robotState.SafeFireAsync(RobotTrigger.FinishedNavigatingIntersection);
        }
    }))

    .Permit(RobotTrigger.CollectBox, RobotState.CollectingBox)
    .Permit(RobotTrigger.FollowBlueLine, RobotState.FollowingBlueLine)
    .Permit(RobotTrigger.FinishedNavigatingIntersection, RobotState.FollowingRedLine);

robotState.Configure(RobotState.FollowingBlueLine)
    .OnEntryAsync(async () =>
    {
        await robot.Move(0.2f, 0, 0);
        await Task.Delay(2000);

        var actions = new Actions(robot, robotState);

        // actions.LookForObstacles();
        await actions.FollowLine(LineColour.Blue);
    })

    .OnExitAsync(async () =>
    {
        await robot.Move(0.2f, 0, 0);
        await Task.Delay(2000);
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
        await Task.Delay(2000);
        await robot.Move(0.1f, 0, 0);
        await Task.Delay(2000);

        var actions = new Actions(robot, robotState);
        actions.LookForObstacles(4);
        await actions.FollowLine(LineColour.Red);
    }))

    .OnExitAsync(async () => await robot.SetWheelSpeed(0))

    .Permit(RobotTrigger.ObstacleTooClose, RobotState.GrabbingBox)
    .Permit(RobotTrigger.NoLineDetected, RobotState.GrabbingBox)
    .Permit(RobotTrigger.IntersectionDetected, RobotState.GrabbingBox);

robotState.Configure(RobotState.GrabbingBox)
    .SubstateOf(RobotState.CollectingBox)

    .OnEntry(() => Task.Run(async () =>
    {
        await robot.MoveArm(86, 100);
        await robot.CloseGripper();
        await robot.MoveArm(72, 174);

        await robotState.SafeFireAsync(RobotTrigger.GrabbedBox);
    }))

    .Permit(RobotTrigger.GrabbedBox, RobotState.ReturningWithBox);

robotState.Configure(RobotState.ReturningWithBox)
    .SubstateOf(RobotState.CollectingBox)

    .OnEntry(() => Task.Run(async () =>
    {
        await robot.Move(0, 0, 180);
        await Task.Delay(2000);

        var actions = new Actions(robot, robotState);

        await actions.FollowLine(LineColour.Red);
    }))

    .OnExitAsync(async () =>
    {
        await robot.SetWheelSpeed(0);
        await robot.Move(0, 0, 90);
        await Task.Delay(2000);
    })

    .Permit(RobotTrigger.IntersectionDetected, RobotState.FollowingRedLine)
    .Permit(RobotTrigger.NoLineDetected, RobotState.FollowingRedLine);

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

Console.WriteLine("Paused. Press any key to start...");
await Task.WhenAny(Task.Run(Console.ReadKey));

await robot.SetLineRecognitionEnabled();
await robot.SetIrEnabled();
await robotState.FireAsync(RobotTrigger.Resume);

Console.WriteLine("Press any key to stop...");
await Task.WhenAny(Task.Run(Console.ReadKey));

await robotState.SafeFireAsync(RobotTrigger.Pause);

robot.Dispose();
