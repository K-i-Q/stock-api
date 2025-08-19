#!/bin/bash
set -e

echo "🔄 Restaurando pacotes..."
dotnet restore StockSolution.sln

echo "🧹 Verificando formatação (auto-detect flag)..."
if dotnet format --help | grep -q -- "--check"; then
  echo "Using --check"
  dotnet format StockSolution.sln --check
else
  echo "Using --verify-no-changes"
  dotnet format StockSolution.sln --verify-no-changes
fi

echo "⚙️ Buildando em Release..."
dotnet build StockSolution.sln --configuration Release

echo "🧪 Rodando testes com cobertura..."
dotnet test StockSolution.sln --configuration Release --collect:"XPlat Code Coverage"

echo "✅ Tudo certo! Pronto para dar push 🚀"