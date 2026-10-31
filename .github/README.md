# GitHub Actions

Os workflows seguem a estrutura do CT-e, adaptada à solução `src/OpenAC.Net.NFe.slnx` e aos testes TUnit em .NET 10.

## Compilação

`workflows/push.yml` executa em pushes e pull requests para `main`, além da execução manual. Restaura os pacotes, compila a solução em Release no Windows e executa os testes. Os relatórios ficam no artefato `test-results` por sete dias.

## Publicação

`workflows/publish.yml` tem quatro etapas: gerar os pacotes, validá-los, publicar no NuGet.org e notificar no Discord.

- A execução manual gera e valida os pacotes, sem publicar. O campo opcional `version` permite escolher a versão; sem ele, usa a versão de `src/Directory.Build.props` com o sufixo `-ci.<número da execução>`.
- A publicação de uma GitHub Release gera a versão a partir da tag, com ou sem o prefixo `v`. Exemplo: `v1.0.0-beta.1` gera pacotes `1.0.0-beta.1`.
- Restore, build e pack recebem a mesma `Version` e `PackageVersion`. `AssemblyVersion` e `FileVersion` permanecem em `1.0.0`, conforme `src/Directory.Build.props`.
- Com a dependência atual de DFe.Core em pré-release, use uma versão pré-release também para os pacotes NF-e. O empacotamento mantém a verificação do NuGet que impede pacotes estáveis com dependências pré-release.
- Compilação e testes precisam passar antes do empacotamento. A publicação depende da validação e usa `--skip-duplicate` para permitir retomadas.
- O build da publicação usa `GeneratePackageOnBuild=false` para gerar os pacotes apenas na etapa de pack, depois dos testes.
- Os cinco pacotes publicáveis são NF-e, DANFE.PDFSharp, DANFE.EscPos, Web e DANFE.PDFSharp.Web. Demo e testes não são empacotados.

A validação Meziantou verifica assemblies otimizados, autor, licença, README e repositório. Os pacotes também incluem o ícone do projeto, a licença MIT em arquivo, documentação XML e informações de depuração e SourceLink, seguindo o padrão da NFSe.Nacional.

Os símbolos usam PDB portátil (`DebugType=portable`) para gerar os pacotes `.snupkg` em compilações limpas.

Configure os secrets do repositório:

| Secret | Uso |
| --- | --- |
| `NUGET_APIKEY` | Chave de publicação no NuGet.org, necessária para a etapa de publicação |
| `DISCORD_WEBHOOK_URL` | Webhook opcional para avisar após a publicação; sem ele, a notificação é ignorada |

Os artefatos NuGet e relatórios dos testes da publicação são mantidos por sete dias. Os workflows usam os SDKs .NET 8, 9 e 10 na compilação e .NET 10 na validação/publicação.
