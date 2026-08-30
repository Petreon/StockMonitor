using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StockMonitor
{
	internal class ConfRead
	{
		private readonly UserData mUserData;

		public ConfRead(UserData userData)
		{
			if (userData is null)
			{
				const string message = "A referência de UserData não pode ser nula.";
				Logger.PrintLog(LogLevel.Error, message);
				throw new ArgumentNullException(nameof(userData), message);
			}

			mUserData = userData;
		}

	}
}
