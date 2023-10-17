from sbhs_robomaster import Line

def clamp(value: float, min: float, max: float) -> float:
    return min if value < min else max if value > max else value

class LineFollower:
    total_wheel_speed: float

    target_x: float

    p_sensitivity: float
    d_sensitivity: float
    i_sensitivity: float

    _previous_error: float
    _cumulative_error: float

    def __init__(self, total_wheel_speed: float = 120, target_x: float = 0.35, p_sensitivity: float = 3,
                    d_sensitivity: float = 2, i_sensitivity: float = 0):
            self.total_wheel_speed = total_wheel_speed
            self.target_x = target_x
            self.p_sensitivity = p_sensitivity
            self.d_sensitivity = d_sensitivity
            self.i_sensitivity = i_sensitivity

            self._previous_error = 0
            self._cumulative_error = 0

    def get_wheel_speed(self, line: Line) -> tuple[float, float]:
        actual = line.points[0].x

        p_error = self.target_x - actual
        d_error = p_error - self._previous_error
        self._cumulative_error += p_error

        self._previous_error = p_error

        total_error = clamp(
            p_error * self.p_sensitivity + d_error * self.d_sensitivity \
                + self._cumulative_error * self.i_sensitivity,
            -1, 1
        )

        if total_error == 0:
            left_weight = 1
            right_weight = 1
        elif total_error < 0: # Go Left
            balance = total_error

            left_weight = 1
            right_weight = balance * 2 + 1
        else: # Go Right
            balance = -total_error

            left_weight = balance * 2 + 1
            right_weight = 1

        total_weight = abs(left_weight) + abs(right_weight)

        normalised_left_weight = left_weight / total_weight
        normalised_right_weight = right_weight / total_weight

        return (
            self.total_wheel_speed * normalised_left_weight,
            self.total_wheel_speed * normalised_right_weight
        )


import sbhs_robomaster as rm

follower = LineFollower()
async for line in rm.DroppingAsyncEnumerable[rm.Line](robot.line):
    left, right = follower.get_wheel_speed(line)
    await robot.set_wheel_speed(right, left, left, right)

