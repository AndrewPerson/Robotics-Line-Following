import sbhs_robomaster as rm
import asyncio

async def main():
    async with await rm.connect_to_robomaster(rm.DIRECT_CONNECT_IP) as robot:
        await robot.set_wheel_speed(50, 50, 50, 50)
        await asyncio.sleep(1)

        await robot.set_arm_position(50, 50)

        await robot.close_gripper()
        await robot.open_gripper()

        await robot.set_ir_enabled()
        print(await robot.get_ir_distance(1))

        await robot.set_line_recognition_enabled()
        await robot.set_line_recognition_colour(rm.LineColour.Red)

        async for line in rm.DroppingAsyncEnumerable[rm.Line](robot.line):
            print(f"Line type: {line.type}")
            print(f"Points: [{', '.join(line.points)}]")


asyncio.run(main())