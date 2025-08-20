# StockApi — Gestão de Estoque e Produtos

![.NET CI](https://github.com/K-i-Q/stock-api/actions/workflows/dotnet.yml/badge.svg)

[![codecov](https://codecov.io/gh/K-i-Q/stock-api/branch/main/graph/badge.svg)](https://codecov.io/gh/K-i-Q/stock-api)

API em .NET 9 (Minimal APIs) para cadastro de usuários, autenticação JWT, catálogo de produtos, controle de estoque, emissão de pedidos e mensageria com RabbitMQ — pronta para rodar em Docker e com testes automatizados.

---

## Sumário

- [Stack](#stack)
- [Arquitetura & Decisões](#arquitetura--decisões)
- [Estrutura do Projeto](#estrutura-do-projeto)
- [Como rodar (Docker)](#como-rodar-docker)
- [Como rodar (sem Docker)](#como-rodar-sem-docker)
- [Banco de dados & Migrações](#banco-de-dados--migrações)
- [Autenticação, Perfis e Autorização](#autenticação-perfis-e-autorização)
- [Mensageria (RabbitMQ)](#mensageria-rabbitmq)
- [Endpoints](#endpoints)
- [Exemplos de Requests (cURL)](#exemplos-de-requests-curl)
- [Guia de QA (H1 → H5)](#guia-de-qa-h1--h5)
- [Testes Automatizados](#testes-automatizados)
- [Observabilidade / Tracing (OpenTelemetry)](#observabilidade--tracing-opentelemetry)
- [CI (GitHub Actions)](#ci-github-actions)
- [Variáveis de Ambiente](#variáveis-de-ambiente)
- [Troubleshooting](#troubleshooting)
- [Licença & Versão](#licença--versão)
- [Créditos](#créditos)
- [Release Notes](#release-notes)

---

## Stack

- .NET 9 (Minimal APIs)
- Entity Framework Core 9 (Npgsql provider)
- PostgreSQL 16 (Docker)
- RabbitMQ (Mensageria)
- JWT (Microsoft.AspNetCore.Authentication.JwtBearer)
- Swagger / OpenAPI (Swashbuckle)
- xUnit (integração + cobertura)
- OpenTelemetry (instrumentação HTTP/ASP.NET + exportação console)

---

## Arquitetura & Decisões

- **Minimal API**: favorece clareza e velocidade.
- **EF Core**: `Database.MigrateAsync()` no startup; InMemory em `Testing`.
- **JWT**: políticas de autorização por perfil.
  - `AdminOnly` → escrever em Produtos e Estoque
  - `SellerOrAdmin` → criar/ler Pedidos
- **Mensageria**:
  - RabbitMQ para publicação/consumo de eventos de pedidos.
  - `OrderCreatedPublisher` → publica no exchange `stock.events` com routing key `orders.created`.
  - `OrderCreatedConsumer` → consome e processa mensagens da fila.
- **Erros**:
  - `ValidationProblemDetails` para validações
  - mensagens claras para regras de negócio.
- **Swagger**: sempre habilitado em `/swagger`.
- **Seed**: cria usuário `admin@local / admin123` se o banco estiver vazio.
- **Testing estável**: mensageria desabilitada por padrão no ambiente de testes (`RabbitMq__Disabled=true`).

---

## Estrutura do Projeto

```
StockApi/            # API (Minimal APIs)
StockApi.Tests/      # Testes de integração e unitários (xUnit)
.github/workflows/   # CI (GitHub Actions)
api-samples.http     # Coleção de requests (VS Code/.http)
Dockerfile           # Build da API
docker-compose.yml   # API + Postgres + RabbitMQ + healthchecks
README.md            # Este arquivo
```

---

## Como rodar (Docker)

**Pré-requisitos:** Docker Desktop.

```bash
git clone https://github.com/K-i-Q/stock-api.git
cd stock-api
docker compose up --build
```

- API: http://localhost:8080/swagger
- RabbitMQ Management: http://localhost:15672 (guest/guest)
- Healthcheck: `/swagger/v1/swagger.json`

Variáveis usadas no compose:

- `ConnectionStrings__Default=Host=db;Port=5432;Database=stockdb;Username=stock;Password=stock`
- `Jwt__Key=super-secret-key-change-me-please-32chars`
- `Jwt__Issuer=stockapi`
- `Jwt__Audience=stockapi-clients`
- `Jwt__TokenExpirationMinutes=60`
- `RabbitMq__Host=rabbitmq`
- `RabbitMq__User=guest`
- `RabbitMq__Pass=guest`

---

## Como rodar (sem Docker)

1. Suba um Postgres local na porta 5432 e crie o DB `stockdb`.
2. Suba um RabbitMQ local (porta 5672, painel 15672).
3. Configure `appsettings.Development.json` ou variáveis de ambiente equivalentes:

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=stockdb;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "Key": "uma-chave-de-32+ caracteres",
    "Issuer": "stockapi",
    "Audience": "stockapi-clients",
    "TokenExpirationMinutes": 60
  },
  "RabbitMq": {
    "Host": "localhost",
    "User": "stock",
    "Pass": "stock"
  }
}
```

4. Rode a aplicação:

```bash
dotnet restore
dotnet run --project StockApi/StockApi.csproj
```

Swagger: http://localhost:5189/swagger

---

## Banco de dados & Migrações

- Produção: `db.Database.MigrateAsync()` no startup.
- Testing: InMemory.
- Criar migrações localmente:

```bash
dotnet ef migrations add MinhaMigracao -s StockApi -p StockApi
dotnet ef database update -s StockApi -p StockApi
```

---

## Autenticação, Perfis e Autorização

- Login via e-mail → retorna JWT.
- Perfis (`UserRole`):
  - `Admin = 1`
  - `Seller = 2`
- Políticas:
  - `AdminOnly` → POST/PUT/DELETE /products, POST /stock/entries
  - `SellerOrAdmin` → POST /orders, GET /orders/{id}

No `/auth/signup`, envie role como string ("Admin" ou "Seller").

---

## Mensageria (RabbitMQ)

### Publisher

- Publica mensagens no exchange `stock.events` com routing key `orders.created`.
- Executado ao criar pedido (`POST /orders`).

### Consumer

- `OrderCreatedConsumer` consome fila ligada a `orders.created`.
- Processa eventos de novos pedidos.

### Como testar

1. Rodar API + RabbitMQ via Docker.
2. Criar pedido (`POST /orders`).
3. Verificar fila no painel http://localhost:15672.
4. Logs da API exibem consumo:

```bash
docker compose logs -f api
```

---

## Endpoints

(... permanece igual ...)

---

## Testes Automatizados

- Testes de **integração** (xUnit) exercitam endpoints reais com autenticação JWT.
- Testes **unitários** cobrem regras de domínio e geração de JWT.
- Mensageria é desabilitada em `Testing` via `RabbitMq__Disabled=true`.
- Cobertura de código com script auxiliar:

```bash
cd StockApi.Tests
./coverage.sh
```

O relatório HTML é gerado em `StockApi.Tests/TestResults/**/report/index.html`.

---

## Observabilidade / Tracing (OpenTelemetry)

- Exportador console habilitado em `Development`.
- Traços para requisições HTTP e ASP.NET.

---

## CI (GitHub Actions)

- Workflow `.github/workflows/dotnet.yml` executa build, testes e publica cobertura no Codecov.

---

## Variáveis de Ambiente

| Nome                          | Descrição                  | Exemplo                                                            |
| ----------------------------- | -------------------------- | ------------------------------------------------------------------ |
| ConnectionStrings\_\_Default  | String de conexão Postgres | `Host=db;Port=5432;Database=stockdb;Username=stock;Password=stock` |
| Jwt\_\_Key                    | Chave JWT (≥32 bytes)      | `super-secret-key-change-me-please-32chars`                        |
| Jwt\_\_Issuer                 | Issuer                     | `stockapi`                                                         |
| Jwt\_\_Audience               | Audience                   | `stockapi-clients`                                                 |
| Jwt\_\_TokenExpirationMinutes | Expiração                  | `60`                                                               |
| RabbitMq\_\_Host              | Host RabbitMQ              | `rabbitmq`                                                         |
| RabbitMq\_\_User              | Usuário RabbitMQ           | `guest`                                                            |
| RabbitMq\_\_Pass              | Senha RabbitMQ             | `guest`                                                            |
| RabbitMq\_\_Disabled          | Desliga mensageria (tests) | `true`                                                             |
| ASPNETCORE_URLS (opcional)    | URL Kestrel                | `http://+:8080`                                                    |

---

## Troubleshooting

- Swagger não abre → confira logs do container.
- 401 → verifique token e permissões.
- PendingModelChangesWarning → gere nova migração.
- Mensagens não aparecem → verifique se `RabbitMq__*` estão corretas e se o consumer está ativo.

---

## Licença & Versão

- Licença: MIT
- Versão: v1.3.0

---

## Créditos

Desafio baseado em **Arlequim Stack — Desafio Técnico Backend**.

---

## Release Notes

### v1.3.0

- Testes: suíte estabilizada com cenários de _binding_ inválido e rota/body mismatch.
- Infra de testes: desabilita RabbitMQ via `RabbitMq__Disabled=true`.
- Correções nos DTOs utilizados nos testes de integração.
- README reorganizado e ampliado (seções de testes e variáveis).

### v1.2.2

- Correções de documentação e versionamento.
- Ajuste do número da versão no README.
- Alinhamento das notas de release com as versões publicadas no GitHub.

### v1.2.1

- Correção: uso de `NullMessageBus` nos testes para evitar falhas de conexão com RabbitMQ.
- Ajustes no `Program.cs` para registrar RabbitMQ apenas fora de `Testing`.
- Documentação atualizada para refletir `stock.events` + routing key `orders.created`.

### v1.2.0

- Integração com RabbitMQ (publisher + consumer de pedidos).
- Nova seção de **Mensageria** no README.
- Atualização do `docker-compose.yml` para incluir serviço RabbitMQ.
- Variáveis de ambiente `RabbitMq__*` documentadas.

### v1.1.0

- Inclusão de testes automatizados (xUnit).
- Cobertura integrada com Codecov.

### v1.0.0

- Primeira versão com CRUD de produtos, estoque e pedidos.
