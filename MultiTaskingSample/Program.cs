using System;
using System.Linq;
using System.Threading.Tasks;

namespace MultiTaskingSample
{
    class Program
    {
        static object lockLogging = new object();

        static void Main(string[] args)
        {
            RunTest().Wait();
        }

        static async Task RunTest()
        {
            var numberOfLoop = 50;
            var batchSize = 8;
            var continueOnException = true;

            //initialize helper
            var queue = new BatchProcess<ServiceResult>(TaskMode.QueueProcess);

            //proof of queueing
            queue.BatchCompleted += (s, e) => ShowBatchProgress(e); //event for ExecuteInBatchAsync only
            queue.QueueCompleted += (s, e) => ShowQueueProgress(e); //event for ExecuteInQueueAsync only

            //register task
            for (int i = 1; i < numberOfLoop + 1; i++)
            {
                var request = new ServiceRequest { Id = i };
                queue.RegisterTask(() => GetResponseFromService(request));
            }

            //wait for result
            Console.WriteLine("Start executing\n");
            var response = await queue.ExecuteAsync(batchSize, continueOnException);
            Console.WriteLine("End executing\n");

            //use the result
            Console.WriteLine(string.Format("Result : {0} successfull request.", response.Count(e => !e.IsFault)));
            Console.ReadKey();
        }

        static async Task<ServiceResult> GetResponseFromService(ServiceRequest request)
        {
            var sleep = (new Random().Next(1, 20)) * 1000;

            //test long running task
            if (request.Id == 3 || request.Id == 7 || request.Id == 12)
                sleep = 20 * 1000;

            var result = new ServiceResult
            {
                Id = request.Id,
                ExecutedOn = DateTime.Now.ToString("mm:ss.ffffff"),
                ResponseTime = sleep,
                Response = Guid.NewGuid().ToString()
            };

            await Task.Delay(sleep);

            //test throw exception
            if (request.Id == 9 || request.Id == 12 || request.Id == 23)
                throw new Exception(string.Format("Id:{0}, Service throwing exception, ResponseTime:{1} sec", request.Id, result.ResponseTime / 1000));

            return result;
        }

        static void ShowBatchProgress(BatchLogEvent<ServiceResult> e)
        {
            Console.WriteLine(string.Format("Batch {0} finished.", e.BatchNumber));
            foreach (var item in e.Result)
            {
                ShowQueueProgress(item);
            }
            Console.WriteLine();
        }

        static void ShowQueueProgress(TaskResult<ServiceResult> e)
        {
            lock (lockLogging)
            {
                if (e.IsFault)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(string.Format("[Error] {0} {1}", e.Exception.Message, e.Exception.InnerException?.Message));
                    Console.ResetColor();
                }
                else
                {
                    var result = e.Result;
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write("[Completed] ");
                    Console.ResetColor();
                    Console.WriteLine(string.Format("Id:{0}, ExecutedOn:{1}, ResponseTime:{2} sec, Response:{3}", result.Id, result.ExecutedOn, result.ResponseTime / 1000, result.Response));
                }
            }
        }

        class ServiceRequest
        {
            public int Id { get; set; }
        }

        class ServiceResult
        {
            public int Id { get; set; }

            public int ResponseTime { get; set; }

            public string ExecutedOn { get; set; }

            public string Response { get; set; }
        }
    }
}
