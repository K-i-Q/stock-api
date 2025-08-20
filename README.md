# StockApi — Gestão de Estoque e Produtos

![.NET CI](https://github.com/K-i-Q/stock-api/actions/workflows/dotnet.yml/badge.svg)

[![codecov](https://codecov.io/gh/K-i-Q/stock-api/branch/main/graph/badge.svg)](https://codecov.io/gh/K-i-Q/stock-api)

API em .NET 9 (Minimal APIs) para cadastro de usuários, autenticação JWT, catálogo de produtos, controle de estoque, emissão de pedidos e mensageria com RabbitMQ — pronta para rodar em Docker e com testes automatizados.

**Domínio escolhido:** equipamentos esportivos (genérico o suficiente para outros domínios do enunciado).

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
- **EF Core**: `Database.MigrateAsync()` no startup; InMemory em Testing.
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
    "User": "guest",
    "Pass": "guest"
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

- Publica mensagens no exchange `stock.events` com routing key `orders.created`.
- Executado ao criar pedido (`POST /orders`).

### Consumer

- `OrderCreatedConsumer` consome fila ligada a `orders.created`.
- Processa eventos de novos pedidos.

### Como testar

1. Rodar API + RabbitMQ via Docker.
2. Criar pedido (`POST /orders`).
3. Verificar fila no painel [http://localhost:15672](http://localhost:15672).
4. Logs da API exibem consumo:

```bash
docker compose logs -f api
```

---

## Endpoints

- `POST /auth/signup` — criar usuário
- `POST /auth/login` — autenticação
- `GET /products` — listar produtos
- `POST /products` — criar produto (Admin)
- `PUT /products/{id}` — atualizar produto (Admin)
- `DELETE /products/{id}` — remover produto (Admin)
- `POST /stock/entries` — adicionar estoque (Admin)
- `POST /orders` — criar pedido (Seller ou Admin)
- `GET /orders/{id}` — consultar pedido (Seller ou Admin)

---

## Exemplos de Requests (cURL)

```bash
# Criar usuário
curl -X POST http://localhost:8080/auth/signup \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@local","password":"admin123","role":"Admin"}'

# Login
curl -X POST http://localhost:8080/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@local","password":"admin123"}'

# Criar produto
curl -X POST http://localhost:8080/products \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name":"Bola de Futebol","description":"Oficial","price":199.99}'

# Adicionar estoque
curl -X POST http://localhost:8080/stock/entries \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"productId":"<guid>","quantity":10}'

# Criar pedido
curl -X POST http://localhost:8080/orders \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"productId":"<guid>","quantity":2}'
```

---

## Guia de QA (H1 → H5)

- **H1**: Criar produto com preço negativo → retorna 400 + `ValidationProblemDetails`.
- **H2**: Criar pedido sem estoque → retorna erro de regra de negócio.
- **H3**: Usuário comum criando produto → 403 Forbidden.
- **H4**: JWT inválido → 401 Unauthorized.
- **H5**: Pedido criado deve publicar mensagem em `stock.events`.

---

## Testes Automatizados

- xUnit para testes de integração (API + DB InMemory).
- `coverage.sh` gera relatório com `dotnet test` + `coverlet` + `reportgenerator`.
- Expectativa: cobertura **≥ 90% linhas** e validação de cenários críticos (branch coverage).

```bash
./coverage.sh
open ./coverage/index.html
```

---

## Observabilidade / Tracing (OpenTelemetry)

- ASP.NET Core requests instrumentados.
- `HttpClient` instrumentado.
- Exportador Console habilitado.
- Exemplo: acompanhar logs com:

```bash
docker compose logs -f api
```

---

## CI (GitHub Actions)

- Workflow em `.github/workflows/dotnet.yml`:
  - Restauração
  - Build
  - Execução de testes
  - Geração de cobertura (TRX + cobertura)
  - Upload para Codecov

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
- Versão: v1.2.2

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

### v1.2.1

- Correção: uso de `NullMessageBus` nos testes para evitar falhas de conexão com RabbitMQ.
- Ajustes no `Program.cs` para registrar RabbitMQ apenas fora de `Testing`.
- Documentação atualizada para refletir `stock.events` + routing key `orders.created`.

### v1.2.2

- Release de correção de documentação e versionamento.
- Ajuste do número da versão no README.
- Alinhamento das notas de release com as versões publicadas no GitHub.

### v1.3.0

- Validações de rota: agora o `PUT /products/{id}` retorna **400 BadRequest** quando o `id` da rota difere do corpo.
- Validações de binding: requisições com **GUID inválido** em `/products/{id}` e `/orders/{id}` retornam **400 BadRequest** (antes eram 404).
- Melhoria nos testes de integração cobrindo cenários de inconsistência de rota e binding inválido.
- Atualização do README para refletir novos comportamentos de validação.

### v1.3.3

- Atualização do README para refletir melhorias de documentação.
- Ajustes de versionamento e alinhamento das seções já implementadas.
