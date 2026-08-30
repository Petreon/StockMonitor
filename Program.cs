using StockMonitor.ConfReader;

namespace StockMonitor
{
	internal class Program
	{
		static int Main(string[] args)
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

			//TODO: essa implementação vai ser mudada para usar um IMonitor para conseguir separar
			// a interface de conexão Http e Websocket.
			//Monitor monitor		= new(userData, ConnectionType.HttpClient, "TODO"); //
			Alarm alarm			= new(userData);

			return 0;
		}
	}
}
