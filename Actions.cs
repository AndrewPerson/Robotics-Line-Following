using System.Reactive.Linq;
using RoboMaster;
using Stateless;

public static class Actions
{
    public static void LookForObstacles(RoboMasterClient robot, StateMachine<RobotState, RobotTrigger> robotState)
    {
        new Thread(async () =>
        {
            var wasTooClose = false;
            while (true)
            {
                var distance = await robot.GetIRDistance(1);

                if (distance < 30)
                {
                    if (!wasTooClose) await robotState.SafeFireAsync(RobotTrigger.ObstacleTooClose);
                    wasTooClose = true;
                }
                else
                {
                    if (wasTooClose) await robotState.SafeFireAsync(RobotTrigger.NoObstacles);
                    wasTooClose = false;
                }

                await Task.Delay(100);
            }
        })
        {
            IsBackground = true
        }.Start();
    }

    public static void FollowLine(RoboMasterClient robot, StateMachine<RobotState, RobotTrigger> robotState)
    {
        new Thread(async () =>
        {
            var follower = new Follower();

            foreach (var line in robot.Line.MostRecent(new Line()))
            {
                if (line.Points.Length != 10)
                {
                    await robotState.SafeFireAsync(RobotTrigger.NoLineDetected);
                    break;
                }

                if (line.Type != LineType.Straight)
                {
                    await robotState.SafeFireAsync(RobotTrigger.IntersectionDetected);
                    break;
                }

                var (leftSpeed, rightSpeed) = follower.GetWheelSpeed(line);

                await robot.SetWheelSpeed(rightSpeed, leftSpeed);
            }
        })
        {
            IsBackground = true
        }.Start();
    }

    public static async Task Stop(RoboMasterClient robot)
    {
        await robot.SetWheelSpeed(0);
    }

    public static void LookForLine(RoboMasterClient robot, StateMachine<RobotState, RobotTrigger> robotState)
    {
        new Thread(async () =>
        {
            await foreach (var line in robot.Line.ToAsyncEnumerable())
            {
                if (line.Points.Length == 10 && line.Type == LineType.Straight)
                {
                    await robotState.SafeFireAsync(RobotTrigger.LineDetected);
                    break;
                }
            }
        })
        {
            IsBackground = true
        }.Start();
    }
}