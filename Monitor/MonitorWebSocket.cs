using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace StockMonitor.Monitor
{
	internal sealed class MonitorWebSocket : IMonitor
	{
		private const string BinanceStreamEndpoint = "wss://stream.binance.com:9443/stream";
		private const string AggregateTradeSuffix = "@aggTrade";
		private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

		private readonly Alarm mAlarm;
		private readonly UserData mUserData;
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
						await RunConnectionAsync(cancellationToken);
						if (!cancellationToken.IsCancellationRequested)
						{
							Logger.PrintLog(
								LogLevel.Warn,
								"A conexão WebSocket da Binance foi perdida. Uma nova conexão será tentada.");
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
						await Task.Delay(ReconnectDelay, cancellationToken);
					}
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				// Encerramento solicitado pela aplicação.
			}
		}

		private async Task RunConnectionAsync(CancellationToken cancellationToken)
		{
			using ClientWebSocket webSocket = new();
			await webSocket.ConnectAsync(new Uri(BinanceStreamEndpoint), cancellationToken);
			if(webSocket.State != WebSocketState.Open)
			{
				Logger.PrintLog(LogLevel.Error, "Falha ao abrir a conexão WebSocket da Binance.");
				throw new InvalidOperationException("Falha ao abrir a conexão WebSocket da Binance.");
			}
			Logger.PrintLog(LogLevel.Info, $"Conexão WebSocket aberta: {BinanceStreamEndpoint}.");

			using CancellationTokenSource connectionCancellation =
				CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			Channel<string> outgoingMessages = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
			{
				SingleReader = true,
				SingleWriter = true
			});

			Task sendTask = SendMessagesAsync(webSocket, outgoingMessages.Reader, connectionCancellation.Token);
			
			Task receiveTask = ReceiveMessagesAsync(webSocket, connectionCancellation.Token);

			// create a subscriber in a topic/"stock"
			outgoingMessages.Writer.TryWrite(CreateSubscriptionMessage());

			try
			{
				await Task.WhenAny(sendTask, receiveTask);
			}
			finally
			{
				connectionCancellation.Cancel();
				outgoingMessages.Writer.TryComplete();
			}

			try
			{
				await Task.WhenAll(sendTask, receiveTask);
			}
			catch (OperationCanceledException) when (connectionCancellation.IsCancellationRequested)
			{
				// Uma das tasks encerrou a conexão ou o encerramento foi solicitado.
			}
		}

		private async Task SendMessagesAsync(
			ClientWebSocket webSocket,
			ChannelReader<string> outgoingMessages,
			CancellationToken cancellationToken)
		{
			await foreach (string message in outgoingMessages.ReadAllAsync(cancellationToken))
			{
				byte[] messageBytes = Encoding.UTF8.GetBytes(message);
				await webSocket.SendAsync(
					new ArraySegment<byte>(messageBytes),
					WebSocketMessageType.Text,
					endOfMessage: true,
					cancellationToken);
			}
		}

		private async Task ReceiveMessagesAsync(
			ClientWebSocket webSocket,
			CancellationToken cancellationToken)
		{
			while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
			{
				string? message = await ReceiveMessageAsync(webSocket, cancellationToken);
				if (message is null)
				{
					return;
				}

				ProcessMessage(message);
			}
		}

		private void ProcessMessage(string message)
		{
			using JsonDocument document = JsonDocument.Parse(message);
			JsonElement root = document.RootElement;
			JsonElement eventPayload = root;

			if (root.TryGetProperty("data", out JsonElement dataElement) &&
				dataElement.ValueKind == JsonValueKind.Object)
			{
				eventPayload = dataElement;
			}

			if (!eventPayload.TryGetProperty("e", out _))
			{
				if (root.TryGetProperty("code", out JsonElement codeElement))
				{
					string errorMessage = root.TryGetProperty("msg", out JsonElement messageElement)
						? messageElement.GetString() ?? "sem detalhes"
						: "sem detalhes";
					Logger.PrintLog(
						LogLevel.Error,
						$"Erro ao inscrever o stream da Binance. Código: {codeElement}. " +
						$"Mensagem: {errorMessage}. JSON de saída: {message}");
					return;
				}

				if (root.TryGetProperty("result", out _))
				{
					Logger.PrintLog(LogLevel.Info, "Inscrição no stream de preço da Binance confirmada.");
					return;
				}

				Logger.PrintLog(LogLevel.Warn, $"Mensagem WebSocket não reconhecida. JSON de saída: {message}");
				return;
			}

			BinanceAggregateTradeEvent? tradeEvent =
				eventPayload.Deserialize<BinanceAggregateTradeEvent>();
			if (tradeEvent is null || !string.Equals(tradeEvent.EventType, "aggTrade", StringComparison.Ordinal))
			{
				return;
			}

			if (!TryReadPrice(tradeEvent.Price, out decimal price))
			{
				Logger.PrintLog(
					LogLevel.Error,
					$"O evento da Binance não contém um preço válido. JSON de saída: {message}");
				return;
			}

			string symbol = string.IsNullOrWhiteSpace(tradeEvent.Symbol)
				? mUserData.StockSymbol.ToUpperInvariant()
				: tradeEvent.Symbol;
			DateTimeOffset timestamp = tradeEvent.EventTime > 0
				? DateTimeOffset.FromUnixTimeMilliseconds(tradeEvent.EventTime)
				: DateTimeOffset.UtcNow;

			mAlarm.TryEnqueuePrice(new PriceReading(symbol, price, timestamp));
		}

		private string CreateSubscriptionMessage()
		{
			return JsonSerializer.Serialize(new BinanceSubscriptionRequest
			{
				Method = "SUBSCRIBE",
				Parameters = new[] { $"{mUserData.StockSymbol.ToLowerInvariant()}{AggregateTradeSuffix}" },
				Id = Guid.NewGuid().ToString()
			});
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

				if (result.MessageType != WebSocketMessageType.Text)
				{
					continue;
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
				decimal.TryParse(
					priceElement.GetString(),
					NumberStyles.Number,
					CultureInfo.InvariantCulture,
					out price))
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

		private sealed class BinanceSubscriptionRequest
		{
			[JsonPropertyName("method")]
			public string Method { get; init; } = string.Empty;

			[JsonPropertyName("params")]
			public string[] Parameters { get; init; } = Array.Empty<string>();

			[JsonPropertyName("id")]
			public string Id { get; init; } = string.Empty;
		}

		private sealed class BinanceAggregateTradeEvent
		{
			[JsonPropertyName("e")]
			public string? EventType { get; init; }

			[JsonPropertyName("E")]
			public long EventTime { get; init; }

			[JsonPropertyName("s")]
			public string? Symbol { get; init; }

			[JsonPropertyName("p")]
			public JsonElement Price { get; init; }
		}
	}
}
