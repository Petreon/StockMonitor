using DotNetEnv;

namespace StockMonitor.ConfReader
{
	internal class ConfReadEnv
	{
		private readonly string mEnvPath;

		public ConfReadEnv(string envPath = ".env")
		{
			if (string.IsNullOrWhiteSpace(envPath))
			{
				const string message = "O caminho do arquivo .env não pode ser vazio.";
				Logger.LogAndThrow(message, nameof(envPath));
			}

			mEnvPath = ResolveEnvPath(envPath);
			Credentials = ReadEnv();
		}

		public EnvironmentConfiguration Credentials { get; }

		private EnvironmentConfiguration ReadEnv()
		{
			if (!File.Exists(mEnvPath))
			{
				string message = $"O arquivo de credenciais .env não foi encontrado: '{mEnvPath}'.";
				Logger.PrintLog(LogLevel.Error, message);
				throw new FileNotFoundException(message, mEnvPath);
			}

			try
			{
				Env.Load(mEnvPath);
			}
			catch (Exception exception)
			{
				string message = $"Erro ao carregar o arquivo .env '{mEnvPath}': {exception.Message}";
				Logger.PrintLog(LogLevel.Error, message);
				throw new InvalidDataException(message, exception);
			}

			return new EnvironmentConfiguration
			{
				BrapiToken = ReadRequired("BRAPI_TOKEN"),
				WebSocketToken = ReadRequired("WEB_SOCKET_TOKEN"),
				SmtpUsername = ReadRequired("SMTP_USERNAME"),
				SmtpPassword = ReadRequired("SMTP_PASSWORD")
			};
		}

		private static string ResolveEnvPath(string envPath)
		{
			if (Path.IsPathRooted(envPath))
			{
				return envPath;
			}

			string applicationPath = Path.Combine(AppContext.BaseDirectory, envPath);
			if (File.Exists(applicationPath))
			{
				return applicationPath;
			}

			return Path.GetFullPath(envPath);
		}

		private static string ReadRequired(string key)
		{
			string? value = Environment.GetEnvironmentVariable(key);
			if (string.IsNullOrWhiteSpace(value))
			{
				string message = $"A variável obrigatória '{key}' não foi encontrada no arquivo .env.";
				Logger.PrintLog(LogLevel.Error, message);
				throw new InvalidDataException(message);
			}

			return value.Trim();
		}
	}
}
