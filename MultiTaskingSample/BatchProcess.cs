using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MultiTaskingSample
{
    public class BatchProcess<T>
    {
        private readonly TaskMode mode;
        private List<TaskResult<T>> result;
        private List<Func<Task<T>>> batchTasks;
        private ConcurrentQueue<Func<Task<T>>> queueTask;
        private ConcurrentDictionary<int, Task> runningTask;
        private TaskCompletionSource<bool> sourceTask;
        private object lockObject = new object();

        public event EventHandler<BatchLogEvent<T>> BatchCompleted;
        public event EventHandler<TaskResult<T>> QueueCompleted;

        public BatchProcess(TaskMode mode)
        {
            this.mode = mode;
            result = new List<TaskResult<T>>();

            if (mode == TaskMode.BatchProcess)
            {
                batchTasks = new List<Func<Task<T>>>();
            }
            else
            {
                sourceTask = new TaskCompletionSource<bool>();
                queueTask = new ConcurrentQueue<Func<Task<T>>>();
                runningTask = new ConcurrentDictionary<int, Task>();
            }
        }

        public void RegisterTask(Func<Task<T>> task)
        {
            if (mode == TaskMode.BatchProcess)
                batchTasks.Add(task);
            else
                queueTask.Enqueue(task);
        }

        public async Task<List<TaskResult<T>>> ExecuteAsync(int batchSize, bool continueOnException = true)
        {
            if (mode == TaskMode.BatchProcess)
                return await ExecuteInBatchAsync(batchSize, continueOnException);
            else
                return await ExecuteInQueueAsync(batchSize, continueOnException);
        }

        private async Task<List<TaskResult<T>>> ExecuteInBatchAsync(int batchSize, bool continueOnException)
        {
            int loop = 0, batchNo = 0, taskCount = batchTasks.Count;

            while (loop < taskCount)
            {
                var currentTask = new List<Task<T>>();
                batchNo++;

                for (int i = 0; i < batchSize && loop < taskCount; i++)
                {
                    var t = batchTasks[loop];
                    currentTask.Add(t());
                    loop++;
                }

                try
                {
                    await Task.WhenAll(currentTask);
                }
                catch (Exception ex)
                {
                    if (!continueOnException)
                        throw ex;
                }

                var currentResult = new List<TaskResult<T>>();
                foreach (var task in currentTask)
                {
                    if (task.IsFaulted)
                    {
                        currentResult.Add(new TaskResult<T> { IsFault = true, Exception = task.Exception });
                    }
                    else
                    {
                        currentResult.Add(new TaskResult<T> { Result = task.Result });
                    }
                }

                BatchCompleted?.Invoke(this, new BatchLogEvent<T> { BatchNumber = batchNo, Result = currentResult });
                result.AddRange(currentResult);
            }

            batchTasks = new List<Func<Task<T>>>();
            return result;
        }

        private async Task<List<TaskResult<T>>> ExecuteInQueueAsync(int batchSize, bool continueOnException)
        {
            //reference: https://github.com/Ciantic/TaskQueue/blob/master/TaskQueue.cs
            var pendingTasks = sourceTask.Task;
            StartQueueingTask(batchSize, continueOnException);
            await pendingTasks;
            return result;
        }

        private void StartQueueingTask(int batchSize, bool continueOnException)
        {
            var startMaxCount = batchSize - runningTask.Count;
            for (int i = 0; i < startMaxCount; i++)
            {
                if (!queueTask.TryDequeue(out Func<Task<T>> futureTask))
                    break;

                var t = Task.Run(futureTask);
                if (!runningTask.TryAdd(t.GetHashCode(), t))
                {
                    var currentException = new Exception("Could not get unique hash code for current task");
                    var runningTaskResult = new TaskResult<T> { Exception = currentException, IsFault = true };
                    if (continueOnException)
                    {
                        lock (lockObject)
                        {
                            result.Add(runningTaskResult);
                        }
                        QueueCompleted?.Invoke(this, runningTaskResult);
                        break;
                    }
                    else
                    {
                        throw currentException;
                    }
                }

                t.ContinueWith((t2) =>
                {
                    if (!runningTask.TryRemove(t2.GetHashCode(), out Task currentTask))
                    {
                        var currentException = new Exception("Could not get unique hash code for current task");
                        var runningTaskResult = new TaskResult<T> { Exception = currentException, IsFault = true };
                        if (continueOnException)
                        {
                            lock (lockObject)
                            {
                                result.Add(runningTaskResult);
                            }
                            QueueCompleted?.Invoke(this, runningTaskResult);
                            return;
                        }
                        else
                        {
                            throw currentException;
                        }
                    }

                    TaskResult<T> taskResult;
                    if (t2.IsFaulted)
                    {
                        taskResult = new TaskResult<T> { Exception = t2.Exception, IsFault = true };
                        if (!continueOnException)
                            throw t2.Exception;
                    }
                    else
                    {
                        taskResult = new TaskResult<T> { Result = t2.Result };
                    }

                    lock (lockObject)
                    {
                        result.Add(taskResult);
                    }

                    QueueCompleted?.Invoke(this, taskResult);
                    StartQueueingTask(batchSize, continueOnException);
                });
            }

            if (queueTask.IsEmpty && runningTask.IsEmpty)
            {
                var _oldQueue = Interlocked.Exchange(
                    ref sourceTask, new TaskCompletionSource<bool>());

                _oldQueue.TrySetResult(true);
            }
        }
    }

    public enum TaskMode
    {
        BatchProcess,
        QueueProcess
    }

    public class TaskResult<T>
    {
        public bool IsFault { get; set; }

        public T Result { get; set; }

        public Exception Exception { get; set; }
    }

    public class BatchLogEvent<T>
    {
        public int BatchNumber { get; set; }

        public ICollection<TaskResult<T>> Result { get; set; }
    }
}
