using System.Globalization;
using System.Net;
using System.Text.Json;
using StockMonitor.ConfReader;

namespace StockMonitor.Monitor
{
	internal sealed class MonitorHttp : IMonitor
	{
		private const string BrapiQuoteUrl = "https://brapi.dev/api/v2/stocks/quote?symbols={0}";

		private readonly Alarm mAlarm;
		private readonly UserData mUserData;
		private readonly HttpClient mHttpClient;
		private readonly TimeSpan mPollingInterval;
		private readonly CancellationTokenSource mCancellationTokenSource;
		private readonly Task mMonitorTask;
		public Task MonitoringTask => mMonitorTask;

		private bool mDisposed;

		public MonitorHttp(UserData userData, Alarm alarm)
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

			if (validUserData.Environment is null || string.IsNullOrWhiteSpace(validUserData.Environment.BrapiToken))
			{
				const string message = "O token BRAPI não foi carregado no UserData.";
				Logger.LogAndThrow(message);
			}

			AppConfiguration configuration = validUserData.Configuration!;
			EnvironmentConfiguration environment = validUserData.Environment!;

			mUserData = validUserData;
			//TODO: check the poolinginterval here
			mPollingInterval = TimeSpan.FromSeconds(configuration.Monitor.RequestIntervalSeconds);
			mHttpClient = new HttpClient
			{
				Timeout = TimeSpan.FromSeconds(30)
			};

			mHttpClient.DefaultRequestHeaders.Add("Authorization", environment.BrapiToken);
			mAlarm = alarm!;
			mCancellationTokenSource = new CancellationTokenSource();
			mMonitorTask = Task.Run(() => RunAsync(mCancellationTokenSource.Token));
		}


		private async Task RunAsync(CancellationToken cancellationToken)
		{
			ObjectDisposedException.ThrowIf(mDisposed, this);

			try
			{
				while (!cancellationToken.IsCancellationRequested)
				{
					await ReadPriceAsync(cancellationToken);
					await Task.Delay(mPollingInterval, cancellationToken);
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				// Encerramento solicitado pela aplicação.
			}
		}

		private async Task ReadPriceAsync(CancellationToken cancellationToken)
		{
			string symbol = Uri.EscapeDataString(mUserData.StockSymbol);
			string requestUrl = string.Format(CultureInfo.InvariantCulture, BrapiQuoteUrl, symbol);

			try
			{
				using HttpResponseMessage response = await mHttpClient.GetAsync(requestUrl, cancellationToken);
				string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

				if (response.StatusCode != HttpStatusCode.OK)
				{
					Logger.PrintLog(
						LogLevel.Error,
						$"Erro HTTP {(int)response.StatusCode} ({response.ReasonPhrase}) ao consultar a BRAPI. JSON de saída: {responseBody}");
					return;
				}

				BrapiQuoteResponse? quoteResponse = JsonSerializer.Deserialize<BrapiQuoteResponse>(responseBody);
				BrapiQuoteResult? quoteResult = quoteResponse?.Results?.FirstOrDefault();
				decimal? price = quoteResult?.Data?.RegularMarketPrice;

				if (price is null)
				{
					Logger.PrintLog(
						LogLevel.Error,
						$"A resposta HTTP 200 da BRAPI não contém 'regularMarketPrice'. JSON de saída: {responseBody}");
					return;
				}

				BrapiQuoteData quoteData = quoteResult!.Data!;
				DateTimeOffset timestamp = quoteData.RegularMarketTime ?? DateTimeOffset.UtcNow;
				PriceReading reading = new(
					quoteResult.Symbol ?? mUserData.StockSymbol,
					price.Value,
					timestamp);

				mAlarm.TryEnqueuePrice(reading);
			}
			catch (JsonException exception)
			{
				Logger.PrintLog(LogLevel.Error, $"Erro ao interpretar o JSON da BRAPI: {exception.Message}");
			}
			catch (HttpRequestException exception)
			{
				Logger.PrintLog(LogLevel.Error, $"Erro na requisição HTTP para a BRAPI: {exception.Message}");
			}
			catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
			{
				Logger.PrintLog(LogLevel.Error, $"Timeout na requisição HTTP para a BRAPI: {exception.Message}");
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				// Encerramento solicitado pela aplicação.
			}
		}

		public async ValueTask DisposeAsync()
		{
			if (!mDisposed)
			{
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

				mHttpClient.Dispose();
				mCancellationTokenSource.Dispose();
				Logger.PrintLog(LogLevel.Info, "Monitoramento HTTP encerrado e recursos liberados.");
			}
		}
	}
}
