using Basic.Models;
using Microsoft.Extensions.Logging;

namespace Basic.Services
{
	public class FooService
	{
		private ILogger<FooService> _logger;
		private AppDbContext _context;

		public FooService(
			AppDbContext context,
			ILogger<FooService> logger)
		{
			_logger = logger;
			_context = context;
		}

		public void LogSomething(string message)
		{
			_logger.LogInformation($"Logging something: \"{message}\".");
		}
	}
}
