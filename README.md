# StockApi — Gestão de Estoque e Produtos

&#x20;

API em .NET 9 (Minimal APIs) para cadastro de usuários, autenticação JWT, catálogo de produtos, controle de estoque e emissão de pedidos — pronta para rodar em Docker e com testes automatizados.

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
docker-compose.yml   # API + Postgres + healthchecks
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
- Healthcheck: `/swagger/v1/swagger.json`

Variáveis usadas no compose:

- `ConnectionStrings__Default=Host=db;Port=5432;Database=stockdb;Username=stock;Password=stock`
- `Jwt__Key=super-secret-key-change-me-please-32chars`
- `Jwt__Issuer=stockapi`
- `Jwt__Audience=stockapi-clients`
- `Jwt__TokenExpirationMinutes=60`

---

## Como rodar (sem Docker)

1. Suba um Postgres local na porta 5432 e crie o DB `stockdb`.
2. Configure `appsettings.Development.json` ou variáveis de ambiente equivalentes:

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

## Endpoints

### Auth

- `POST /auth/signup` → cria usuário
- `POST /auth/login` → retorna `{ token, email, role }`

### Produtos (auth)

- `GET /products`
- `GET /products/{id}`
- `POST /products` (Admin)
- `PUT /products/{id}` (Admin)
- `DELETE /products/{id}` (Admin)

### Estoque (Admin)

- `POST /stock/entries`

### Pedidos (Seller/Admin)

- `POST /orders`
- `GET /orders/{id}`

---

## Exemplos de Requests (cURL)

### Signup (Admin)

```bash
curl -X POST http://localhost:8080/auth/signup \
 -H "Content-Type: application/json" \
 -d '{"name":"Admin","email":"admin@local","password":"admin123","role":"Admin"}'
```

### Login

```bash
TOKEN=$(curl -s -X POST http://localhost:8080/auth/login \
 -H "Content-Type: application/json" \
 -d '{"email":"admin@local","password":"admin123"}' | jq -r .token)
echo $TOKEN
```

### Criar produto (Admin)

```bash
curl -X POST http://localhost:8080/products \
 -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
 -d '{"name":"Bola","description":"Futebol","price":99.9}'
```

### Adicionar estoque (Admin)

```bash
curl -X POST http://localhost:8080/stock/entries \
 -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
 -d '{"productId":"<PRODUCT_ID>","quantity":10,"invoiceNumber":"NF-001"}'
```

### Criar pedido (Seller ou Admin)

```bash
curl -X POST http://localhost:8080/orders \
 -H "Authorization: Bearer $TOKEN_SELLER" -H "Content-Type: application/json" \
 -d '{"customerDocument":"12345678900","sellerName":"Carlos","items":[{"productId":"<PRODUCT_ID>","quantity":2}]}'
```

---

## Guia de QA (H1 → H5)

### H1 — Cadastro de Usuários

- Senha curta → 400
- E-mail duplicado → 400

### H2 — Login

- Credenciais inválidas → 401

### H3 — Produtos (Admin)

- CRUD completo + restrição para Seller

### H4 — Estoque (Admin)

- `quantity > 0`, `invoiceNumber` obrigatório
- Erros: `Product not found.`, `Quantity must be > 0.`, etc.

### H5 — Pedidos (Seller/Admin)

- Redução de estoque
- Erros: estoque insuficiente, produtos inválidos

---

## Testes Automatizados

```bash
cd StockApi.Tests
./coverage.sh
```

Esperado:

- Line coverage ≈ 90%+
- Branch coverage cobrindo auth, produtos, estoque e pedidos

---

## Observabilidade / Tracing (OpenTelemetry)

- ASP.NET Core requests
- HttpClient
- Exportação via Console

Logs:

```bash
docker compose logs -f api
```

---

## CI (GitHub Actions)

Pipeline executa:

- restore → build → testes
- Upload de cobertura e TRX

---

## Variáveis de Ambiente

| Nome                          | Descrição                  | Exemplo                                                            |
| ----------------------------- | -------------------------- | ------------------------------------------------------------------ |
| ConnectionStrings\_\_Default  | String de conexão Postgres | `Host=db;Port=5432;Database=stockdb;Username=stock;Password=stock` |
| Jwt\_\_Key                    | Chave JWT (≥32 bytes)      | `super-secret-key-change-me-please-32chars`                        |
| Jwt\_\_Issuer                 | Issuer                     | `stockapi`                                                         |
| Jwt\_\_Audience               | Audience                   | `stockapi-clients`                                                 |
| Jwt\_\_TokenExpirationMinutes | Expiração                  | `60`                                                               |
| ASPNETCORE\_URLS (opcional)   | URL Kestrel                | `http://+:8080`                                                    |

---

## Troubleshooting

- Swagger não abre → confira logs do container.
- 401 → verifique token e permissões.
- PendingModelChangesWarning → gere nova migração.

---

## Licença & Versão

- Licença: MIT
- Versão: v1.0.0

---

## Créditos

Desafio baseado em **Arlequim Stack — Desafio Técnico Backend**.

