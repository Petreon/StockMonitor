using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using StockMonitor.ConfReader;

namespace StockMonitor.Monitor
{
	internal sealed class MonitorWebSocket : IMonitor
	{
		private const string BinanceWebSocketEndpoint = "wss://ws-api.binance.com/ws-api/v3";
		private const string TickerPriceMethod = "ticker.price";

		private readonly Alarm mAlarm;
		private readonly UserData mUserData;
		private readonly TimeSpan mPollingInterval;
		private readonly CancellationTokenSource mCancellationTokenSource;
		private readonly Task mMonitorTask;
		private bool mDisposed;

		public Task MonitoringTask => mMonitorTask;

		public MonitorWebSocket(UserData userData, Alarm alarm)
		{
			if (userData is null)
			{
				const string message = "A referência de UserData não pode ser nula.";
				Logger.LogAndThrow(message, nameof(userData));
			}

			if (alarm is null)
			{
				const string message = "A referência de Alarm não pode ser nula.";
				Logger.LogAndThrow(message, nameof(alarm));
			}

			UserData validUserData = userData!;
			if (validUserData.Configuration is null)
			{
				const string message = "A configuração YAML não foi carregada no UserData.";
				Logger.LogAndThrow(message);
			}

			mUserData = validUserData;
			mAlarm = alarm!;
			mPollingInterval = TimeSpan.FromSeconds(validUserData.Configuration!.Monitor.RequestIntervalSeconds);
			mCancellationTokenSource = new CancellationTokenSource();
			mMonitorTask = Task.Run(() => RunAsync(mCancellationTokenSource.Token));
		}

