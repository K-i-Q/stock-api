StockApi — Gestão de Estoque e Produtos

API em .NET para cadastro de usuários, autenticação JWT, catálogo de produtos, controle de estoque e emissão de pedidos.

Sumário
• Stack
• Arquitetura & Decisões
• Como rodar (Docker)
• Como rodar (sem Docker)
• Banco de dados & Migrações
• Fluxo completo (H1 → H5)
• Endpoints
• Testes
• Variáveis de ambiente
• Troubleshooting
• Bônus / Observabilidade (opcional)

⸻

Stack
• .NET 9 (Minimal APIs)
• Entity Framework Core 9
• PostgreSQL 16 (Docker)
• JWT (Microsoft.AspNetCore.Authentication.JwtBearer)
• Swagger / OpenAPI
• xUnit (integração + cobertura)

⸻

Arquitetura & Decisões
• Minimal API para velocidade e clareza no desafio.
• EF Core com Npgsql; em produção aplica MigrateAsync() ao subir; em testes usa InMemory.
• JWT para autenticação; políticas:
• AdminOnly → escreve em produtos e estoque
• SellerOrAdmin → cria pedidos e consulta pedidos
• Tratamento de erros com ProblemDetails nas validações de modelo; retornos 400/401/403/404 com mensagens claras.
• Swagger para auto-descoberta dos endpoints.
• Seed: cria um usuário admin@local / admin123 se o banco estiver vazio.

⸻

Como rodar (Docker)

Pré-requisitos: Docker Desktop.

# na pasta StockApi/ (onde está Dockerfile e docker-compose.yml)

docker compose up --build

    •	API: http://localhost:8080/swagger

Observação: a UI do Swagger fica em /swagger. A raiz / redireciona para /swagger.

Variáveis usadas no compose:
• ConnectionStrings**Default=Host=db;Port=5432;Database=stockdb;Username=stock;Password=stock
• Jwt**Key (default no compose: super-secret-key-change-me-please-32chars)
• Jwt**Issuer=stockapi, Jwt**Audience=stockapi-clients, Jwt\_\_TokenExpirationMinutes=60

⸻

Como rodar (sem Docker) 1. Suba um Postgres local (porta 5432) e crie o DB stockdb. 2. Configure appsettings.Development.json ou variáveis de ambiente:

ConnectionStrings**Default="Host=localhost;Port=5432;Database=stockdb;Username=postgres;Password=postgres"
Jwt**Key="uma-chave-de-32+ caracteres"
Jwt**Issuer="stockapi"
Jwt**Audience="stockapi-clients"
Jwt\_\_TokenExpirationMinutes="60"

    3.	Rode:

dotnet restore
dotnet run

    •	Swagger: http://localhost:5189/swagger (ou porta informada no console)

⸻

Banco de dados & Migrações
• Produção (Docker): Program.cs chama db.Database.MigrateAsync() ao subir.
• Testes: usa InMemory.
• Para criar/atualizar migrações localmente:

dotnet ef migrations add MinhaMigracao -s StockApi -p StockApi
dotnet ef database update -s StockApi -p StockApi

⸻

Fluxo completo (H1 → H5)

Use os exemplos abaixo (cURL).
Dica: exporte o token após o login para facilitar.

1. H1 — Cadastro de usuário (Admin ou Seller)

curl -X POST http://localhost:8080/auth/signup \
 -H "Content-Type: application/json" \
 -d '{ "name":"Admin", "email":"admin@local", "password":"admin123", "role": "Admin" }'

2. H2 — Login (recebe token)

TOKEN=$(curl -s -X POST http://localhost:8080/auth/login \
 -H "Content-Type: application/json" \
 -d '{ "email":"admin@local", "password":"admin123" }' | jq -r .token)
echo $TOKEN

3. H3 — Produtos (Admin)

Criar

curl -X POST http://localhost:8080/products \
 -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
 -d '{ "name":"Bola", "description":"Futebol", "price": 99.90 }'

Listar

curl -H "Authorization: Bearer $TOKEN" http://localhost:8080/products

