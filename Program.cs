using System.Reactive.Linq;
using RoboMaster;
using Stateless;

var robot = await RoboMasterClient.Connect(RoboMasterClient.DIRECT_CONNECT_IP);

#region State Machine
var robotState = new StateMachine<RobotState, RobotTrigger>(RobotState.RedStopped);

robotState.Configure(RobotState.FollowingRedLine)
    .OnEntryAsync(async () => await robot.SetLineRecognitionColour(LineColour.Red))
    .OnEntry(() => Actions.FollowLine(robot, robotState))

    .Permit(RobotTrigger.NoLineDetected, RobotState.RedStopped)
    .Permit(RobotTrigger.ObstacleTooClose, RobotState.RedStopped)
    .Permit(RobotTrigger.Pause, RobotState.RedStopped)
    .Permit(RobotTrigger.IntersectionDetected, RobotState.FollowingBlueLine);

robotState.Configure(RobotState.FollowingBlueLine)
    .OnEntryAsync(async () => await robot.SetLineRecognitionColour(LineColour.Blue))
    .OnEntry(() => Actions.FollowLine(robot, robotState))

    .Permit(RobotTrigger.ObstacleTooClose, RobotState.BlueStopped)
    .Permit(RobotTrigger.Pause, RobotState.BlueStopped)
    .Permit(RobotTrigger.NoLineDetected, RobotState.FollowingRedLine)
    .Permit(RobotTrigger.IntersectionDetected, RobotState.FollowingRedLine);

var stoppedReasons = new HashSet<RobotTrigger>() { RobotTrigger.Pause };
robotState.Configure(RobotState.Stopped)
    .OnEntryAsync(async () => await Actions.Stop(robot))

    .OnExit(stoppedReasons.Clear)
    
    .OnEntryFrom(RobotTrigger.NoLineDetected, () => Actions.LookForLine(robot, robotState))

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

Actions.LookForObstacles(robot, robotState);
await robotState.FireAsync(RobotTrigger.Resume);

Console.WriteLine("Press any key to stop...");
await Task.WhenAny(Task.Run(Console.ReadKey));

await robotState.SafeFireAsync(RobotTrigger.Pause);

robot.Dispose();
