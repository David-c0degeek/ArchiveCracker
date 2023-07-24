using CommandLine;

namespace ArchiveCracker.Extensions;

public static class ParserExtensions
{
    public static async Task<ParserResult<T>> WithParsedAsync<T>(this Task<ParserResult<T>> task, Func<T, Task> action)
    {
        var result = await task;
        if (result is Parsed<T> parsed)
        {
            await action(parsed.Value);
        }
        return result;
    }
    
    public static async Task<ParserResult<T>> WithNotParsedAsync<T>(this Task<ParserResult<T>> task, Action<IEnumerable<Error>> action)
    {
        var result = await task;
        if (result is NotParsed<T> notParsed)
        {
            action(notParsed.Errors);
        }
        return result;
    }

}