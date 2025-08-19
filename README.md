StockApi

API de gestão de estoque e pedidos — .NET 9 + ASP.NET Core (Minimal APIs) + EF Core + PostgreSQL + JWT.
Testes com xUnit e EF Core InMemory. Contêiner com Docker e orquestração com Docker Compose.

⸻

🚀 Requisitos
• .NET 9 SDK
• Docker (opcional, recomendado)
• PostgreSQL (se rodar local sem Docker)

⸻

▶️ Rodar com Docker Compose (recomendado)

cd StockApi

# necessário ter um arquivo .env na raiz do repo com JWT_KEY=<chave>

docker compose up --build

    •	API: http://localhost:8080/swagger
    •	Seed automático: admin@local / admin123

⸻

▶️ Rodar local (sem Docker)

dotnet run --project StockApi

    •	API: http://localhost:5000/swagger (ou 5001 https)

Ajuste appsettings.json se necessário.

⸻

🔑 Autenticação
• POST /auth/login → retorna { token }
• No Swagger, clique em Authorize e cole apenas o token (sem Bearer ).

⸻

📌 Endpoints principais
• POST /auth/signup (Admin/Seller)
• POST /auth/login
• GET /products (auth)
• GET /products/{id} (auth)
• POST /products, PUT /products/{id}, DELETE /products/{id} (Admin)
• POST /stock/entries (Admin) → adiciona quantidade + nº da nota fiscal
• POST /orders (Seller/Admin) → valida estoque e baixa automaticamente
• GET /orders/{id} (auth)

⸻

🧪 Testes

dotnet test

Exemplo de saída:

total: 4, failed: 0, succeeded: 4

⸻

🗄️ Migrations (EF Core)

dotnet ef migrations add <Nome>
dotnet ef database update

⸻

🔐 Notas de segurança
• Nunca comitar segredos. Use .env (já ignorado no .gitignore) e variáveis de ambiente.
• Em produção, use uma chave JWT ≥ 256 bits e rotação periódica.

⸻

📖 Exemplos práticos (curl)

1. Login

curl -X POST http://localhost:8080/auth/login \
 -H "Content-Type: application/json" \
 -d '{
"email": "admin@local",
"password": "admin123"
}'

Resposta esperada:

{
"token": "eyJhbGciOi..."
}

2. Criar produto (Admin)

curl -X POST http://localhost:8080/products \
 -H "Content-Type: application/json" \
 -H "Authorization: Bearer <TOKEN_AQUI>" \
 -d '{
"name": "Notebook Dell",
"price": 4500.00,
"stock": 10
}'

Resposta esperada:

{
"id": 38dafa76-ff4b-4241-82bb-f95f8aed1aae,
"name": "Notebook Dell",
"price": 4500.0,
"stock": 10
}
