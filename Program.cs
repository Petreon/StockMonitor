using StockMonitor.ConfReader;
using StockMonitor.Monitor;

namespace StockMonitor
{
	internal class Program
	{
		static async Task<int> Main(string[] args)
		{
			UserData userData;

			try
			{
				userData = UserData.FromArgs(args);
			}
			catch (ArgumentException)
			{
				return 1;
			}

			Logger.PrintLog(LogLevel.Info, $"Dados da ação '{userData.StockSymbol}' inicializados.");

			try
			{
				// foi adicionado uma configuração no .cs project para facilitar, a leitura do path do .yaml e criada uma copia no root do binario.
				// São usados somente para popular o UserData
				ConfRead confRead		= new("config.yaml");
				ConfReadEnv confReadEnv = new(".env");

				userData.Configuration	= confRead.Configuration;
				userData.Environment	= confReadEnv.Credentials;
			}
			catch (Exception exception)
			{
				Logger.PrintLog(LogLevel.Error, exception.Message);
				return 1;
			}

			Alarm alarm			= new(userData);

			// starts the monitoring Task in background
			await using IMonitor monitor = new MonitorHttp(userData, alarm);


			while (true)
			{
				
			}

			return 0;
		}
	}
}
