using System.Text.Json.Serialization;

namespace StockMonitor.Monitor
{
	internal sealed class BrapiQuoteResponse
	{
		[JsonPropertyName("results")]
		public List<BrapiQuoteResult>? Results { get; set; }
	}

	internal sealed class BrapiQuoteResult
	{
		[JsonPropertyName("requestedSymbol")]
		public string? RequestedSymbol { get; set; }

		[JsonPropertyName("symbol")]
		public string? Symbol { get; set; }

		[JsonPropertyName("data")]
		public BrapiQuoteData? Data { get; set; }
	}

	internal sealed class BrapiQuoteData
	{
		[JsonPropertyName("regularMarketPrice")]
		public decimal? RegularMarketPrice { get; set; }

		[JsonPropertyName("regularMarketTime")]
		public DateTimeOffset? RegularMarketTime { get; set; }
	}
}
