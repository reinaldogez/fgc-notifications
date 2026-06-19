# fcg-notifications

Worker de notificações da plataforma **FIAP Cloud Games (FCG)**. É um **consumidor puro**
(sem API de negócio): escuta eventos no RabbitMQ e emite a notificação correspondente. As
notificações são entregues como **log com prefixo `[EMAIL]`** (não há provedor de e-mail real
neste serviço — o log é o efeito observável).

## Sumário

- [fcg-notifications](#fcg-notifications)
  - [Sumário](#sumário)
  - [O que o serviço faz](#o-que-o-serviço-faz)
    - [Filas e exchanges consumidas](#filas-e-exchanges-consumidas)
  - [Arquitetura](#arquitetura)
  - [Pré-requisitos](#pré-requisitos)
  - [Token para restaurar o `Fcg.Contracts`](#token-para-restaurar-o-fcgcontracts)
  - [Build e testes locais](#build-e-testes-locais)
  - [Docker](#docker)
    - [Rodando o container](#rodando-o-container)
  - [Observabilidade](#observabilidade)
  - [Health checks](#health-checks)
  - [Imagem e visibilidade no GHCR](#imagem-e-visibilidade-no-ghcr)
  - [Deploy](#deploy)

## O que o serviço faz

| Evento consumido | Condição | Notificação emitida |
| :--- | :--- | :--- |
| `UserCreatedEvent` | — | Boas-vindas |
| `PaymentProcessedEvent` | `Status = Approved` | Confirmação de compra |
| `PaymentProcessedEvent` | `Status = Rejected` | Recusa de compra (com o motivo) |

O despacho do `PaymentProcessedEvent` é feito **pelo `Status`**: `Approved` gera a confirmação,
`Rejected` gera a recusa.

### Filas e exchanges consumidas

| Fila | Exchange | Evento |
| :--- | :--- | :--- |
| `user-created.fcg-notifications` | `user-created` | `UserCreatedEvent` |
| `payment-processed.fcg-notifications` | `payment-processed` | `PaymentProcessedEvent` |

A **idempotência** é garantida por `MessageId` via Redis (`SET NX` com TTL): a reentrega da
mesma mensagem não duplica o efeito.

## Arquitetura

Três camadas, **sem camada de domínio** (o serviço é stateless, sem agregado nem banco de
domínio):

- **`Fcg.Notifications.Application`** — os *handlers* das notificações (núcleo portável).
- **`Fcg.Notifications.Infrastructure`** — transporte (MassTransit/RabbitMQ), idempotência (Redis).
- **`Fcg.Notifications.Api`** — host: composição, health checks e observabilidade.

Direção de dependência: `Api → Infrastructure → Application`.

## Pré-requisitos

- **.NET 10 SDK**
- **Redis** acessível (idempotência; dependência dura)
- **RabbitMQ** acessível (transporte dos eventos)
- Acesso de leitura ao feed **GitHub Packages** para restaurar o pacote `Fcg.Contracts`
  (ver abaixo — exige token mesmo sendo público).

## Token para restaurar o `Fcg.Contracts`

O serviço referencia o pacote **`Fcg.Contracts`** (contratos de eventos), publicado no feed
**GitHub Packages**. Esse feed **exige autenticação mesmo para pacotes públicos** — diferente do
`ghcr.io` de imagens, que serve anônimo. Logo, o `dotnet restore` local precisa de um
**Personal Access Token (PAT)** com o escopo **`read:packages`**.

O `nuget.config` versionado declara o source `github-fcg` **sem** credenciais. Forneça o token
**fora do repositório**, de uma destas formas (deixe o `nuget.config` versionado intacto):

**Opção A — `nuget.config` no nível de usuário (recomendado):** grava a credencial no
config global do NuGet (`%AppData%\NuGet\NuGet.Config` no Windows / `~/.nuget/NuGet/NuGet.Config`),
fora do repo:

```bash
dotnet nuget update source github-fcg \
  --username <seu-usuario-github> \
  --password <SEU_PAT_read:packages> \
  --store-password-in-clear-text \
  --configfile "<caminho-do-nuget.config-de-usuario>"
```

**Opção B — variável de ambiente** (sem gravar em disco):

```bash
# bash
export NuGetPackageSourceCredentials_github-fcg="Username=<seu-usuario-github>;Password=<SEU_PAT_read:packages>"
```
```powershell
# PowerShell
$env:NuGetPackageSourceCredentials_github-fcg = "Username=<seu-usuario-github>;Password=<SEU_PAT_read:packages>"
```

> **Atenção:** mantenha token, senha ou credencial **fora** do `nuget.config` versionado e de
> qualquer arquivo rastreado.

## Build e testes locais

Com o token configurado:

```bash
# restaura, compila a solution inteira
dotnet build fcg-notifications.slnx

# testes (unit + integração)
dotnet test
```

> Os testes de **integração** sobem Redis e RabbitMQ reais via **Testcontainers** — é preciso ter
> um runtime de containers (Docker) disponível na máquina.

## Docker

O `dotnet restore` ocorre **dentro** do build da imagem, então o token do `Fcg.Contracts` entra
via **secret mount do BuildKit** (não fica em nenhuma layer da imagem final):

```bash
DOCKER_BUILDKIT=1 docker build \
  --secret id=gh_token,src=<arquivo-com-o-PAT> \
  -t fcg-notifications .
```

> `src` aponta para um **arquivo** contendo apenas o PAT (`read:packages`). No Linux/macOS dá para
> usar `src=<(echo -n "$SEU_PAT")`.

### Rodando o container

O serviço lê a configuração de variáveis de ambiente (chaves aninhadas usam `__`):

```bash
docker run --rm -p 8080:8080 \
  -e Redis__Connection="redis:6379" \
  -e RabbitMq__Host="rabbitmq" \
  -e RabbitMq__Username="guest" \
  -e RabbitMq__Password="guest" \
  fcg-notifications
```

| Variável | Obrigatória | Descrição |
| :--- | :--- | :--- |
| `Redis__Connection` | sim | Conexão do Redis (idempotência) |
| `RabbitMq__Host` | sim | Host do RabbitMQ |
| `RabbitMq__Username` / `RabbitMq__Password` | sim | Credenciais do RabbitMQ |
| `Serilog__LokiUrl` | não | URL do Loki — só então o sink Loki é ligado |
| `Otel__Endpoint` | não | Endpoint OTLP — só então traces/métricas são exportados |

## Observabilidade

Logs no **console** e enricher de `TraceId`/`SpanId` estão **sempre** ativos. Os sinks de rede
são **opcionais e desacoplados** — entram apenas se o endpoint correspondente estiver configurado:

- **Loki** (logs): ligado só com `Serilog__LokiUrl`.
- **OTLP** (traces/métricas → Tempo/Prometheus): ligado só com `Otel__Endpoint`.

Sem esses endpoints o serviço **sobe limpo**, console-only, sem erros de conexão. O *service name*
reportado é **`Fcg.Notifications.Api`**.

## Health checks

| Endpoint | Significado |
| :--- | :--- |
| `GET /health/live` | Liveness — processo vivo (não checa dependências). |
| `GET /health/ready` | Readiness — reflete o **Redis** (dependência dura). |

O broker (RabbitMQ) **não** entra no `/health/ready`: a idempotência desacopla o serviço do broker,
e derrubar a readiness por causa dele anularia esse benefício.

## Imagem e visibilidade no GHCR

A imagem é publicada em **`ghcr.io/reinaldogez/fcg-notifications`** (tags `latest` + `{sha}`).

## Deploy

Os manifestos **Kubernetes** deste serviço **não vivem aqui**: estão centralizados no repositório
de orquestração **`fcg-ops`**.