		private async Task RunAsync(CancellationToken cancellationToken)
		{
			try
			{
				while (!cancellationToken.IsCancellationRequested)
				{
					try
					{
						using ClientWebSocket webSocket = new();
						await webSocket.ConnectAsync(new Uri(BinanceWebSocketEndpoint), cancellationToken);
						Logger.PrintLog(LogLevel.Info, $"Conexão WebSocket aberta: {BinanceWebSocketEndpoint}.");

						while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
						{
							bool connectionIsUsable = await RequestPriceAsync(webSocket, cancellationToken);
							if (!connectionIsUsable)
							{
								break;
							}

							await Task.Delay(mPollingInterval, cancellationToken);
						}
					}
					catch (WebSocketException exception)
					{
						Logger.PrintLog(LogLevel.Error, $"Erro na conexão WebSocket da Binance: {exception.Message}");
					}
					catch (JsonException exception)
					{
						Logger.PrintLog(LogLevel.Error, $"Erro ao interpretar o JSON da Binance: {exception.Message}");
					}
					catch (Exception exception) when (exception is IOException or InvalidOperationException)
					{
						Logger.PrintLog(LogLevel.Error, $"Erro no monitoramento WebSocket da Binance: {exception.Message}");
					}

					if (!cancellationToken.IsCancellationRequested)
					{
						await Task.Delay(mPollingInterval, cancellationToken);
					}
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				// Encerramento solicitado pela aplicação.
			}
		}

		private async Task<bool> RequestPriceAsync(ClientWebSocket webSocket, CancellationToken cancellationToken)
		{
			string requestJson = JsonSerializer.Serialize(new BinanceTickerPriceRequest
			{
				Id = Guid.NewGuid().ToString(),
				Method = TickerPriceMethod,
				Parameters = new BinanceTickerPriceParameters
				{
					Symbol = mUserData.StockSymbol.ToUpperInvariant()
				}
			});

			byte[] requestBytes = Encoding.UTF8.GetBytes(requestJson);
			await webSocket.SendAsync(
				new ArraySegment<byte>(requestBytes),
				WebSocketMessageType.Text,
				endOfMessage: true,
				cancellationToken);

			string? responseJson = await ReceiveMessageAsync(webSocket, cancellationToken);
			if (responseJson is null)
			{
				Logger.PrintLog(LogLevel.Warn, "A conexão WebSocket da Binance foi encerrada pelo servidor.");
				return false;
			}

			BinanceWebSocketResponse? response;
			try
			{
				response = JsonSerializer.Deserialize<BinanceWebSocketResponse>(responseJson);
			}
			catch (JsonException exception)
			{
				Logger.PrintLog(
					LogLevel.Error,
					$"Erro ao interpretar o JSON de saída da Binance: {exception.Message}. JSON de saída: {responseJson}");
				return true;
			}

			if (response is null)
			{
				Logger.PrintLog(LogLevel.Error, $"A Binance retornou um JSON vazio. JSON de saída: {responseJson}");
				return true;
			}

			if (response.Status != 200)
			{
				string errorMessage = response.Error is null
					? "sem detalhes de erro"
					: $"código {response.Error.Code}: {response.Error.Message}";
				Logger.PrintLog(
					LogLevel.Error,
					$"A Binance retornou status {response.Status} ({errorMessage}). JSON de saída: {responseJson}");
				return true;
			}

			if (response.Result is null || !TryReadPrice(response.Result.Price, out decimal price))
			{
				Logger.PrintLog(
					LogLevel.Error,
					$"A resposta da Binance não contém um preço válido. JSON de saída: {responseJson}");
				return true;
			}

			string symbol = string.IsNullOrWhiteSpace(response.Result.Symbol)
				? mUserData.StockSymbol
				: response.Result.Symbol;
			mAlarm.TryEnqueuePrice(new PriceReading(symbol, price, DateTimeOffset.UtcNow));
			return true;
		}

		private static async Task<string?> ReceiveMessageAsync(
			ClientWebSocket webSocket,
			CancellationToken cancellationToken)
		{
			using MemoryStream messageStream = new();
			byte[] buffer = new byte[4096];

			while (true)
			{
				WebSocketReceiveResult result = await webSocket.ReceiveAsync(
					new ArraySegment<byte>(buffer),
					cancellationToken);

				if (result.MessageType == WebSocketMessageType.Close)
				{
					return null;
				}

				messageStream.Write(buffer, 0, result.Count);
				if (result.EndOfMessage)
				{
					return Encoding.UTF8.GetString(messageStream.ToArray());
				}
			}
		}

		private static bool TryReadPrice(JsonElement priceElement, out decimal price)
		{
			if (priceElement.ValueKind == JsonValueKind.Number && priceElement.TryGetDecimal(out price))
			{
				return true;
			}

			if (priceElement.ValueKind == JsonValueKind.String &&
				decimal.TryParse(priceElement.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out price))
			{
				return true;
			}

			price = default;
			return false;
		}

		public async ValueTask DisposeAsync()
		{
			if (mDisposed)
			{
				return;
			}

			mDisposed = true;
			mCancellationTokenSource.Cancel();

			try
			{
				await mMonitorTask.ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// Encerramento solicitado durante a liberação dos recursos.
			}

			mCancellationTokenSource.Dispose();
		}

		private sealed class BinanceTickerPriceRequest
		{
			[JsonPropertyName("id")]
			public string Id { get; init; } = string.Empty;

			[JsonPropertyName("method")]
			public string Method { get; init; } = string.Empty;

			[JsonPropertyName("params")]
			public BinanceTickerPriceParameters Parameters { get; init; } = new();
		}

		private sealed class BinanceTickerPriceParameters
		{
			[JsonPropertyName("symbol")]
			public string Symbol { get; init; } = string.Empty;
		}

		private sealed class BinanceWebSocketResponse
		{
			[JsonPropertyName("status")]
			public int Status { get; init; }

			[JsonPropertyName("result")]
			public BinanceTickerPriceResult? Result { get; init; }

			[JsonPropertyName("error")]
			public BinanceWebSocketError? Error { get; init; }
		}

		private sealed class BinanceTickerPriceResult
		{
			[JsonPropertyName("symbol")]
			public string? Symbol { get; init; }

			[JsonPropertyName("price")]
			public JsonElement Price { get; init; }
		}

		private sealed class BinanceWebSocketError
		{
			[JsonPropertyName("code")]
			public int Code { get; init; }

			[JsonPropertyName("msg")]
			public string Message { get; init; } = string.Empty;
		}
	}
}
