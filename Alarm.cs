using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Threading.Channels;
using StockMonitor.ConfReader;
using StockMonitor.Monitor;

namespace StockMonitor
{
	internal sealed class Alarm : IAsyncDisposable
	{
		private readonly UserData mUserData;
		private readonly Channel<PriceReading> mPriceQueue;
		private readonly TimeSpan mSendInterval;
		private readonly CancellationTokenSource mCancellationTokenSource;
		private readonly Task mConsumerTask;
		private bool mDisposed;

		public Alarm(UserData userData)
		{
			if (userData is null)
			{
				const string message = "A referência de UserData não pode ser nula.";
				Logger.LogAndThrow(message, nameof(userData));
			}

			mUserData = userData!;

			if (mUserData.Configuration?.Alerts is null)
			{
				const string message = "A configuração de alertas não foi carregada no UserData.";
				Logger.LogAndThrow(message);
			}

			if (mUserData.Configuration!.Smtp is null)
			{
				const string message = "A configuração SMTP não foi carregada no UserData.";
				Logger.LogAndThrow(message);
			}

			if (mUserData.Environment is null)
			{
				const string message = "As credenciais do ambiente não foram carregadas no UserData.";
				Logger.LogAndThrow(message);
			}

			mPriceQueue = Channel.CreateBounded<PriceReading>(new BoundedChannelOptions(
				mUserData.Configuration!.Alerts!.MaxQueuedEvents)
			{
				FullMode = BoundedChannelFullMode.Wait,
				SingleReader = true,
				SingleWriter = false
			});

			mSendInterval = TimeSpan.FromSeconds(mUserData.Configuration.Alerts.SendIntervalSeconds);
			mCancellationTokenSource = new CancellationTokenSource();
			mConsumerTask = Task.Run(() => ConsumePricesAsync(mCancellationTokenSource.Token));
		}

		public bool TryEnqueuePrice(PriceReading reading)
		{
			if (mPriceQueue.Writer.TryWrite(reading))
			{
				return true;
			}

			Logger.PrintLog(
				LogLevel.Warn,
				$"A fila de preços atingiu o limite de {mUserData.Configuration!.Alerts!.MaxQueuedEvents} eventos. " +
				$"O evento de '{reading.Symbol}' em {reading.Timestamp:O} foi descartado.");
			return false;
		}

		private async Task ConsumePricesAsync(CancellationToken cancellationToken)
		{
			DateTimeOffset? lastAlertAttempt = null;

			try
			{
				// reads all queue data.
				await foreach (PriceReading reading in mPriceQueue.Reader.ReadAllAsync(cancellationToken))
				{
					if (!ReachedPriceLimit(reading.Price))
					{
						continue;
					}

					if (lastAlertAttempt is not null)
					{
					    TimeSpan elapsed = DateTimeOffset.UtcNow - lastAlertAttempt.Value;
					    if (elapsed < mSendInterval)
					    {
					        // Ignora este evento para não acumular/atrasar a fila
					        continue; 
					    }
					}
					
					lastAlertAttempt = DateTimeOffset.UtcNow;
					await SendAlertAsync(reading, cancellationToken);
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				// Encerramento solicitado pela aplicação.
			}
		}

		private bool ReachedPriceLimit(decimal price)
		{
			return price <= mUserData.MinimumPrice || price >= mUserData.MaximumPrice;
		}

		private async Task SendAlertAsync(PriceReading reading, CancellationToken cancellationToken)
		{
			SmtpConfiguration smtpConfiguration = mUserData.Configuration!.Smtp!;
			EnvironmentConfiguration environment = mUserData.Environment!;

			string price = reading.Price.ToString(CultureInfo.InvariantCulture);
			string minimumPrice = mUserData.MinimumPrice.ToString(CultureInfo.InvariantCulture);
			string maximumPrice = mUserData.MaximumPrice.ToString(CultureInfo.InvariantCulture);
			string subject = $"Alerta de preço: {reading.Symbol}";
			string body =
				$"O preço monitorado atingiu um dos limites configurados.{Environment.NewLine}{Environment.NewLine}" +
				$"Ação: {reading.Symbol}{Environment.NewLine}" +
				$"Preço atual: {price}{Environment.NewLine}" +
				$"Preço mínimo: {minimumPrice}{Environment.NewLine}" +
				$"Preço máximo: {maximumPrice}{Environment.NewLine}" +
				$"Data da leitura: {reading.Timestamp:O}";

			try
			{
				using MailMessage mailMessage = new(
					new MailAddress(smtpConfiguration.From!),
					new MailAddress(smtpConfiguration.To!))
				{
					Subject = subject,
					Body = body,
					IsBodyHtml = false
				};

				using SmtpClient smtpClient = new(smtpConfiguration.Host!, smtpConfiguration.Port)
				{
					EnableSsl = smtpConfiguration.UseSsl,
					UseDefaultCredentials = false,
					Credentials = new NetworkCredential(
						environment.SmtpUsername,
						environment.SmtpPassword),
					DeliveryMethod = SmtpDeliveryMethod.Network
				};

				await smtpClient.SendMailAsync(mailMessage, cancellationToken);
				Logger.PrintLog(LogLevel.Info, $"Alerta SMTP enviada para o evento de '{reading.Symbol}'.");
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				// Encerramento solicitado pela aplicação.
			}
			catch (SmtpException exception)
			{
				Logger.PrintLog(LogLevel.Error, $"Erro ao enviar alerta SMTP: {exception.Message}");
			}
			catch (FormatException exception)
			{
				Logger.PrintLog(LogLevel.Error, $"Endereço de e-mail SMTP inválido: {exception.Message}");
			}
		}

		public async ValueTask DisposeAsync()
		{
			if (mDisposed)
			{
				return;
			}

			mDisposed = true;
			mPriceQueue.Writer.TryComplete();
			mCancellationTokenSource.Cancel();

			try
			{
				await mConsumerTask.ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// Encerramento solicitado durante a liberação dos recursos.
			}

			mCancellationTokenSource.Dispose();
		}
	}
}
