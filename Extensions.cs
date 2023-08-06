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
}