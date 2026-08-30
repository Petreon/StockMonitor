using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StockMonitor
{
	public enum LogLevel
	{
		Info	= 0x00,
		Warn	= 0x01,
		Error	= 0x02
	}
	public static  class Logger
	{

		
		public class Log
		{
			public DateTime Timestamp;
			public LogLevel Level;
			public string Message;
		}

		// poderia
		private static Dictionary<LogLevel, List<Log>> mLogger; // dicionario para armazenar erros de log para debug futuros

		static Logger() 
		{
			mLogger = new Dictionary<LogLevel, List<Log>>();
			mLogger[LogLevel.Info]	= new List<Log>();
			mLogger[LogLevel.Warn]	= new List<Log>();
			mLogger[LogLevel.Error] = new List<Log>();
		}

		public static void PrintLog(LogLevel level, String message)
		{
			Log log = new Log();
			log.Level = level;
			log.Message = message;

			DateTime time = DateTime.UtcNow;
			String timeFmt = time.ToString("yyyy-MM-dd HH:mm:ss");
			log.Timestamp = time;

			mLogger[level].Add(log);

			Console.WriteLine(BuildString(level, message, timeFmt));

		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="level"></param>
		/// <param name="message"></param>
		/// <param name="timefmt"></param>
		/// <returns></returns>
		private static String BuildString(LogLevel level, String message, String timefmt)
		{
			return $"[{timefmt}] | [{level}] | {message}";
		}


	}
}
