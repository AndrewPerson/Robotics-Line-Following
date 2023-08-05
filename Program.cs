using System.Reactive.Linq;
using RoboMaster;
using Stateless;

var robot = await RoboMasterClient.Connect(RoboMasterClient.DIRECT_CONNECT_IP);

#region State Machine
var robotState = new StateMachine<RobotState, RobotTrigger>(RobotState.RedStopped);

robotState.Configure(RobotState.FollowingLine)
    .Ignore(RobotTrigger.LineDetected)
    .Ignore(RobotTrigger.NoObstacle)
    .Ignore(RobotTrigger.Resume);

robotState.Configure(RobotState.FollowingRedLine)
    .SubstateOf(RobotState.FollowingLine)

    .OnEntryAsync(async () => await robot.SetLineRecognitionColour(LineColour.Red))
    .OnEntry(() => Actions.FollowLine(robot, robotState))

    .Permit(RobotTrigger.TooCloseToObstacle, RobotState.RedStopped)
    .Permit(RobotTrigger.NoLineDetected, RobotState.RedStopped)
    .Permit(RobotTrigger.Pause, RobotState.RedStopped)
    .Permit(RobotTrigger.IntersectionDetected, RobotState.FollowingBlueLine);

robotState.Configure(RobotState.FollowingBlueLine)
    .SubstateOf(RobotState.FollowingLine)

    .OnEntryAsync(async () => await robot.SetLineRecognitionColour(LineColour.Blue))
    .OnEntry(() => Actions.FollowLine(robot, robotState))

    .Permit(RobotTrigger.IntersectionDetected, RobotState.BlueStopped)
    .Permit(RobotTrigger.TooCloseToObstacle, RobotState.BlueStopped)
    .Permit(RobotTrigger.Pause, RobotState.BlueStopped)
    .Permit(RobotTrigger.NoLineDetected, RobotState.FollowingRedLine);

robotState.Configure(RobotState.Stopped)
    .OnEntryAsync(async () => await Actions.Stop(robot))
    .OnEntry(() => Actions.LookForLine(robot, robotState))

    .Ignore(RobotTrigger.NoLineDetected)
    .Ignore(RobotTrigger.IntersectionDetected)
    .Ignore(RobotTrigger.TooCloseToObstacle)
    .Ignore(RobotTrigger.Pause);

robotState.Configure(RobotState.RedStopped)
    .SubstateOf(RobotState.Stopped)

    .Permit(RobotTrigger.LineDetected, RobotState.FollowingRedLine)
    .Permit(RobotTrigger.NoObstacle, RobotState.FollowingRedLine)
    .Permit(RobotTrigger.Resume, RobotState.FollowingRedLine);

robotState.Configure(RobotState.BlueStopped)
    .SubstateOf(RobotState.Stopped)

    .Permit(RobotTrigger.LineDetected, RobotState.FollowingBlueLine)
    .Permit(RobotTrigger.NoObstacle, RobotState.FollowingBlueLine)
    .Permit(RobotTrigger.Resume, RobotState.FollowingBlueLine);
#endregion

Actions.LookForObstacles(robot, robotState);

await Task.WhenAny(Task.Run(Console.ReadKey));

await robotState.FireAsync(RobotTrigger.Pause);

robot.Dispose();
