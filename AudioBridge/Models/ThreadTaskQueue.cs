using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace AudioBridge.Models
{
    public class ThreadTaskQueue
    {
        private ConcurrentStack<Action> tasks = new ConcurrentStack<Action>();

        public void Start()
        {
            Thread();
        }

        private void Thread()
        {
            for(; ; )
            {
                lock (tasks)
                {
                    for(; ; )
                    {
                        if (!tasks.TryPop(out var task)) break;

                        task();
                    }
                    Monitor.Wait(tasks);
                }
            }
        }
        public async Task EnqueueTaskAsync(Action a)
        {
            var tcs = new TaskCompletionSource<int>();
            lock (tasks)
            {
                tasks.Push(() =>
                {
                    try
                    {
                        a();
                        tcs.SetResult(0);
                    } catch(Exception e)
                    {
                        tcs.SetException(e);
                    }
                });
                Monitor.Pulse(tasks);
            }
            await tcs.Task;
        }
    }
}
