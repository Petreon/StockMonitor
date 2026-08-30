using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static StockMonitor.Logger;
using System.Threading.Channels;
using StockMonitor.Monitor;

namespace StockMonitor
{
	internal class Alarm
	{
		private readonly UserData mUserData;
		private readonly Channel<PriceReading> mPriceQueue;

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

			mPriceQueue = Channel.CreateBounded<PriceReading>(new BoundedChannelOptions(
				mUserData.Configuration!.Alerts!.MaxQueuedEvents)
			{
				FullMode = BoundedChannelFullMode.Wait,
				SingleReader = true,
				SingleWriter = false
			});
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
	}
}
