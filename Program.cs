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

			await using Alarm alarm = new(userData);

			await using IMonitor monitor = CreateMonitor(userData, alarm);

			// shutdown do Ctrl+C do console pra poder finalizar o programa:
			using CancellationTokenSource shutdown = new();
			ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
			{
				eventArgs.Cancel = true;
				shutdown.Cancel();
			};

			Console.CancelKeyPress += cancelHandler;
			try
			{
				Logger.PrintLog(LogLevel.Info, "Monitoramento iniciado. Pressione Ctrl+C para encerrar.");
				Task shutdownTask = Task.Delay(Timeout.InfiniteTimeSpan, shutdown.Token);
				Task completedTask = await Task.WhenAny(monitor.MonitoringTask, shutdownTask);

				if (completedTask == monitor.MonitoringTask)
				{
					await monitor.MonitoringTask;
				}
			}
			finally
			{
				Console.CancelKeyPress -= cancelHandler;
				await monitor.DisposeAsync(); // dispose the monitor sucessfully
			}

			Logger.PrintLog(LogLevel.Info, "Monitoramento encerrado.");
			return 0;
		}

		private static IMonitor CreateMonitor(UserData userData, Alarm alarm)
		{
			ConnectionType connectionType = userData.Configuration!.Monitor.ConnectionType!.Value;
			return connectionType switch
			{
				ConnectionType.HttpClient => new MonitorHttp(userData, alarm),
				ConnectionType.WebSocket => new MonitorWebSocket(userData, alarm),
				_ => throw new InvalidOperationException($"Tipo de conexão não suportado: {connectionType}.")
			};
		}
	}
}
