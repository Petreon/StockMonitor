using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static StockMonitor.Logger;

namespace StockMonitor
{
	internal class Alarm
	{
		private readonly UserData mUserData;

		public Alarm(UserData userData)
		{
			if (userData is null)
			{
				const string message = "A referência de UserData não pode ser nula.";
				Logger.LogAndThrow(message, nameof(userData));
			}

			mUserData = userData!;
		}

	}
}
