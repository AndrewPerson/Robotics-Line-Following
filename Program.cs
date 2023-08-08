using System.Reactive.Linq;
using RoboMaster;
using Stateless;

var robot = await RoboMasterClient.Connect(RoboMasterClient.DIRECT_CONNECT_IP);

#region State Machine
var robotState = new StateMachine<RobotState, RobotTrigger>(RobotState.RedStopped);

var intersectionCount = 0;
robotState.OnTransitioned(transition =>
{
    if (transition.Trigger == RobotTrigger.IntersectionDetected) intersectionCount++;
});

robotState.Configure(RobotState.FollowingRedLine)
    .OnEntryAsync(async () =>
    {
        var actions = new Actions(robot, robotState);

        actions.LookForObstacles();
        await actions.FollowLine(LineColour.Red);
    })

    .Permit(RobotTrigger.NoLineDetected, RobotState.RedStopped)
    .Permit(RobotTrigger.ObstacleTooClose, RobotState.RedStopped)
    .Permit(RobotTrigger.Pause, RobotState.RedStopped)
    .Permit(RobotTrigger.IntersectionDetected, RobotState.FollowingBlueLine);

robotState.Configure(RobotState.FollowingBlueLine)
    .OnEntryAsync(async () =>
    {
        var actions = new Actions(robot, robotState);

        actions.LookForObstacles();
        await actions.FollowLine(LineColour.Blue);
    })

    .Permit(RobotTrigger.ObstacleTooClose, RobotState.BlueStopped)
    .Permit(RobotTrigger.Pause, RobotState.BlueStopped)
    .Permit(RobotTrigger.NoLineDetected, RobotState.FollowingRedLine)
    .Permit(RobotTrigger.IntersectionDetected, RobotState.FollowingRedLine);

robotState.Configure(RobotState.CollectingBox);

robotState.Configure(RobotState.MovingToBox)
    .SubstateOf(RobotState.CollectingBox)

    .OnEntry(() => Task.Run(async () =>
    {
        var actions = new Actions(robot, robotState);
        actions.LookForObstacles();

        await robot.SetWheelSpeed(10);
    }))

    .OnExitAsync(async () => await robot.SetWheelSpeed(0))

    .Permit(RobotTrigger.ObstacleTooClose, RobotState.GrabbingBox);

robotState.Configure(RobotState.GrabbingBox)
    .SubstateOf(RobotState.CollectingBox)

    .OnEntry(() => Task.Run(async () =>
    {
        // TODO
        await robotState.SafeFireAsync(RobotTrigger.GrabbedBox);
    }))

    .Permit(RobotTrigger.GrabbedBox, RobotState.ReturningWithBox);

robotState.Configure(RobotState.ReturningWithBox)
    .SubstateOf(RobotState.CollectingBox)

    .OnEntry(() => Task.Run(async () =>
    {
        await robot.Move(50, 0, 0);
        await robot.Move(0, 0, 90);
        await robot.Move(50, 0, 0);

        await robotState.FireAsync(RobotTrigger.ReturnedWithBox);
    }))

    .Permit(RobotTrigger.ReturnedWithBox, RobotState.FollowingRedLine);

var stoppedReasons = new HashSet<RobotTrigger>() { RobotTrigger.Pause };
robotState.Configure(RobotState.Stopped)
    .OnEntryAsync(async () => await new Actions(robot, robotState).Stop())

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
await robotState.FireAsync(RobotTrigger.Resume);

Console.WriteLine("Press any key to stop...");
await Task.WhenAny(Task.Run(Console.ReadKey));

await robotState.SafeFireAsync(RobotTrigger.Pause);

robot.Dispose();
