using System.Reactive.Linq;
using RoboMaster;

public class Actions
{
    private RoboMasterClient robot;

    public Actions(RoboMasterClient robot)
    {
        this.robot = robot;
    }

    private static float GetCircularDistance(float angle1, float angle2)
    {
        var distance = Math.Abs(angle1 - angle2);

        if (distance > 180)
        {
            distance = 360 - distance;
        }

        return distance;
    }

    public async Task LookForObstacle(float minDistance = 30, CancellationToken? cancellationToken = null)
    {
        while (true)
        {
            var distance = await robot.GetIRDistance(1);

            if (distance < minDistance)
            {
                return;
            }

            if (cancellationToken != null && cancellationToken.Value.IsCancellationRequested)
            {
                return;
            }

            await Task.Delay(100);
        }
    }

    public async Task LookForNoObstacles(float minDistance = 30)
    {
        while (true)
        {
            var distance = await robot.GetIRDistance(1);

            if (distance >= minDistance)
            {
                return;
            }

            await Task.Delay(100);
        }
    }

    public async Task FollowLine(LineColour lineColour, float? speed = null, float obstacleDistance = 30)
    {
        var cancellationTokenSource = new CancellationTokenSource();

        await robot.SetLineRecognitionColour(lineColour);

        _ = LookForObstacle(obstacleDistance, cancellationTokenSource.Token).ContinueWith(async _ =>
        {
            if (cancellationTokenSource.IsCancellationRequested) return;

            await robot.SetWheelSpeed(0);
            cancellationTokenSource.Cancel();
            throw new ObstacleTooCloseException();
        });

        var follower = new Follower();
        if (speed != null) follower.BaseWheelSpeed = speed.Value;

        var didSeeIntersection = false;

        await foreach (var line in robot.Line.ToDroppingAsyncEnumerable())
        {
            if (line.Type == LineType.None || line.Points.Length == 0)
            {
                await robot.SetWheelSpeed(0);
                cancellationTokenSource.Cancel();
                throw new NoLineException();
            }

            if (line.Type == LineType.Intersection)
            {
                didSeeIntersection = true;
            }
            else if (didSeeIntersection)
            {
                cancellationTokenSource.Cancel();
                return;
            }

            var (leftSpeed, rightSpeed) = follower.GetWheelSpeed(line);

            await robot.SetWheelSpeed(rightSpeed, leftSpeed);
        }

        cancellationTokenSource.Cancel();
        throw new Exception("The line stream ended unexpectedly");
    }

    public async Task FindLine(LineColour lineColour)
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

    public async Task MoveToDepot()
    {
        #region Move Approximately to Line
        await robot.Move(0, 0, -90);
        await Task.Delay(3000);

        await robot.SetWheelSpeed(30);
        await Task.Delay(500);

        await robot.SetWheelSpeed(-30, 30, 30, -30); // Move right
        await Task.Delay(500);
        #endregion

        #region Center on Line
        var targetX = 0.4f;
        var margin = 0.05f;

        await robot.SetLineRecognitionColour(LineColour.Red);

        await foreach (var line in robot.Line.ToDroppingAsyncEnumerable())
        {
            var x = line.Points[0].X;

            if (Math.Abs(x - targetX) < margin)
            {
                await robot.SetWheelSpeed(0);
                break;
            }

            if (x < targetX) // Go Left
            {
                await robot.SetWheelSpeed(50, -50, -50, 50);
            }
            else // Go Right
            {
                await robot.SetWheelSpeed(-50, 50, 50, -50);
            }
        }
        #endregion

        #region Move to Depot
        try
        {
            await FollowLine(LineColour.Red, speed: 40, obstacleDistance: 15);
        }
        catch (NoLineException) { }
        catch (ObstacleTooCloseException) { }
        #endregion
    }

    public async Task ReturnFromDepot()
    {
        await robot.Move(0, 0, 180);
        await Task.Delay(8000);

        await robot.SetWheelSpeed(60);
        await Task.Delay(500);

        await robot.Move(0, 0, -90);
        await Task.Delay(3000);
    }
}

public class NoLineException : Exception
{
    public NoLineException() : base("No line was found")
    {
    }
}

public class ObstacleTooCloseException : Exception
{
    public ObstacleTooCloseException() : base("An obstacle was too close")
    {
    }
}