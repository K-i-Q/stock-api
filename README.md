StockApi — Gestão de Estoque e Produtos

# StockApi

![Build](https://github.com/K-i-Q/StockApi/actions/workflows/dotnet.yml/badge.svg)

API em .NET 9 (Minimal APIs) para cadastro de usuários, autenticação JWT, catálogo de produtos, controle de estoque e emissão de pedidos — pronta para rodar em Docker e com testes automatizados.

Domínio escolhido: equipamentos esportivos (genérico o suficiente para outros domínios do enunciado).

⸻

Sumário
• Stack
• Arquitetura & Decisões
• Estrutura do Projeto
• Como rodar (Docker)
• Como rodar (sem Docker)
• Banco de dados & Migrações
• Autenticação, Perfis e Autorização
• Endpoints
• Exemplos de Requests (cURL)
• Guia de QA (H1 → H5)
• Testes Automatizados
• Observabilidade / Tracing (OpenTelemetry)
• CI (GitHub Actions)
• Variáveis de Ambiente
• Troubleshooting
• Licença & Versão

⸻

Stack
• .NET 9 (Minimal APIs)
• Entity Framework Core 9 (Npgsql provider)
• PostgreSQL 16 (Docker)
• JWT (Microsoft.AspNetCore.Authentication.JwtBearer)
• Swagger / OpenAPI (Swashbuckle)
• xUnit (integração + cobertura)
• OpenTelemetry (instrumentação HTTP/ASP.NET + exportação console)

⸻

Arquitetura & Decisões
• Minimal API: favorece clareza e velocidade para o desafio, mantendo validações e filtros.
• EF Core: em Produção aplica Database.MigrateAsync() no startup; em Testing usa InMemory.
• JWT: políticas de autorização por perfil.
• AdminOnly → escrever em Produtos e Estoque
• SellerOrAdmin → criar/ler Pedidos
• Tratamento de erros:
• ValidationProblemDetails para validações de modelo (via ValidationFilter<T>)
• Mensagens claras para regras de negócio (ex.: “Insufficient stock…”, “Some product(s) not found.”).
• Swagger: sempre habilitado e publicado em /swagger (raiz / redireciona com 302 para /swagger).
• Seed: cria usuário admin@local / admin123 se o banco estiver vazio.

⸻

Estrutura do Projeto

StockApi/ # API (Minimal APIs)
StockApi.Tests/ # Testes de integração (xUnit)
.github/workflows/ # CI (GitHub Actions)
api-samples.http # Coleção de requests (VS Code/.http)
Dockerfile # Build da API
docker-compose.yml # API + Postgres + healthchecks
README.md # Este arquivo

⸻

Como rodar (Docker)

Pré-requisitos: Docker Desktop.

# clone

git clone https://github.com/K-i-Q/stock-api.git
cd stock-api/StockApi

# sobe DB + API (com healthcheck)

docker compose up --build

    •	API: http://localhost:8080/swagger
    •	Healthcheck API: tenta baixar /swagger/v1/swagger.json

Variáveis usadas no compose (defaults adequados para dev):
• ConnectionStrings**Default=Host=db;Port=5432;Database=stockdb;Username=stock;Password=stock
• Jwt**Key=super-secret-key-change-me-please-32chars (a app faz padding se vier menor)
• Jwt**Issuer=stockapi, Jwt**Audience=stockapi-clients, Jwt\_\_TokenExpirationMinutes=60

A aplicação executa migrações automaticamente no startup em produção (Docker).

⸻

Como rodar (sem Docker) 1. Suba um Postgres local na porta 5432 e crie o DB stockdb. 2. Configure appsettings.Development.json ou use variáveis de ambiente equivalentes:

// appsettings.Development.json (exemplo)
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

    3.	Rode a aplicação:

dotnet restore
dotnet run --project StockApi/StockApi.csproj

# Swagger: http://localhost:5189/swagger (ou porta exibida no console)

⸻

Banco de dados & Migrações
• Produção (Docker): Program.cs chama db.Database.MigrateAsync() no startup.
• Testing: InMemory.
• Criar migrações localmente:

# criar

dotnet ef migrations add MinhaMigracao -s StockApi -p StockApi

# aplicar

dotnet ef database update -s StockApi -p StockApi

⸻

Autenticação, Perfis e Autorização
• Login: via e-mail, retorna JWT (token, email, role).
• Perfis (UserRole):
• Admin = 1
• Seller = 2
• Políticas:
• AdminOnly → POST/PUT/DELETE /products, POST /stock/entries
• SellerOrAdmin → POST /orders, GET /orders/{id}

No /auth/signup, envie role como string ("Admin" ou "Seller") ou numérica compatível com o enum.

⸻

Endpoints

Auth
• POST /auth/signup → cria usuário (Admin/Seller). Regras: e-mail único, senha ≥ 6.
• POST /auth/login → retorna { token, email, role }.

Produtos (auth)
• GET /products → lista produtos
• GET /products/{id} → consulta
• POST /products (Admin) → cria
• PUT /products/{id} (Admin) → edita
• DELETE /products/{id} (Admin) → exclui

Estoque (Admin)
• POST /stock/entries → adiciona entrada com productId, quantity (> 0) e invoiceNumber (obrigatória)

Pedidos (Seller/Admin)
• POST /orders → cria pedido com customerDocument, sellerName e items[] (productId, quantity)
• Valida itens, existência dos produtos e estoque disponível
• Baixa estoque automaticamente se tudo OK
• GET /orders/{id} → consulta pedido

⸻

Exemplos de Requests (cURL)

Há também um arquivo api-samples.http na raiz para VS Code/JetBrains.

1. Signup (Admin)

curl -X POST http://localhost:8080/auth/signup \
 -H "Content-Type: application/json" \
 -d '{
"name":"Admin",
"email":"admin@local",
"password":"admin123",
"role":"Admin"
}'

