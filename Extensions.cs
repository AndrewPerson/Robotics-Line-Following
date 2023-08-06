using Stateless;

public static class Extensions
{
    public static async Task<bool> SafeFireAsync<TState, TTrigger>(this StateMachine<TState, TTrigger> self, TTrigger trigger)
    {
        if (self.CanFire(trigger))
        {
            await self.FireAsync(trigger);
            return true;
        }

        return false;
    }

    public static StateMachine<TState, TTrigger>.StateConfiguration OnEntryFromAndInternal<TState, TTrigger>(
        this StateMachine<TState, TTrigger>.StateConfiguration self, TTrigger trigger, Action action)
    {
        return self.OnEntryFrom(trigger, action).InternalTransition(trigger, action);
    }
}