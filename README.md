# StockMonitor

Aplicação de console para monitorar o preço de uma ação ou ativo e enviar um alerta por e-mail quando o preço atingir os limites configurados.

## Requisitos

- .NET 8
- Arquivo `config.yaml`
- Arquivo `.env`
- Credenciais válidas do servidor SMTP

Os arquivos `config.yaml` e `.env` devem estar no mesmo diretório do executável (`.exe`). O arquivo `.env` não deve ser versionado.

## Uso

A aplicação recebe exatamente três argumentos:

```text
StockMonitor.exe AÇÃO PREÇO_MÍNIMO PREÇO_MÁXIMO
```

Exemplos:

```powershell
StockMonitor.exe PETR4 35 45
StockMonitor.exe BNBUSDT 600 700
```

Os preços devem utilizar ponto como separador decimal, pois os argumentos são interpretados com `CultureInfo.InvariantCulture`:

```powershell
StockMonitor.exe PETR4 35.50 45.75
```

O programa encerra com `Ctrl+C`.

## Exemplo de `config.yaml`

```yaml
monitor:
  # Valores aceitos: HttpClient ou WebSocket
  connectionType: WebSocket

  # Usado pelo MonitorHttp. No modo WebSocket, o preço chega por stream
  # contínuo e este valor não controla a frequência dos eventos.
  requestIntervalSeconds: 5

smtp:
  host: "smtp.example.com"
  port: 587
  useSsl: true
  from: "stock-monitor@example.com"
  to: "destinatario@example.com"

alerts:
  # Intervalo mínimo entre tentativas de envio de alertas.
  sendIntervalSeconds: 5

  # Quantidade máxima de leituras aguardando processamento pelo Alarm.
  maxQueuedEvents: 10
```

### Tipos de monitoramento

#### `HttpClient`

Usa a BRAPI e consulta o preço em intervalos definidos por `requestIntervalSeconds`.

```yaml
monitor:
  connectionType: HttpClient
  requestIntervalSeconds: 5
```

O preço utilizado é `results[0].data.regularMarketPrice`.

#### `WebSocket`

Usa o stream contínuo `@aggTrade` da Binance. Não utiliza polling nem token de autorização para obter dados públicos de mercado.

Para Binance, informe o símbolo no formato do par de negociação, por exemplo:

```powershell
StockMonitor.exe BTCUSDT 60000 70000
```

## Exemplo de `.env`

Crie um arquivo chamado `.env` ao lado do executável:

```dotenv
BRAPI_TOKEN=seu_token_da_brapi
WEB_SOCKET_TOKEN=valor_compatibilidade
SMTP_USERNAME=seu_usuario_smtp
SMTP_PASSWORD=sua_senha_smtp
```

O `WEB_SOCKET_TOKEN` não é utilizado pelo stream público da Binance. Ele permanece no exemplo porque o carregador atual de ambiente ainda exige essa variável.

Não coloque credenciais reais neste repositório. O arquivo `.env.example` pode ser usado como modelo:

```powershell
Copy-Item .env.example .env
```

Depois, substitua os valores de exemplo pelas credenciais reais.

## Estrutura da aplicação

- `Program.cs`: lê os argumentos, carrega o YAML e o `.env`, e cria o monitor conforme `connectionType`.
- `UserData.cs`: armazena símbolo e limites de preço.
- `ConfReader/ConfRead.cs`: lê e valida o YAML.
- `ConfReader/ConfReadEnv.cs`: lê as variáveis do `.env`.
- `Monitor/MonitorHttp.cs`: consulta a BRAPI usando polling.
- `Monitor/MonitorWebSocket.cs`: mantém a conexão com a Binance e recebe eventos contínuos.
- `Alarm.cs`: consome a fila de preços, verifica os limites e envia os alertas SMTP.
- `Logger.cs`: registra mensagens no console e erros no `Console.Error`.

## APIs e deserialização

### BRAPI

O monitor HTTP utiliza o endpoint:

```text
GET https://brapi.dev/api/v2/stocks/quote?symbols=PETR4
```

O token é enviado no header `Authorization`.

Exemplo resumido de resposta:

```json
{
  "results": [
    {
      "symbol": "PETR4",
      "data": {
        "regularMarketPrice": 41.18,
        "regularMarketTime": "2026-06-14T05:15:42.000Z"
      }
    }
  ]
}
```

A resposta é desserializada com `System.Text.Json` nos modelos de [`Monitor/BrapiModels.cs`](Monitor/BrapiModels.cs). Os nomes dos campos são associados às propriedades C# por meio de `JsonPropertyName`, por exemplo:

```csharp
[JsonPropertyName("regularMarketPrice")]
public decimal? RegularMarketPrice { get; set; }
```

### Binance WebSocket Streams

O monitor WebSocket conecta-se a:

```text
wss://stream.binance.com:9443/stream
```

Depois envia uma inscrição para o stream do símbolo:

```json
{
  "method": "SUBSCRIBE",
  "params": ["bnbusdt@aggTrade"],
  "id": "identificador-da-requisicao"
}
```

Os eventos recebidos pelo endpoint combinado possuem o formato geral:

```json
{
  "stream": "bnbusdt@aggTrade",
  "data": {
    "e": "aggTrade",
    "E": 1672515782136,
    "s": "BNBUSDT",
    "p": "600.25"
  }
}
```

O monitor desempacota o campo `data` e utiliza:

- `e`: tipo do evento;
- `E`: horário do evento;
- `s`: símbolo;
- `p`: preço do negócio.

Como o preço pode ser retornado como string ou número, o código aceita os dois formatos antes de criar um `PriceReading`.

## Referências oficiais

- [Documentação da BRAPI](https://brapi.dev/docs)
- [BRAPI — cotações de ações](https://web-next.brapi.dev/docs/acoes)
- [Binance Spot WebSocket Market Streams](https://developers.binance.com/docs/binance-spot-api-docs/web-socket-streams)
- [Binance — método `ticker.price`](https://developers.binance.com/docs/binance-spot-api-docs/websocket-api/market-data-requests)
- [Microsoft — desserialização com `System.Text.Json`](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/deserialization)
- [Microsoft — atributo `JsonPropertyName`](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.serialization.jsonpropertynameattribute)
- [YamlDotNet](https://github.com/aaubry/YamlDotNet)

## Compilação e publicação

Para compilar:

```powershell
dotnet build -c Release
```

Para publicar:

```powershell
dotnet publish -c Release -o publish
```

Após publicar, copie o `.env` manualmente para a pasta que contém o executável. O `config.yaml` é copiado automaticamente durante o processo de build/publicação conforme a configuração do projeto, caso esteja utilizando o visual studio
