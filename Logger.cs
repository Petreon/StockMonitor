namespace StockMonitor
{
	public enum LogLevel
	{
		Info = 0x00,
		Warn = 0x01,
		Error = 0x02
	}

	public static class Logger
	{
		public sealed class Log
		{
			public DateTime Timestamp { get; init; }
			public LogLevel Level { get; init; }
			public string Message { get; init; } = string.Empty;
		}

		private static readonly Dictionary<LogLevel, List<Log>> mLogger;

		static Logger()
		{
			mLogger = new Dictionary<LogLevel, List<Log>>
			{
				[LogLevel.Info] = new List<Log>(),
				[LogLevel.Warn] = new List<Log>(),
				[LogLevel.Error] = new List<Log>()
			};
		}

		/// <summary>
		/// Função para armazenar os log para casa uma aplicação futura precise receber os dados de log dela
		/// ex: grafana ou algum Log Wacther.
		/// </summary>
		/// <param name="level"></param>
		/// <param name="message"></param>
		public static void PrintLog(LogLevel level, string message)
		{
			DateTime timestamp = DateTime.UtcNow;
			Log log = new()
			{
				Timestamp = timestamp,
				Level = level,
				Message = message
			};

			mLogger[level].Add(log);

			string timeFormatted = timestamp.ToString("yyyy-MM-dd HH:mm:ss");
			string logMessage = BuildString(level, message, timeFormatted);

			if (level == LogLevel.Error)
			{
				Console.Error.WriteLine(logMessage);
				return;
			}

			Console.WriteLine(logMessage);
		}

		/// <summary>
		/// Used for error cases only
		/// </summary>
		/// <param name="message"></param>
		/// <param name="parameterName"></param>
		/// <exception cref="ArgumentException"></exception>
		public static void LogAndThrow(string message, string? parameterName = null)
		{
			Logger.PrintLog(LogLevel.Error, message);
			throw new ArgumentException(message, parameterName);
		}

		private static string BuildString(LogLevel level, string message, string timeFormatted)
		{
			return $"[{timeFormatted}] | [{level}] | {message}";
		}
	}
}
