using System.Reactive.Linq;
using Stateless;
using RoboMaster;

public static class Utils
{
    public static async Task CenterOnLine(RoboMasterClient robot, LineColour lineColour, float targetX, float margin)
    {
        await robot.SetLineRecognitionColour(lineColour);

        await foreach (var line in robot.Line.ToDroppingAsyncEnumerable())
        {
            var x = line.Points[0].X;

            if (Math.Abs(x - targetX) < margin)
            {
                await robot.SetWheelSpeed(0);
                break;
            }

            // TODO The wheel speeds might be reversed
            if (x < targetX) // Go Left
            {
                await robot.SetWheelSpeed(50, -50, -50, 50);
            }
            else // Go Right
            {
                await robot.SetWheelSpeed(-50, 50, 50, -50);
            }
        }
    }

    public static async Task MoveToDepot(RoboMasterClient robot, StateMachine<RobotState, RobotTrigger> robotState)
    {
        await robot.Move(0, 0, -90);
        await Task.Delay(3000);

        await robot.SetWheelSpeed(30);
        await Task.Delay(500);

        await robot.SetWheelSpeed(-30, 30, 30, -30); // Move right
        await Task.Delay(500);

        await CenterOnLine(robot, LineColour.Red, 0.4f, 0.05f);

        var actions = new Actions(robot, robotState);

        actions.LookForObstacles(12);
        await actions.FollowLine(LineColour.Red, 40);
    }

    public static async Task ReturnFromDepot(RoboMasterClient robot, StateMachine<RobotState, RobotTrigger> robotState)
    {
        await robot.Move(0, 0, 180);
        await Task.Delay(8000);

        await robot.SetWheelSpeed(60);
        await Task.Delay(500);

        await robot.Move(0, 0, -90);
        await Task.Delay(3000);

        _ = Task.Run(() => robotState.SafeFireAsync(RobotTrigger.ReturnedFromDepot));
    }

    public static async Task FindLine(RoboMasterClient robot, LineColour lineColour)
    {
        await robot.SetLineRecognitionColour(lineColour);

        var rotationEnumerable = robot.ChassisAttitude.Select(attitude => attitude.Yaw).ToDroppingAsyncEnumerable();
        var lineEnumerable = robot.Line.ToDroppingAsyncEnumerable();

        var startingYaw = await robot.ChassisAttitude.Select(attitude => attitude.Yaw).FirstAsync();
        var stopDistance = 5f;
        var hasLeftStopDistance = false;

        var rotationsWithVisibleLine = new List<float>();

        await robot.SetWheelSpeed(50, -50);

        await foreach (var (yaw, line) in rotationEnumerable.Zip(lineEnumerable))
        {
            if (line.Type != LineType.None)
            {
                rotationsWithVisibleLine.Add(yaw);
            }

            if (!hasLeftStopDistance)
            {
                if (GetCircularDistance(yaw, startingYaw) > stopDistance)
                {
                    hasLeftStopDistance = true;
                }
            }
            else
            {
                if (GetCircularDistance(yaw, startingYaw) < stopDistance)
                {
                    break;
                }
            }
        }

        await robot.SetWheelSpeed(0);

        var closestRotations = rotationsWithVisibleLine.OrderBy(rotation => GetCircularDistance(rotation, startingYaw)).Take(4).ToList();
        var targetRotation = closestRotations.Average();

        await robot.SetWheelSpeed(50, -50);

        await foreach (var yaw in rotationEnumerable)
        {
            if (GetCircularDistance(yaw, targetRotation) < stopDistance)
            {
                break;
            }
        }
    }

    public static float GetCircularDistance(float angle1, float angle2)
    {
        var distance = Math.Abs(angle1 - angle2);

        if (distance > 180)
        {
            distance = 360 - distance;
        }

        return distance;
    }
}