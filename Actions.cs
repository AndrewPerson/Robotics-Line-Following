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
        var normalisedAngle1 = angle1 < 0 ? angle1 + 360 : angle1;
        var normalisedAngle2 = angle2 < 0 ? angle2 + 360 : angle2;

        var distance = Math.Abs(normalisedAngle1 - normalisedAngle2);

        while (distance > 180)
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

            await Task.Delay(500);
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
        var framesSinceLastIntersection = 0;

        await foreach (var line in robot.Line.ToDroppingAsyncEnumerable())
        {
            if (line.Type == LineType.None || line.Points.Length == 0)
            {
                await robot.SetWheelSpeed(0);
                cancellationTokenSource.Cancel();
                throw new NoLineException();
            }

            Console.WriteLine(line.Points.Sum(p => p.X) / line.Points.Length);

            if (line.Type == LineType.Intersection)
            {
                didSeeIntersection = true;
                framesSinceLastIntersection = 0;
            }
            else
            {
                framesSinceLastIntersection++;
            }

            if (didSeeIntersection && framesSinceLastIntersection >= 5)
            {
                await robot.SetWheelSpeed(0);
                cancellationTokenSource.Cancel();
                return;
            }

            var (leftSpeed, rightSpeed) = follower.GetWheelSpeed(line);

            await robot.SetWheelSpeed(rightSpeed, leftSpeed);
        }

        await robot.SetWheelSpeed(0);
        cancellationTokenSource.Cancel();
        throw new Exception("The line stream ended unexpectedly");
    }

    public async Task FindLine(LineColour lineColour)
    {
        await robot.SetLineRecognitionColour(lineColour);
        await robot.SetWheelSpeed(0);

        await foreach (var line in robot.Line.ToDroppingAsyncEnumerable())
        {
            if (line.Type != LineType.None)
            {
                return;
            }
        }
    }

    public async Task FindLineHorizontally(LineColour lineColour, float targetX, float margin = 0.01f, float speed = 20)
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

            if (x < targetX) // Go Left
            {
                await robot.SetWheelSpeed(speed, -speed, -speed, speed);
            }
            else // Go Right
            {
                await robot.SetWheelSpeed(-speed, speed, speed, -speed);
            }
        }
    }

    public async Task AlignToLine(LineColour lineColour, float angularTolerance, float positionalTolerance = 0.05f)
    {
        await FindLineHorizontally(lineColour, 0.5f);

        await foreach (var line in robot.Line.ToDroppingAsyncEnumerable())
        {
            if (line.Type == LineType.None)
            {
                await robot.SetWheelSpeed(0);
                throw new NoLineException();
            }

            if (Math.Abs(line.Points[0].X - 0.5f) > positionalTolerance)
            {
                await robot.SetWheelSpeed(0);
                await FindLineHorizontally(lineColour, 0.5f);

                continue;
            }

            // Least squares line of best fit
            var xMean = line.Points.Average(point => point.X);
            var yMean = line.Points.Average(point => point.Y);

            var denominator = line.Points.Select(point => Math.Pow(point.X - xMean, 2)).Sum();
            var slope = denominator == 0 ? 90 : line.Points.Select(point => (point.X - xMean) * (point.Y - yMean)).Sum() / denominator;

            var angle = (float)(Math.Atan(slope) * 180 / Math.PI);

            if (GetCircularDistance(angle, 90) < angularTolerance)
            {
                await robot.SetWheelSpeed(0);
                return;
            }

            if (angle < 0) // Go Left
            {
                await robot.SetWheelSpeed(-15, 15);
            }
            else // Go Right
            {
                await robot.SetWheelSpeed(15, -15);
            }
        }
    }

    public async Task MoveToDepot()
    {
        #region Move Approximately to Line
        await robot.Move(0, 0, -90);
        await Task.Delay(3000);

        await robot.MoveForward(10);

        await robot.SetWheelSpeed(-30, 30, 30, -30); // Move right
        await Task.Delay(500);
        #endregion

        #region Center on Line
        await AlignToLine(LineColour.Red, 2f);
        await FindLineHorizontally(LineColour.Red, 0.35f);
        #endregion

        #region Move to Depot
        await robot.SetWheelSpeed(30);

        while (await robot.GetIRDistance(1) > 18) { }

        await robot.SetWheelSpeed(0);
        #endregion
    }

    public async Task ReturnFromDepot()
    {
        await robot.Move(0, 0, 180);
        await Task.Delay(8000);

        await robot.MoveForward(40);

        await robot.Move(0, 0, -90);
        await Task.Delay(3000);

        await FindLineHorizontally(LineColour.Red, 0.3f);
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