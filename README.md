# StockApi — Gestão de Estoque e Produtos

![.NET CI](https://github.com/K-i-Q/stock-api/actions/workflows/dotnet.yml/badge.svg)

[![codecov](https://codecov.io/gh/K-i-Q/stock-api/branch/main/graph/badge.svg)](https://codecov.io/gh/K-i-Q/stock-api)

&#x20;

API em .NET 9 (Minimal APIs) para cadastro de usuários, autenticação JWT, catálogo de produtos, controle de estoque, emissão de pedidos e mensageria com RabbitMQ — pronta para rodar em Docker e com testes automatizados.

Domínio escolhido: equipamentos esportivos (genérico o suficiente para outros domínios do enunciado).

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
- **EF Core**: `Database.MigrateAsync()` no startup; InMemory em Testing.
- **JWT**: políticas de autorização por perfil.
  - `AdminOnly` → escrever em Produtos e Estoque
  - `SellerOrAdmin` → criar/ler Pedidos
- **Mensageria**:
  - RabbitMQ para publicação/consumo de eventos de pedidos.
  - `OrderCreatedPublisher` → publica no exchange `orders`.
  - `OrderCreatedConsumer` → consome e processa mensagens.
- **Erros**:
  - `ValidationProblemDetails` para validações
  - mensagens claras para regras de negócio.
- **Swagger**: sempre habilitado em `/swagger`.
- **Seed**: cria usuário `admin@local / admin123` se o banco estiver vazio.

---

## Estrutura do Projeto

```
StockApi/            # API (Minimal APIs)
StockApi.Tests/      # Testes de integração (xUnit)
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
cd stock-api/StockApi
docker compose up --build
```

- API: [http://localhost:8080/swagger](http://localhost:8080/swagger)
- RabbitMQ Management: [http://localhost:15672](http://localhost:15672) (guest/guest)
- Healthcheck: `/swagger/v1/swagger.json`

Variáveis usadas no compose:

- `ConnectionStrings__Default=Host=db;Port=5432;Database=stockdb;Username=stock;Password=stock`
- `Jwt__Key=super-secret-key-change-me-please-32chars`
- `Jwt__Issuer=stockapi`
- `Jwt__Audience=stockapi-clients`
- `Jwt__TokenExpirationMinutes=60`
- `RabbitMq__HostName=rabbit`
- `RabbitMq__UserName=guest`
- `RabbitMq__Password=guest`

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
    "HostName": "localhost",
    "UserName": "guest",
    "Password": "guest"
  }
}
```

3. Rode a aplicação:

```bash
dotnet restore
dotnet run --project StockApi/StockApi.csproj
```

Swagger: [http://localhost:5189/swagger](http://localhost:5189/swagger)

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

- `OrderCreatedPublisher` publica mensagens no exchange `orders`.
- Mensagens enviadas ao criar pedido (`POST /orders`).

### Consumer

- `OrderCreatedConsumer` consome fila `orders.created`.
- Processa eventos de novos pedidos.

### Como testar

1. Rodar API + RabbitMQ via Docker.
2. Criar pedido (`POST /orders`).
3. Verificar fila `orders.created` no painel [http://localhost:15672](http://localhost:15672).
4. Logs da API exibem consumo:

```bash
docker compose logs -f api
```

---

## Endpoints

(... permanece igual ...)

---

## Variáveis de Ambiente

| Nome                          | Descrição                  | Exemplo                                                            |
| ----------------------------- | -------------------------- | ------------------------------------------------------------------ |
| ConnectionStrings\_\_Default  | String de conexão Postgres | `Host=db;Port=5432;Database=stockdb;Username=stock;Password=stock` |
| Jwt\_\_Key                    | Chave JWT (≥32 bytes)      | `super-secret-key-change-me-please-32chars`                        |
| Jwt\_\_Issuer                 | Issuer                     | `stockapi`                                                         |
| Jwt\_\_Audience               | Audience                   | `stockapi-clients`                                                 |
| Jwt\_\_TokenExpirationMinutes | Expiração                  | `60`                                                               |
| RabbitMq\_\_HostName          | Host RabbitMQ              | `rabbit`                                                           |
| RabbitMq\_\_UserName          | Usuário RabbitMQ           | `guest`                                                            |
| RabbitMq\_\_Password          | Senha RabbitMQ             | `guest`                                                            |
| ASPNETCORE_URLS (opcional)    | URL Kestrel                | `http://+:8080`                                                    |

---

## Troubleshooting

- Swagger não abre → confira logs do container.
- 401 → verifique token e permissões.
- PendingModelChangesWarning → gere nova migração.
- Mensagens não aparecem → verifique se `RabbitMq__*` estão corretos e consumer está ativo.

---

## Licença & Versão

- Licença: MIT
- Versão: v1.2.0

---

## Créditos

Desafio baseado em **Arlequim Stack — Desafio Técnico Backend**.

---

## Release Notes

### v1.0.0

- Primeira versão com CRUD de produtos, estoque e pedidos.

### v1.1.0

- Inclusão de testes automatizados (xUnit).
- Cobertura integrada com Codecov.

### v1.2.0

- Integração com RabbitMQ (publisher + consumer de pedidos).
- Nova seção de **Mensageria** no README.
- Atualização do `docker-compose.yml` para incluir serviço RabbitMQ.
- Variáveis de ambiente `RabbitMq__*` documentadas.
