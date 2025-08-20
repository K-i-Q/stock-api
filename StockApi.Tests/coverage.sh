#!/usr/bin/env bash
set -e
rm -rf TestResults coveragereport
dotnet test --collect:"XPlat Code Coverage"
reportgenerator \
  "-reports:TestResults/**/coverage.cobertura.xml" \
  "-targetdir:coveragereport" \
  "-assemblyfilters:+StockApi" \
  "-filefilters:-*Migrations/*.cs;-*Dtos/*.cs"
open coveragereport/index.html
