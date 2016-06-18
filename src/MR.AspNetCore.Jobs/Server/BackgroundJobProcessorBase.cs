using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MR.AspNetCore.Jobs.Client;

namespace MR.AspNetCore.Jobs.Server
{
	public abstract class BackgroundJobProcessorBase : IProcessor
	{
		private readonly TimeSpan _pollingDelay = TimeSpan.FromSeconds(15);
		protected ILogger _logger;

		public BackgroundJobProcessorBase(ILogger logger)
		{
			_logger = logger;
		}

		public Task ProcessAsync(ProcessingContext context)
		{
			if (context == null) throw new ArgumentNullException(nameof(context));
			return ProcessCoreAsync(context);
		}

		public async Task ProcessCoreAsync(ProcessingContext context)
		{
			OnProcessEnter(context);

			try
			{
				var storage = context.Storage;
				using (var connection = storage.GetConnection())
				{
					var fetched = default(IFetchedJob);
					while (
						!context.IsStopping &&
						(fetched = await FetchNextJobCoreAsync(connection)) != null)
					{
						using (fetched)
						using (var scopedContext = context.CreateScope())
						{
							var job = fetched.Job;
							var invocationData = Helper.FromJson<InvocationData>(job.Data);
							var method = invocationData.Deserialize();
							var factory = scopedContext.Provider.GetService<IJobFactory>();

							var instance = default(object);
							if (!method.Method.IsStatic)
							{
								instance = factory.Create(method.Type);
							}

							try
							{
								var sp = Stopwatch.StartNew();
								var result =
									method.Method.Invoke(instance, method.Args.ToArray()) as Task;
								if (result != null)
								{
									await result;
								}
								sp.Stop();
								fetched.RemoveFromQueue();
								_logger.LogInformation(
									"Job executed succesfully. Took: {seconds} secs.",
									sp.Elapsed.TotalSeconds);
							}
							catch (Exception ex)
							{
								_logger.LogWarning(
									$"Job failed to execute: '{ex.Message}'. Requeuing.");
								fetched.Requeue();
								throw;
							}
						}
					}
				}

				var token = GetTokenToWaitOn(context);
				token.WaitHandle.WaitOne(_pollingDelay);
			}
			finally
			{
				OnProcessExit(context);
			}
		}

		protected virtual CancellationToken GetTokenToWaitOn(ProcessingContext context)
		{
			return context.CancellationToken;
		}

		protected abstract Task<IFetchedJob> FetchNextJobCoreAsync(IStorageConnection connection);

		protected virtual void OnProcessEnter(ProcessingContext context)
		{
		}

		protected virtual void OnProcessExit(ProcessingContext context)
		{
		}
	}
}