2. Login

TOKEN=$(curl -s -X POST http://localhost:8080/auth/login \
 -H "Content-Type: application/json" \
 -d '{"email":"admin@local","password":"admin123"}' | jq -r .token)
echo $TOKEN

3. Criar produto (Admin)

curl -X POST http://localhost:8080/products \
 -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
 -d '{"name":"Bola","description":"Futebol","price":99.9}'

4. Adicionar estoque (Admin)

curl -X POST http://localhost:8080/stock/entries \
 -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
 -d '{"productId":"<PRODUCT_ID>","quantity":10,"invoiceNumber":"NF-001"}'

5. Criar pedido (Seller ou Admin)

# (opcional) criar seller

yes | true >/dev/null 2>&1 # no-op para shell com múltiplos blocos
curl -X POST http://localhost:8080/auth/signup \
 -H "Content-Type: application/json" \
 -d '{"name":"Vendedor","email":"seller@local","password":"seller123","role":"Seller"}'

TOKEN_SELLER=$(curl -s -X POST http://localhost:8080/auth/login \
 -H "Content-Type: application/json" \
 -d '{"email":"seller@local","password":"seller123"}' | jq -r .token)

echo $TOKEN_SELLER

# criar pedido (baixa de estoque)

curl -X POST http://localhost:8080/orders \
 -H "Authorization: Bearer $TOKEN_SELLER" -H "Content-Type: application/json" \
 -d '{
"customerDocument":"12345678900",
"sellerName":"Carlos",
"items":[{"productId":"<PRODUCT_ID>","quantity":2}]
}'

⸻

Guia de QA (H1 → H5)

Objetivo: validar ponta a ponta as histórias do enunciado, incluindo casos de erro.

H1 — Cadastro de Usuários
• Cenário feliz: POST /auth/signup com role = Admin e senha ≥ 6 → 201 Created.
• Senha curta: senha < 6 → 400 BadRequest com mensagem clara.
• E-mail duplicado: repetir o cadastro → 400 BadRequest (“E-mail already registered.”).

H2 — Login
• Cenário feliz: POST /auth/login com credenciais válidas → 200 OK + { token, email, role }.
• Credenciais inválidas: e-mail inexistente ou senha errada → 401 Unauthorized.

H3 — Gerenciamento de Produtos (Admin) 1. Com token de Admin: POST /products → 201 Created (payload com id). 2. GET /products → 200 OK (lista contém item criado). 3. PUT /products/{id} → 204 NoContent (editar). 4. GET /products/{id} → 200 OK (dados atualizados). 5. DELETE /products/{id} → 204 NoContent. 6. Autorização: com token de Seller, POST /products → 403 Forbidden.