4. H4 — Adicionar estoque (Admin)

Substitua PRODUCT_ID pelo Id retornado ao criar o produto.

curl -X POST http://localhost:8080/stock/entries \
 -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
 -d '{ "productId":"PRODUCT_ID", "quantity": 10, "invoiceNumber":"NF-001" }'

5. H5 — Emissão de pedido (Seller ou Admin)

Crie um vendedor e faça login para obter TOKEN_SELLER, ou use o admin.

# (opcional) criar seller

curl -X POST http://localhost:8080/auth/signup \
 -H "Content-Type: application/json" \
 -d '{ "name":"Vendedor", "email":"seller@local", "password":"seller123", "role": "Seller" }'

TOKEN_SELLER=$(curl -s -X POST http://localhost:8080/auth/login \
 -H "Content-Type: application/json" \
 -d '{ "email":"seller@local", "password":"seller123" }' | jq -r .token)

# criar pedido (baixa automática de estoque)

curl -X POST http://localhost:8080/orders \
 -H "Authorization: Bearer $TOKEN_SELLER" -H "Content-Type: application/json" \
 -d '{
"customerDocument": "12345678900",
"sellerName": "Carlos",
"items": [
{ "productId": "PRODUCT_ID", "quantity": 2 }
]
}'

Se algum produto não tiver estoque suficiente, retorna 400 com mensagem de erro.

⸻

Endpoints

Auth
• POST /auth/signup → cria usuário (Admin/Seller). Regras: e-mail único, senha ≥ 6.
• POST /auth/login → retorna { token, email, role }.

Produtos
• GET /products → lista (auth requerida).
• GET /products/{id} → consulta (auth).
• POST /products → cria (Admin).
• PUT /products/{id} → edita (Admin).
• DELETE /products/{id} → exclui (Admin).

Estoque
• POST /stock/entries → adiciona entrada com quantity e invoiceNumber (Admin).

Pedidos
• POST /orders → cria pedido (Seller/Admin).
• Valida itens, existência dos produtos e estoque.
• Dá baixa no estoque.
• GET /orders/{id} → consulta pedido.

⸻

Testes

Rodar todos os testes e gerar cobertura (usando script do projeto):

cd StockApi.Tests
./coverage.sh

Saída esperada:
• Line coverage alta (≈ 90%+)
• Branch coverage cobrindo regras principais (validações de auth, produtos, estoque e pedidos)

⸻

Variáveis de ambiente

Nome Descrição Exemplo
ConnectionStrings**Default string de conexão Postgres Host=db;Port=5432;Database=stockdb;Username=stock;Password=stock
Jwt**Key chave secreta JWT (≥ 32 bytes; código aplica padding se menor) super-secret-key-change-me-please-32chars
Jwt**Issuer issuer do token stockapi
Jwt**Audience audience do token stockapi-clients
Jwt\_\_TokenExpirationMinutes minutos de expiração 60
ASPNETCORE_URLS (opcional) URL Kestrel http://+:8080

Em Testing, o app usa InMemory; em Produção, usa Postgres + migrações automáticas.

⸻

Troubleshooting
• Swagger não abre
Verifique: http://localhost:8080/swagger.
Confirme que o container api está listening on http://[::]:8080 no log.
• Erro de HTTPS redirect em Docker
Em produção a app não força HTTPS. Se estiver forçando, garanta que UseHttpsRedirection() só roda em Development.
• PendingModelChangesWarning
Em Docker, ao subir a primeira vez, a app aplica MigrateAsync(). Se alterar modelo, gere nova migração local e re-build:

dotnet ef migrations add Nova
docker compose up --build

    •	JWT inválido / 401

Confirme que está enviando Authorization: Bearer <TOKEN> e que o token corresponde ao usuário com permissão.

⸻

Bônus / Observabilidade (opcional)

Se desejar habilitar tracing/APM:
• Adicionar OpenTelemetry (traces + logs) e um backend (Jaeger/Zipkin).
• Incluir um serviço Jaeger no docker-compose e instrumentar middlewares (requests, EFCore).
