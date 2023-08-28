public enum RobotState
{
    FollowingRedLine,
    FollowingBlueLine,
    NavigatingIntersection,
    CollectingBox,
        MovingToBox,
        GrabbingBox,
        ReturningWithBox,
    DroppingBox,
        MovingToDropPoint,
        PlacingBox,
        ReturningDropPoint,
    Stopped,
        RedStopped,
        BlueStopped
}