H4 — Controle de Estoque (Admin)
• POST /stock/entries com quantity > 0 e invoiceNumber não vazio → 201 Created.
• Erros:
• productId inexistente → 400 BadRequest (“Product not found.”)
• quantity <= 0 → 400 BadRequest (“Quantity must be > 0.”)
• invoiceNumber vazio/branco → 400 BadRequest (“Invoice number required.”)

H5 — Emissão de Pedidos (Seller/Admin) 1. Preparar: criar produto (Admin) e adicionar estoque (ex.: 10 unidades). 2. Criar pedido (Seller) com quantity = 3 → 201 Created. 3. Consultar produto após o pedido → estoque reduzido (de 10 para 7). 4. Sem estoque suficiente: tentar criar com quantity > estoque → 400 BadRequest (mensagem “Insufficient stock…” + estoque disponível). 5. Produtos inválidos: item com productId inexistente → 400 BadRequest (“Some product(s) not found.”)

Há testes automatizados cobrindo os cenários de sucesso e erro (incluindo baixa de estoque e estoque insuficiente).

⸻

Testes Automatizados

Rodar tudo e gerar cobertura:

cd StockApi.Tests
./coverage.sh

Saída esperada:
• Line coverage alta (≈ 90%+)
• Branch coverage cobrindo principais regras (auth, produtos, estoque e pedidos)

Artefatos de cobertura em StockApi.Tests/TestResults/\*\*/coverage.cobertura.xml e relatório HTML (caso configurado no script).

⸻

Observabilidade / Tracing (OpenTelemetry)

A API já está instrumentada com OpenTelemetry para:
• ASP.NET Core (requisições HTTP)
• HttpClient
• Exportação via Console (spans exibidos no STDOUT)

Como verificar os traços (Docker):

# em um terminal

cd StockApi
docker compose up --build

# em outro terminal, gere algum tráfego (ex.: abrir o Swagger ou fazer um GET)

curl -s http://localhost:8080/swagger/v1/swagger.json >/dev/null

# volte aos logs do container e observe spans/atividades

docker compose logs -f api | grep -i "Activity" || true

# ou simplesmente: docker compose logs -f api

Você deverá ver eventos do tipo Activity/span para os handlers HTTP (com TraceId/SpanId, duração, status etc.).

Opcional (bônus): integrar com Jaeger/Zipkin. Bastaria incluir o exporter correspondente (pacote) e o serviço no docker-compose.yml.

⸻

CI (GitHub Actions)

O pipeline de CI (em .github/workflows/) executa:
• restore → build (Release) → testes com cobertura
• Faz upload de artefatos de cobertura e TRX

O workflow referencia a solution StockSolution.sln. Garanta que ela exista na raiz ou ajuste o caminho no YAML conforme a sua solução.

⸻

Variáveis de Ambiente

Nome Descrição Exemplo
ConnectionStrings**Default String de conexão Postgres Host=db;Port=5432;Database=stockdb;Username=stock;Password=stock
Jwt**Key Chave secreta JWT (≥ 32 bytes; a app aplica padding se menor) super-secret-key-change-me-please-32chars
Jwt**Issuer Issuer do token stockapi
Jwt**Audience Audience do token stockapi-clients
Jwt\_\_TokenExpirationMinutes Minutos de expiração 60
ASPNETCORE_URLS (opcional) URL Kestrel http://+:8080

Em Testing, o app usa InMemory; em Produção (Docker), usa Postgres + migrações automáticas.

⸻

Troubleshooting
• Swagger não abre: http://localhost:8080/swagger — confirme que o container api está listening em http://[::]:8080 nos logs.
• HTTPS redirect em Docker: em produção a app não força HTTPS; UseHttpsRedirection() só roda em Development.
• PendingModelChangesWarning: a aplicação aplica MigrateAsync() no startup; se mudou o modelo, gere nova migração local e faça rebuild:

dotnet ef migrations add Nova
docker compose up --build

    •	401 / JWT inválido: confira o header Authorization: Bearer <TOKEN> e permissões (Admin vs Seller).

⸻

Licença & Versão
• Licença: MIT (sugestão; inclua LICENSE na raiz se desejar)
• Versão: v1.0.0

⸻

Créditos

Desafio baseado no enunciado “Arlequim Stack — Desafio Técnico Backend”.
