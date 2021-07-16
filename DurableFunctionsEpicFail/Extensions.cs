using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace DurableFunctionsEpicFail
{
    public static class Extensions
    {
        public static async Task ParallelForEachAsync<T>(this IEnumerable<T> items, int maxConcurrency, Func<T, Task> action)
        {
            List<Task> tasks;
            if (items is ICollection<T> itemCollection)
            {
                // optimization to reduce the number of memory allocations
                tasks = new List<Task>(itemCollection.Count);
            }
            else
            {
                tasks = new List<Task>();
            }

            using var semaphore = new SemaphoreSlim(maxConcurrency);
            foreach (T item in items)
            {
                tasks.Add(InvokeThrottledAction(item, action, semaphore));
            }

            await Task.WhenAll(tasks);
        }

        static async Task InvokeThrottledAction<T>(T item, Func<T, Task> action, SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync();
            try
            {
                await action(item);
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
