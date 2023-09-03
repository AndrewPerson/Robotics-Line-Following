using RoboMaster;

public class Actions
{
    private RoboMasterClient robot;

    public Actions(RoboMasterClient robot)
    {
        this.robot = robot;
    }

    private static Task<StopReason> RunLoop(Func<Task<StopReason?>> action, CancellationToken cancellationToken)
    {
        var taskCompletionSource = new TaskCompletionSource<StopReason>();

        new Thread(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await action();

                if (result != null)
                {
                    taskCompletionSource.SetResult(result.Value);
                    break;
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                taskCompletionSource.SetCanceled();
            }
        })
        {
            IsBackground = true
        }.Start();

        return taskCompletionSource.Task;
    }

    public Task<StopReason> LookForObstacles(float minDistance = 30, CancellationToken? cancellationToken = null)
    {
        return RunLoop(async () =>
        {
            var distance = await robot.GetIRDistance(1);

            if (distance < minDistance)
            {
                return StopReason.TooClose;
            }

            await Task.Delay(100);

            return null;
        }, cancellationToken ?? CancellationToken.None);
    }

    public Task<StopReason> LookForNoObstacles(float minDistance = 30, CancellationToken? cancellationToken = null)
    {
        return RunLoop(async () =>
        {
            var distance = await robot.GetIRDistance(1);

            if (distance >= minDistance)
            {
                return StopReason.NotTooClose;
            }

            await Task.Delay(100);

            return null;
        }, cancellationToken ?? CancellationToken.None);
    }

    public async Task<StopReason> FollowLine(LineColour lineColour, float? speed = null, CancellationToken? cancellationToken = null)
    {
        await robot.SetLineRecognitionColour(lineColour);
        
        var follower = new Follower();
        if (speed != null) follower.BaseWheelSpeed = speed.Value;

        var didSeeIntersection = false;

        await foreach (var line in robot.Line.ToDroppingAsyncEnumerable())
        {
            if (cancellationToken?.IsCancellationRequested == true) return StopReason.Cancelled;

            if (line.Type == LineType.None || line.Points.Length == 0) return StopReason.NoLine;

            if (line.Type == LineType.Intersection)
            {
                didSeeIntersection = true;
            }
            else if (didSeeIntersection)
            {
                return StopReason.Intersection;
            }

            var (leftSpeed, rightSpeed) = follower.GetWheelSpeed(line);

            await robot.SetWheelSpeed(rightSpeed, leftSpeed);
        }

        throw new Exception("The line stream ended unexpectedly");
    }

    public async Task<StopReason> FindLine(LineColour lineColour, CancellationToken? cancellationToken = null)
    {
        await robot.SetLineRecognitionColour(lineColour);

        var lineEnumerator = robot.Line.ToDroppingAsyncEnumerable().GetAsyncEnumerator();
        await lineEnumerator.MoveNextAsync();

        return await RunLoop(async () =>
        {
            var line = lineEnumerator.Current;

            if (line.Type != LineType.None && line.Points.Length != 0)
            {
                return StopReason.Line;
            }

            await lineEnumerator.MoveNextAsync();

            return null;
        }, cancellationToken ?? CancellationToken.None);
    }

    public async Task<StopReason> MoveToDepot()
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
        var cancellationTokenSource = new CancellationTokenSource();
        
        await await Task.WhenAny
        (
            FollowLine(LineColour.Red, speed: 40, cancellationToken: cancellationTokenSource.Token),
            LookForObstacles(cancellationToken: cancellationTokenSource.Token)
        );

        cancellationTokenSource.Cancel();
        #endregion

        return StopReason.Completed;
    }

    public async Task<StopReason> ReturnFromDepot()
    {
        await robot.Move(0, 0, 180);
        await Task.Delay(8000);

        await robot.SetWheelSpeed(60);
        await Task.Delay(500);

        await robot.Move(0, 0, -90);
        await Task.Delay(3000);

        return StopReason.Completed;
    }
}

public enum StopReason
{
    NoLine,
    Line,
    Intersection,
    TooClose,
    NotTooClose,
    Completed,
    Cancelled
}