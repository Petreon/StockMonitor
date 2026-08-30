using System.Net.Http;

namespace StockMonitor.Monitor
{
	public enum ConnectionType
	{
		HttpClient = 0,
		WebSocket = 1,
	}

	internal class Monitor
	{
		private readonly UserData mUserData;
		
		private string	mStockName;
		private Task	mMonitorTask;
		private CancellationTokenSource mCancellationToken;	//cancellation token pra liberar a task

		// Observações, um problema desse desafio e que não e simples conseguir uma APi boa principalemnte em tempo real que
		// não tenha um custo alto

		private HttpClient mHttpClient;		// client para fazer request para api financeira
		private string mBrapiToken;         // brapi token


		public Monitor(UserData userData, ConnectionType connection, string Token)
		{
			if (userData is null)
			{
				const string message = "A referência de UserData não pode ser nula.";
				Logger.PrintLog(LogLevel.Error, message);
				throw new ArgumentNullException(nameof(userData), message);
			}

			//TODO: essa implementação vai ser mudada para usar um IMonitor para conseguir separar
			// a interface de conexão Http e Websocket.

			mUserData = userData;
			switch (connection)
			{
				case ConnectionType.HttpClient:
					mHttpClient = new HttpClient();
					mMonitorTask = Task.Run(HttpMonitor);
					break;
				case ConnectionType.WebSocket:
					break;
			}
		}
		private async Task HttpMonitor()
		{
			mCancellationToken = new CancellationTokenSource();
			// docs da brapi.dev
			mHttpClient.DefaultRequestHeaders.Add("Authorization", mBrapiToken);

			while(!mCancellationToken.IsCancellationRequested)
			{
				
			}

		}

		private async Task WebSocketMonitor()
		{

		}

	}
}
