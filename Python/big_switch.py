import asyncio
from route_planner import ConnectionType, CorrectionType

currentConnection, _ = route[intersectionCount]
currentCorrection = CorrectionType.NoCorrection if intersectionCount == 0 else route[intersectionCount - 1][1]

if currentCorrection == CorrectionType.Left:
    robot.move(0, 0, -90)
    await asyncio.sleep(3)
elif currentCorrection == CorrectionType.Right:
    robot.move(0, 0, 90)
    await asyncio.sleep(3)

if currentConnection == ConnectionType.CollectBox:
    # Grab box
    ...
elif currentConnection == ConnectionType.DropBox:
    # Drop box
    ...
elif currentConnection == ConnectionType.BlueLine:
    # Follow blue line (Same as follow redline)
    ...
elif currentConnection == ConnectionType.Left:
    robot.move_forward(20)
    await asyncio.sleep(2)
    robot.move(0, 0, -90)
    await asyncio.sleep(3)
elif currentConnection == ConnectionType.Right:
    robot.move_forward(20)
    await asyncio.sleep(2)
    robot.move(0, 0, 90)
    await asyncio.sleep(3)
elif currentConnection == ConnectionType.Forward:
    robot.move_forward(20)
    await asyncio.sleep(2)
