namespace StockMonitor
{
	internal class Program
	{
		private const Int32 NUMBERARGS = 3;
	
		private static ConfRead		mConfRead;			// ConfRead para ler o arquivo yaml e conseguir lidar com possiveis casos do programa.
		private static Monitor		mMonitor;			// background thread/task para monitorar o preço da ação.
		private static Alarm		mAlarm;				// Classe para saber quando deve gerar um alarme.


		static void Main(string[] args)
		{
			if(args.Length != NUMBERARGS)
			{
				Logger.PrintLog(LogLevel.Error, $"Erro na inicilização, quantidades de argumentos invalidas requer {NUMBERARGS}, Recebido {args.Length}");
			}

						

		}
	}
}
