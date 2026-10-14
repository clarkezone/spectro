namespace Spectro.Presentation;

public static class CleanupTasks
{
    public static async Task RunAllAsync(params Func<Task>[] operations)
    {
        var failures = new List<Exception>();
        foreach (var operation in operations)
        {
            try { await operation(); }
            catch (Exception exception) { failures.Add(exception); }
        }
        if (failures.Count > 0)
            throw new AggregateException("Some local account data could not be cleared.", failures);
    }
}
