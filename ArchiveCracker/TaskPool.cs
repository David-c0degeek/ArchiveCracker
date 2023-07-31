namespace ArchiveCracker;

public class TaskPool
{
    private readonly SemaphoreSlim _semaphore;

    public TaskPool(int degreeOfParallelism)
    {
        _semaphore = new SemaphoreSlim(degreeOfParallelism);
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