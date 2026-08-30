using System.Globalization;

namespace StockMonitor
{
	public sealed class UserData
	{
		public string StockSymbol { get; }
		// Usando decimal por ser "financeiramente aceitavel "
		public decimal MinimumPrice { get; }
		public decimal MaximumPrice { get; }

		public UserData(string stockSymbol, decimal minimumPrice, decimal maximumPrice)
		{
			if (string.IsNullOrWhiteSpace(stockSymbol))
			{
				const string message = "O símbolo da ação não pode ser vazio.";
				LogAndThrow(message, nameof(stockSymbol));
			}

			if (minimumPrice > maximumPrice)
			{
				const string message = "O preço mínimo não pode ser maior que o preço máximo.";
				LogAndThrow(message);
			}

			StockSymbol = stockSymbol.Trim();
			MinimumPrice = minimumPrice;
			MaximumPrice = maximumPrice;
		}

		public static UserData FromArgs(string[] args)
		{
			if (args is null)
			{
				const string message = "Os argumentos da aplicação não podem ser nulos.";
				Logger.PrintLog(LogLevel.Error, message);
				throw new ArgumentNullException(nameof(args), message);
			}

			if (args.Length != 3)
			{
				string message =
					$"A quantidade de argumentos deve ser 3: ação, preço mínimo e preço máximo. Recebido: {args.Length}.";
				Logger.LogAndThrow(message);
			}

			if (!decimal.TryParse(args[1], NumberStyles.Number, CultureInfo.InvariantCulture, out decimal minimumPrice))
			{
				string message = $"O preço mínimo '{args[1]}' é inválido.";
				Logger.LogAndThrow(message);
			}

			if (!decimal.TryParse(args[2], NumberStyles.Number, CultureInfo.InvariantCulture, out decimal maximumPrice))
			{
				string message = $"O preço máximo '{args[2]}' é inválido.";
				Logger.LogAndThrow(message);
			}

			return new UserData(args[0], minimumPrice, maximumPrice);
		}


	}
}
