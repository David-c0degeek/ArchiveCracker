namespace ArchiveCracker;

public class TaskPool
{
    private SemaphoreSlim _semaphore;

    public TaskPool(int degreeOfParallelism)
    {
        _semaphore = new SemaphoreSlim(degreeOfParallelism);
    }

    public int Capacity => _semaphore.CurrentCount;

    public void Resize(int newCapacity)
    {
        _semaphore = new SemaphoreSlim(newCapacity);
    }

    public async Task Run(Func<Task> taskFactory)
    {
        await _semaphore.WaitAsync();
        try
        {
            await taskFactory();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<T> Run<T>(Func<Task<T>> taskFactory)
    {
        await _semaphore.WaitAsync();
        try
        {
            return await taskFactory();
        }
        finally
        {
            _semaphore.Release();
        }
    }
}