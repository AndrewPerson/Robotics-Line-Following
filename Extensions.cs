using RoboMaster;

public static class Extensions
{
    public static async Task MoveForward(this RoboMasterClient robot, float distance, float speed = 50, float wheelCircumference = 31.415926536f)
    {
        float distancePerSecond = speed / 60 * wheelCircumference;
        float seconds = distance / distancePerSecond;

        await robot.SetWheelSpeed(0);
        await Task.Delay(1000);

        await robot.SetWheelSpeed(speed);
        await Task.Delay((int)(seconds * 1000));
        await robot.SetWheelSpeed(0);
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
        private readonly CancellationToken cancellationToken;

        private T current = default!;

        private TaskCompletionSource<bool> tcs = new();

        public DroppingAsyncEnumerator(IObservable<T> observable, CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;

            observable.Subscribe(OnNext, OnError, OnCompleted);
        }

        private void OnNext(T value)
        {
            current = value;
            tcs.TrySetResult(true);
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

        public async ValueTask<bool> MoveNextAsync()
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await tcs.Task;
                return false;
            }

            var result = await tcs.Task;
            tcs = new TaskCompletionSource<bool>();
            return result;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}