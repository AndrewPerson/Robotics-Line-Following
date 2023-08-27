using System.Reactive.Linq;
using RoboMaster;
using Stateless;

public class Actions
{
    private RoboMasterClient robot;
    private StateMachine<RobotState, RobotTrigger> robotState;

    private bool active = true;

    public Actions(RoboMasterClient robot, StateMachine<RobotState, RobotTrigger> robotState)
    {
        this.robot = robot;
        this.robotState = robotState;

        var weakRefThis = new WeakReference<Actions>(this);
        robotState.OnTransitioned(_ =>
        {
            if (weakRefThis.TryGetTarget(out var self))
            {
                self.active = false;
            }
        });
    }

    private async Task<bool> SafeFireAsync(RobotTrigger trigger)
    {
        if (active)
        {
            active = false;
            return await robotState.SafeFireAsync(trigger);
        }
        else return false;
    }

    private void RunLoop(Func<Task<RobotTrigger?>> action)
    {
        new Thread(async () =>
        {
            while (active)
            {
                var trigger = await action();

                if (trigger != null)
                {
                    await SafeFireAsync(trigger.Value);
                    break;
                }
            }
        })
        {
            IsBackground = true
        }.Start();
    }

    public void LookForObstacles(float minDistance = 30)
    {
        RunLoop(async () =>
        {
            var distance = await robot.GetIRDistance(1);

            if (distance < minDistance)
            {
                return RobotTrigger.ObstacleTooClose;
            }

            await Task.Delay(100);

            return null;
        });
    }

    public void LookForNoObstacles()
    {
        RunLoop(async () =>
        {
            var distance = await robot.GetIRDistance(1);

            if (distance >= 30)
            {
                return RobotTrigger.ObstacleTooClose;
            }

            await Task.Delay(100);

            return null;
        });
    }

    public async Task FollowLine(LineColour lineColour)
    {
        await robot.SetLineRecognitionColour(lineColour);
        
        var follower = new Follower();

        var didSeeIntersection = false;
        
        var lineEnumerator = robot.Line.ToDroppingAsyncEnumerable().GetAsyncEnumerator();

        RunLoop(async () =>
        {
            var line = lineEnumerator.Current;
            await lineEnumerator.MoveNextAsync();

            if (line.Type == LineType.None || line.Points.Length == 0)
            {
                return RobotTrigger.NoLineDetected;
            }

            if (line.Type == LineType.Intersection)
            {
                didSeeIntersection = true;
            }
            else if (didSeeIntersection)
            {
                return RobotTrigger.IntersectionDetected;
            }

            var (leftSpeed, rightSpeed) = follower.GetWheelSpeed(line);

            await robot.SetWheelSpeed(rightSpeed, leftSpeed);

            return null;
        });
    }

    public void LookForLine()
    {
        var lineEnumerator = robot.Line.ToDroppingAsyncEnumerable().GetAsyncEnumerator();

        RunLoop(async () =>
        {
            await lineEnumerator.MoveNextAsync();
            var line = lineEnumerator.Current;

            if (line.Points.Length == 10 && line.Type == LineType.Straight)
            {
                return RobotTrigger.LineDetected;
            }

            return null;
        });
    }
}