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

    // This returns an async enumerable that waits for the previous item to be consumed before producing the next one.
    // Items are dropped if they are produced faster than they are consumed.
    public static IAsyncEnumerable<T> ToDroppingAsyncEnumerable<T>(this IObservable<T> observable)
    {
        return new DroppingAsyncEnumerable<T>(observable);
    }
}

public class DroppingAsyncEnumerable<T> : IAsyncEnumerable<T>
{
    private IObservable<T> observable;

    public DroppingAsyncEnumerable(IObservable<T> observable)
    {
        this.observable = observable;
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new DroppingAsyncEnumerator<T>(observable, cancellationToken);
    }

    private class DroppingAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private CancellationToken cancellationToken;

        private T current;
        private bool hasCurrent;

        private TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

        public DroppingAsyncEnumerator(IObservable<T> observable, CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;

            observable.Subscribe(OnNext, OnError, OnCompleted);
        }

        private void OnNext(T value)
        {
            if (!hasCurrent)
            {
                current = value;
                hasCurrent = true;
                tcs.TrySetResult(true);
            }
        }

        private void OnError(Exception error)
        {
            tcs.TrySetException(error);
        }

        private void OnCompleted()
        {
            tcs.TrySetResult(false);
        }

        public T Current => current;

        public async ValueTask DisposeAsync()
        {
            await tcs.Task;
        }

        public async ValueTask<bool> MoveNextAsync()
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await tcs.Task;
                return false;
            }

            await tcs.Task;
            tcs = new TaskCompletionSource<bool>();
            hasCurrent = false;
            return true;
        }
    }
}