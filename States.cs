public enum RobotState
{
    FollowingRedLine,
    FollowingBlueLine,
    NavigatingIntersection,
    CollectingBox,
        MovingToBox,
        GrabbingBox,
        ReturningWithBox,
    Stopped,
        RedStopped,
        BlueStopped
}
