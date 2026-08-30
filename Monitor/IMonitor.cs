namespace StockMonitor.Monitor
{
	public enum ConnectionType
	{
		HttpClient = 0,
		WebSocket = 1,
	}
	public interface IMonitor : IAsyncDisposable
	{
		Task MonitoringTask { get; }
	}
}
