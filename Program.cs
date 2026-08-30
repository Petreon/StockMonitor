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
			// foi adicionado uma configuração no .cs project para facilitar, a leitura do path do .yaml e criada uma copia no root do binario.
			ConfRead confRead	= new(userData, "config.yaml"); 
			//TODO: essa implementação vai ser mudada para usar um IMonitor para conseguir separar
			// a interface de conexão Http e Websocket.
			//Monitor monitor		= new(userData, ConnectionType.HttpClient, "TODO"); //
			Alarm alarm			= new(userData);

			return 0;
		}
	}
}
