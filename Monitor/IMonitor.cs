namespace StockMonitor.Monitor
{
	public interface IMonitor : IAsyncDisposable
	{
		Task MonitoringTask { get; }
	}
}
