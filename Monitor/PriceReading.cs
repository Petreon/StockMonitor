namespace StockMonitor.Monitor
{
	public sealed record PriceReading(
		string Symbol,
		decimal Price,
		DateTimeOffset Timestamp);
}
