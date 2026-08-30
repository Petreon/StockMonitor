using StockMonitor.Monitor;

namespace StockMonitor.ConfReader
{
	public sealed class AppConfiguration
	{
		public MonitorConfiguration Monitor { get; set; } = new();
		public ApiConfiguration Api { get; set; } = new();
		public SmtpConfiguration Smtp { get; set; } = new();
		public AlertConfiguration Alerts { get; set; } = new();
	}

	public sealed class MonitorConfiguration
	{
		public ConnectionType? ConnectionType { get; set; }
		public int RequestIntervalSeconds { get; set; }
	}

	public sealed class ApiConfiguration
	{
		public string? HttpUrl { get; set; }
		public string? WebSocketUrl { get; set; }
	}

	public sealed class SmtpConfiguration
	{
		public string? Host { get; set; }
		public int Port { get; set; }
		public bool UseSsl { get; set; }
		public string? From { get; set; }
		public string? To { get; set; }
	}

	public sealed class AlertConfiguration
	{
		public int SendIntervalSeconds { get; set; }
		public int MaxQueuedEvents { get; set; }
	}

	public sealed class EnvironmentConfiguration
	{
		public string BrapiToken { get; init; } = string.Empty;
		public string WebSocketToken { get; init; } = string.Empty;
		public string SmtpUsername { get; init; } = string.Empty;
		public string SmtpPassword { get; init; } = string.Empty;
	}
}
