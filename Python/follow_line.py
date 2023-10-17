import sbhs_robomaster as rm
import asyncio
from enum import Enum
from line_follower import LineFollower


class FollowLineResult(Enum):
    ObstacleTooClose = 1
    NoLine = 2
    Intersection = 3


async def follow_line(robot: rm.RoboMasterClient, line_colour: rm.LineColour, speed: float = 120, obstacle_distance: float = 30) -> FollowLineResult:
    await robot.set_line_recognition_colour(line_colour)

    async def look_for_obstacle():
        while True:
            if await robot.get_ir_distance(1) < obstacle_distance:
                return FollowLineResult.ObstacleTooClose

            
    look_for_obstacle_coro = asyncio.create_task(look_for_obstacle())

    async def follow_line():
        follower = LineFollower(total_wheel_speed=speed)

        did_see_intersection: bool = False

        async for line in rm.DroppingAsyncEnumerable[rm.Line](robot.line):
            if line.type == rm.LineType.NoLine or len(line.points) == 0:
                await robot.set_wheel_speed(0)
                return FollowLineResult.NoLine
            
            if line.type == rm.LineType.Intersection:
                did_see_intersection = True
            elif did_see_intersection:
                return FollowLineResult.Intersection

            left, right = follower.get_wheel_speed(line)
            await robot.set_wheel_speed(right, left, left, right)


    follow_line_coro = asyncio.create_task(follow_line())

    done, pending = await asyncio.wait([look_for_obstacle_coro, follow_line_coro], return_when=asyncio.FIRST_COMPLETED)

    await robot.set_wheel_speed(0)

    for task in pending:
        task.cancel()

    return done.pop().result()
