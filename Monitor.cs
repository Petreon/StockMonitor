using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StockMonitor
{
	public enum ConectionType
	{
		HttpClient	= 0,
		WebSocket	= 1,
	}
	
	internal class Monitor
	{

		private String	mStockName;
		private Task	mMonitorTask;
		private CancellationTokenSource mCancellationToken;	//cancellation token pra liberar a task

		// Observações, um problema desse desafio e que não e simples conseguir uma APi boa principalemnte em tempo real que
		// não tenha um custo alto

		private HttpClient mHttpClient;		// client para fazer request para api financeira
		private String mBrapiToken;			// brapi token
		/// <summary>
		/// 
		/// </summary>
		/// <param name="stock">Stock anme to find it</param>
		public Monitor(String stock, ConectionType connection, String Token)
		{
			mStockName = stock;

			switch(connection)
			{
				case ConectionType.HttpClient:
					mHttpClient = new HttpClient();
					mMonitorTask = Task.Run(HttpMonitor);
					break;
				case ConectionType.WebSocket:
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
