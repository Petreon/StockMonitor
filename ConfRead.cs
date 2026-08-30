using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace StockMonitor
{
	internal class ConfRead
	{
		private readonly UserData mUserData;
		private readonly string mYamlPath;
		private AppConfiguration mConfiguration { get; }


		public ConfRead(UserData userData, string yamlPath = "config.yaml")
		{
			if (userData is null)
			{
				const string message = "A referência de UserData não pode ser nula.";
				Logger.LogAndThrow(message, nameof(userData));
			}

			if (string.IsNullOrWhiteSpace(yamlPath))
			{
				const string message = "O caminho do arquivo YAML não pode ser vazio.";
				Logger.LogAndThrow(message, nameof(yamlPath));
			}

			mUserData = userData!;
			mYamlPath = ResolveYamlPath(yamlPath);
			mConfiguration = ReadYaml();
		}


		private AppConfiguration ReadYaml()
		{
			if (!File.Exists(mYamlPath))
			{
				string message = $"O arquivo de configuração YAML não foi encontrado: '{mYamlPath}'.";
				Logger.PrintLog(LogLevel.Error, message);
				throw new FileNotFoundException(message, mYamlPath);
			}

			try
			{
				using StreamReader reader = File.OpenText(mYamlPath);
				IDeserializer deserializer = new DeserializerBuilder()
					.WithNamingConvention(CamelCaseNamingConvention.Instance)
					.Build();

				AppConfiguration? configuration = deserializer.Deserialize<AppConfiguration>(reader);
				if (configuration is null)
				{
					const string message = "O arquivo de configuração YAML está vazio.";
					Logger.PrintLog(LogLevel.Error, message);
					throw new InvalidDataException(message);
				}

				ValidateConfiguration(configuration);
				Logger.PrintLog(LogLevel.Info, $"Configuração YAML carregada: '{mYamlPath}'.");
				return configuration;
			}
			catch (YamlDotNet.Core.YamlException exception)
			{
				string message = $"Erro ao interpretar o arquivo YAML '{mYamlPath}': {exception.Message}";
				Logger.PrintLog(LogLevel.Error, message);
				throw new InvalidDataException(message, exception);
			}
		}

		private string ResolveYamlPath(string yamlPath)
		{
			return Path.IsPathRooted(yamlPath)
				? yamlPath
				: Path.Combine(AppContext.BaseDirectory, yamlPath);
			return Path.Combine(AppContext.BaseDirectory, yamlPath);
		}

		private void ValidateConfiguration(AppConfiguration configuration)
		{
			if (configuration.Monitor is null)
			{
				LogConfigurationError("A seção 'monitor' é obrigatória.");
			}

			MonitorConfiguration monitor = configuration.Monitor!;

			if (monitor.ConnectionType is null ||
				!Enum.IsDefined(monitor.ConnectionType.Value))
			{
				LogConfigurationError("A configuração 'monitor.connectionType' deve ser HttpClient ou WebSocket.");
			}

			if (monitor.RequestIntervalSeconds <= 0)
			{
				LogConfigurationError("A configuração 'monitor.requestIntervalSeconds' deve ser maior que zero.");
			}

			if (configuration.Smtp is null || string.IsNullOrWhiteSpace(configuration.Smtp.Host))
			{
				LogConfigurationError("A configuração 'smtp.host' é obrigatória.");
			}

			SmtpConfiguration smtp = configuration.Smtp!;

			if (smtp.Port is < 1 or > 65535)
			{
				LogConfigurationError("A configuração 'smtp.port' deve estar entre 1 e 65535.");
			}

			if (string.IsNullOrWhiteSpace(smtp.From))
			{
				LogConfigurationError("A configuração 'smtp.from' é obrigatória.");
			}

			if (string.IsNullOrWhiteSpace(smtp.To))
			{
				LogConfigurationError("A configuração 'smtp.to' é obrigatória.");
			}

			if (configuration.Alerts is null)
			{
				LogConfigurationError("A seção 'alerts' é obrigatória.");
			}

			AlertConfiguration alerts = configuration.Alerts!;

			if (alerts.SendIntervalSeconds <= 0)
			{
				LogConfigurationError("A configuração 'alerts.sendIntervalSeconds' deve ser maior que zero.");
			}

			if (alerts.MaxQueuedEvents <= 0)
			{
				LogConfigurationError("A configuração 'alerts.maxQueuedEvents' deve ser maior que zero.");
			}
		}

		private void LogConfigurationError(string message)
		{
			Logger.PrintLog(LogLevel.Error, message);
			throw new InvalidDataException(message);
		}
	}
}
