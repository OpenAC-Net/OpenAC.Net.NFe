# OpenAC.Net.NFe

[![NuGet Version](https://img.shields.io/nuget/v/OpenAC.Net.NFe.svg)](https://www.nuget.org/packages/OpenAC.Net.NFe/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/OpenAC.Net.NFe.svg)](https://www.nuget.org/packages/OpenAC.Net.NFe/)
[![Discord](https://img.shields.io/badge/Chat%20on-Discord-purple.svg)](https://discord.com/invite/brdmJ7Yv6w)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.6.2%20%7C%204.8-blue.svg)](https://dotnet.microsoft.com/)
[![.NET Standard e .NET](https://img.shields.io/badge/.NET-netstandard2.0%20%7C%20net8.0%20%7C%20net9.0%20%7C%20net10.0-blue.svg)](https://dotnet.microsoft.com/)

Biblioteca .NET multiplataforma para geração, assinatura, validação, transmissão e consulta de **NF-e (modelo 55)** e **NFC-e (modelo 65)**.

Desenvolvida pelo time **OpenAC .Net**, utiliza o **OpenAC.Net.DFe.Core** para serialização XML, assinatura digital, comunicação SOAP e armazenamento de documentos. A solução oferece geração de DANFE em PDF, impressão ESC/POS e integração com aplicações ASP.NET Core.

---

## 🚀 Funcionalidades

- **Emissão de NF-e e NFC-e:** autorização síncrona, envio de lotes de NF-e e consulta de recibo.
- **Consultas:** situação do serviço, situação da nota e cadastro de contribuintes.
- **Eventos:** cancelamento, carta de correção, cancelamento por substituição e manifestações do destinatário.
- **Inutilização de numeração:** solicitação e documento processado tipado.
- **Distribuição DF-e:** consulta por último NSU, NSU específico ou chave, com descompactação dos documentos retornados.
- **Assinatura e validação:** XMLDSig, validação da chave, conferência de assinatura e validação pelos XSDs de cada leiaute.
- **NFC-e:** QR Code 2 e 3, emissão normal e contingência offline.
- **DANFE em PDF:** A4 retrato/paisagem, cupom NFC-e de 58/80 mm, eventos e inutilização.
- **Impressão ESC/POS:** cupom NFC-e, QR Code, pagamentos, troco, corte e exportação para bytes, texto ou preview PNG.
- **CNPJ alfanumérico:** tratamento da chave e dos campos previstos nos schemas 4.00 incluídos.
- **Modelos fiscais tipados:** datas, enums, valores decimais, alternativas de tributação e campos de IS e IBS/CBS presentes nos schemas 4.00 incluídos.
- **Aplicações Web:** injeção de dependências, configuração e certificado por empresa, `IHttpClientFactory`, métricas e rastreamento.

---

## 📋 Leiautes e Compatibilidade

| Leiaute | Ler, gerar, assinar e validar XML | DANFE | Comunicação SEFAZ |
| --- | --- | --- | --- |
| **3.10** | Sim | Sim | Bloqueada pela biblioteca |
| **4.00** | Sim | Sim | Sim |

O projeto contempla os leiautes **3.10 e 4.00**. Os modelos compartilham as classes; `InfNFe.Versao` determina os campos serializados. A troca de versão não converte automaticamente informações exclusivas de um leiaute.

As bibliotecas de emissão e impressão atendem a **.NET Framework 4.6.2/4.8**, **.NET Standard 2.0** e **.NET 8/9/10**. Os módulos Web atendem a **.NET 8/9/10**.

---

## 📦 Instalação

### .NET CLI

```bash
dotnet add package OpenAC.Net.NFe
```

### Package Manager Console

```powershell
Install-Package OpenAC.Net.NFe
```

Instale os módulos adicionais conforme a aplicação:

| Pacote | Finalidade |
| --- | --- |
| `OpenAC.Net.NFe` | Modelos, XML, assinatura, validação e serviços SEFAZ |
| `OpenAC.Net.NFe.DANFe.PDFSharp` | Geração local de DANFE em PDF |
| `OpenAC.Net.NFe.DANFe.EscPos` | Impressão de NFC-e com o componente OpenAC.Net.EscPos |
| `OpenAC.Net.NFe.Web` | Clientes e configuração por empresa em aplicações Web |
| `OpenAC.Net.NFe.DANFe.PDFSharp.Web` | Serviço de geração de PDF por injeção de dependências |

Por exemplo, para gerar PDF:

```bash
dotnet add package OpenAC.Net.NFe.DANFe.PDFSharp
```

---

## 🛠️ Como Usar

Os exemplos abaixo usam dados fictícios em homologação. Preencha os dados do emitente, destinatário, produtos e tributação conforme a operação. A aplicação calcula os tributos e totais; a biblioteca não realiza esses cálculos automaticamente.

### 1. Configurando o cliente

```csharp
using System;
using System.IO;
using System.Threading;
using OpenAC.Net.DFe.Core.Common;
using OpenAC.Net.NFe;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.Common.Types;
using OpenAC.Net.NFe.Certificates;
using NotaFiscal = OpenAC.Net.NFe.Common.Model.NFe;

var cancellationToken = CancellationToken.None;

var config = new NFeConfig
{
    Geral =
    {
        Modelo = ModeloNFe.NFe,
        VersaoDFe = NFeVersao.v400
    },
    WebServices =
    {
        UF = DFeSiglaUF.SP,
        Ambiente = DFeTipoAmbiente.Homologacao
    },
    Certificados =
    {
        Certificado = @"C:\Certificados\certificado.pfx",
        Senha = Environment.GetEnvironmentVariable("NFE_CERTIFICADO_SENHA") ?? ""
    },
    Arquivos =
    {
        PathSchemas = Path.Combine(AppContext.BaseDirectory, "Schemas"),
        PathSalvar = @"C:\NFe\Documentos",
        Salvar = true
    }
};

IOpenNFeClient nfe = new OpenNFe(config);
```

Também são aceitos PFX em `Certificados.CertificadoBytes`, certificado instalado ou uma instância em `NFeConfig.Certificado`. Para especializar a obtenção, implemente `INFeCertificadoProvider`.

`OpenNFe` captura uma cópia da configuração na criação e copia os documentos por operação. Para usar outra configuração, crie outro cliente. A instância fornecida em `Certificado` pertence à aplicação; cada operação usa e descarta sua própria cópia.

### 2. Montando uma NF-e

O exemplo cria uma NF-e 4.00 com um item. Os grupos de tributação usam interfaces para selecionar uma única alternativa de cada `choice`.

```csharp
var nota = new NotaFiscal
{
    InfNFe = new NFeInfNFe
    {
        Versao = NFeVersao.v400,
        Ide = new NFeIde
        {
            CUF = DFeCodUF.SP,
            CNF = "12345678",
            NatOp = "VENDA DE MERCADORIA",
            Mod = ModeloNFe.NFe,
            Serie = 1,
            NNF = 1,
            DhEmi = DateTimeOffset.Now,
            TpNF = TipoNFe.Saida,
            IdDest = IndOperacao.Interna,
            CMunFG = 3550308,
            TpImp = TipoImpressao.Retrato,
            TpEmis = TipoEmissao.Normal,
            TpAmb = DFeTipoAmbiente.Homologacao,
            FinNFe = FinalidadeNFe.Normal,
            IndFinal = ConsumidorFinal.Sim,
            IndPres = PresencaComprador.Presencial,
            ProcEmi = ProcessoEmissao.AplicativoContribuinte,
            VerProc = "MinhaAplicacao 1.0"
        },
        Emit = new NFeEmit
        {
            CNPJ = "12345678000195",
            XNome = "EMPRESA DE EXEMPLO LTDA",
            IE = "123456789012",
            CRT = CRT.RegimeNormal,
            EnderEmit = new NFeEmitEnderEmit
            {
                XLgr = "Rua de Exemplo", Nro = "100", XBairro = "Centro",
                CMun = 3550308, XMun = "Sao Paulo", UF = DFeSiglaUF.SP,
                CEP = "01001000", CPais = 1058, XPais = "BRASIL"
            }
        },
        Dest = new NFeDest
        {
            CNPJ = "98765432000198",
            XNome = "NF-E EMITIDA EM AMBIENTE DE HOMOLOGACAO - SEM VALOR FISCAL",
            IndIEDest = IndIEDest.NaoContribuinte,
            EnderDest = new NFeDestEnderDest
            {
                XLgr = "Rua do Cliente", Nro = "200", XBairro = "Centro",
                CMun = 3550308, XMun = "Sao Paulo", UF = DFeSiglaUF.SP,
                CEP = "01001000"
            }
        },
        Det =
        {
            new NFeDet
            {
                NItem = 1,
                Prod = new NFeDetProd
                {
                    CProd = "001", CEAN = "SEM GTIN", XProd = "PRODUTO DE EXEMPLO",
                    NCM = "22021000", CFOP = 5102, UCom = "UN",
                    QCom = 1m, VUnCom = 10m, VProd = 10m,
                    CEANTrib = "SEM GTIN", UTrib = "UN", QTrib = 1m,
                    VUnTrib = 10m, IndTot = IndTotal.Compoe
                },
                Imposto = new NFeDetImposto
                {
                    Imposto = new NFeDetImpostoICMS
                    {
                        Icms = new NFeDetImpostoICMSICMS00
                        {
                            Orig = OrigemMercadoria.Nacional,
                            CST = CSTIcms.Cst00,
                            ModBC = BaseIcms.ValorOperacao,
                            VBC = 10m, PICMS = 18m, VICMS = 1.80m
                        }
                    },
                    PIS = new NFeDetImpostoPIS
                    {
                        TipoPIS = new NFeDetImpostoPISPISNT { CST = CSTPIS.pis07 }
                    },
                    COFINS = new NFeDetImpostoCOFINS
                    {
                        TipoCOFINS = new NFeDetImpostoCOFINSCOFINSNT { CST = CSTCOFINS.cofins07 }
                    }
                }
            }
        },
        Total = new NFeTotal
        {
            ICMSTot = new NFeTotalICMSTot
            {
                VBC = 10m, VICMS = 1.80m, VProd = 10m, VNF = 10m
            }
        },
        Transp = new NFeTransp { ModFrete = ModalidadeFrete.mfSemFrete },
        Pag =
        {
            new NFePag
            {
                DetPag = { new NFePagDetPag { TPag = FormaPagamento.fpDinheiro, VPag = 10m } }
            }
        }
    }
};
```

Para editar um XML existente, use `NotaFiscal.Load("nota-sem-assinatura.xml")`. Datas com fuso usam `DateTimeOffset`; informe o offset correspondente à operação. Identificadores permanecem textuais quando necessário para preservar zeros e caracteres permitidos.

### 3. Gerando a chave, assinando e validando

```csharp
using var certificado = await new NFeCertificadoProvider()
    .ObterAsync(config.Certificados, cancellationToken);

nota.GerarChave();
nota.Assinar(certificado);
nota.Validar(config.Arquivos.PathSchemas);
nota.Save("nota-assinada.xml");

Console.WriteLine($"Chave: {nota.Chave}");
Console.WriteLine($"Assinatura válida: {nota.ValidarAssinatura()}");
```

Preencha e revise os dados antes de assinar. Alterar campos assinados invalida a assinatura. `Load`, `Save` e `GetXml` são fornecidos pelo DFe.Core; `Validar` verifica o XSD correspondente à versão da nota.

### 4. Enviando para autorização

```csharp
var resposta = await nfe.EnviarAsync(new NFeEnvioSolicitacao
{
    Lote = "1",
    Sincrono = true,
    Documentos = new[] { nota }
}, cancellationToken);

Console.WriteLine($"Lote: {resposta.CStat} - {resposta.XMotivo}");

foreach (var autorizada in resposta.Documentos)
{
    autorizada.Save(autorizada.Chave + "-procNFe.xml");
    Console.WriteLine($"NF-e autorizada: {autorizada.Chave}");
}
```

O envio assina documentos ainda não assinados, gera o QR Code quando necessário e valida a assinatura, a chave e o XML antes da transmissão. Documentos já assinados precisam manter a assinatura válida.

| Modalidade | Configuração |
| --- | --- |
| NF-e síncrona | Uma nota, `Sincrono = true` |
| NF-e assíncrona | De 1 a 50 notas, `Sincrono = false`, seguida de consulta do recibo |
| NFC-e | Uma nota, sempre síncrona |

Receber um lote não significa que as notas foram autorizadas. `Documentos` contém as notas associadas a protocolos de autorização com status 100/150. Consulte `Resultado` para os protocolos e rejeições individuais; `XmlEnvio` e `XmlRetorno` preservam os XMLs da operação.

Para um envio assíncrono, consulte o recibo fornecendo as cópias assinadas de `DocumentosEnviados`:

```csharp
if (!string.IsNullOrEmpty(resposta.Recibo))
{
    var recibo = await nfe.ConsultaReciboAsync(new NFeReciboSolicitacao
    {
        Recibo = resposta.Recibo,
        Documentos = resposta.DocumentosEnviados
    }, cancellationToken);

    foreach (var autorizada in recibo.Documentos)
        autorizada.Save(autorizada.Chave + "-procNFe.xml");
}
```

Sem os documentos na consulta do recibo, o retorno contém os protocolos, sem montar as notas processadas. A aplicação controla a frequência e a retomada das consultas.

### 5. Consultando notas e o serviço

```csharp
var status = await nfe.ConsultarSituacaoServicoAsync(cancellationToken);
Console.WriteLine($"Serviço: {status.CStat} - {status.XMotivo}");

var situacao = await nfe.ConsultarAsync(nota.Chave, cancellationToken);
Console.WriteLine($"Nota: {situacao.CStat} - {situacao.XMotivo}");

var cadastro = await nfe.ConsultaCadastroAsync("12345678000195", "CNPJ", cancellationToken);
```

---

## 🧾 Eventos e Inutilização

### Cancelamento

```csharp
var processada = NFeProc.Load("nota-autorizada.xml");
var evento = NFeEvento.Cancelamento(
    processada.Chave,
    processada.NFe.InfNFe.Emit.CNPJ,
    processada.ProtNFe.InfProt.NProt,
    "Justificativa do cancelamento da operacao");

var retornoEvento = await nfe.EnviarEventoAsync(new NFeEventoSolicitacao
{
    Lote = "1",
    Eventos = new[] { evento }
}, cancellationToken);

foreach (var registrado in retornoEvento.Eventos)
    registrado.Save("cancelamento-processado.xml");
```

As factories `CartaCorrecao`, `Manifestacao` e `CancelamentoSubstituicao` preenchem os detalhes dos respectivos eventos. A carta de correção é destinada ao modelo 55. Eventos adicionais podem ser construídos com `Codigo`, `Versao`, `Orgao` e `Detalhe` tipado; o schema específico do evento também é validado.

Verifique o status individual em `Resultado.Eventos`, além do status do lote.

### Inutilização de numeração

```csharp
var inutilizacao = await nfe.InutilizarAsync(new NFeInutilizacaoSolicitacao
{
    Cnpj = "12345678000195",
    Ano = DateTime.Now.Year % 100, // Ano com dois dígitos.
    Serie = 1,
    NumeroInicial = 10,
    NumeroFinal = 12,
    Justificativa = "Numeracao nao utilizada por falha no sistema emissor"
}, cancellationToken);

if (inutilizacao.Documento != null)
    inutilizacao.Documento.Save("inutilizacao-processada.xml");
```

O documento processado é disponibilizado quando o retorno confirma a inutilização com status 102.

---

## 📥 Distribuição DF-e

```csharp
var distribuicao = await nfe.DistribuicaoDFeAsync(new NFeDistribuicaoSolicitacao
{
    CpfCnpj = "12345678000195",
    UltimoNsu = "000000000000000"
}, cancellationToken);

foreach (var distribuido in distribuicao.DocumentosDistribuidos())
    Console.WriteLine($"NSU: {distribuido.NSU} - Schema: {distribuido.Schema}");

Console.WriteLine($"Último NSU: {distribuicao.UltNSU}");
Console.WriteLine($"Maior NSU: {distribuicao.MaxNSU}");
```

Informe exatamente um filtro: `UltimoNsu`, `Nsu` ou `Chave`. `DocumentosDistribuidos()` descompacta os `docZip`, com limite de 16 MiB por documento. A aplicação mantém o NSU e controla a continuidade das consultas.

---

## 🖨️ DANFE e Impressão

### DANFE em PDF

Instale `OpenAC.Net.NFe.DANFe.PDFSharp`:

```csharp
using OpenAC.Net.NFe.DANFe.PDFSharp;
using OpenAC.Net.NFe.DANFe.PDFSharp.Configuracao;

var documento = NFeProc.Load("nota-autorizada.xml");
var danfe = new OpenDANFe(new DANFeConfig
{
    Paisagem = false,
    MensagemRodape = "Minha empresa",
    LarguraNFCeMilimetros = 80
});

byte[] pdf = danfe.GerarPDF(documento);
danfe.GerarPDF("danfe.pdf", documento);

var eventoProcessado = NFeProcEvento.Load("evento-processado.xml");
byte[] pdfEvento = danfe.GerarEventoPDF(eventoProcessado);

var inutilizacaoProcessada = NFeProcInutilizacao.Load("inutilizacao-processada.xml");
byte[] pdfInutilizacao = danfe.GerarInutilizacaoPDF(inutilizacaoProcessada);
```

O modelo 55 seleciona o DANFE A4; o modelo 65 seleciona o cupom NFC-e. A largura térmica pode ser **58 ou 80 mm**. A configuração permite margens, logotipo em memória, canhoto, rodapé, senhas e permissões do PDF.

A API também aceita notas e eventos sem protocolo. `ComProtocolo` permite montar um `NFeProc` a partir da nota e de um protocolo, conferindo chave, versão e digest. A configuração de impressão é independente da emissão e é copiada por operação. Veja o [README PDFSharp](src/OpenAC.Net.NFe.DANFe.PDFSharp/README.md).

### NFC-e em ESC/POS

Instale `OpenAC.Net.NFe.DANFe.EscPos`:

```csharp
using System.Text;
using OpenAC.Net.Devices;
using OpenAC.Net.EscPos;
using OpenAC.Net.EscPos.Commom;
using OpenAC.Net.NFe.DANFe.EscPos;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

using var impressora = new EscPosPrinter<TCPConfig>(new TCPConfig
{
    IP = "192.168.0.100",
    Porta = 9100
});
impressora.PaginaCodigo = PaginaCodigo.pc850;
impressora.Colunas = 48;
impressora.Conectar();

var nfceProcessada = NFeProc.Load("nfce-autorizada.xml");
var cupom = new OpenDANFeEscPos();
cupom.Imprimir(nfceProcessada, impressora);
```

A aplicação configura e conecta a impressora; a conexão permanece disponível após imprimir. Também existem `ImprimirAsync`, exportação de bytes com `Gerar`, texto com `GerarTexto` e preview PNG com `GerarPreview`. Confira a saída no equipamento de destino. Veja o [README ESC/POS](src/OpenAC.Net.NFe.DANFe.EscPos/README.md).

---

## 📱 NFC-e, QR Code e Contingência

Configure `Geral.Modelo = ModeloNFe.NFCe` e use uma nota com modelo 65. O **QR Code 3** é o padrão; o **QR Code 2** usa `Geral.IdCSC` e `Geral.CSC`. A geração de QR Code da biblioteca atende à NFC-e 4.00.

Para uma NFC-e normal, assine antes de gerar o QR Code:

```csharp
var configNfce = config.Copiar();
configNfce.Geral.Modelo = ModeloNFe.NFCe;
configNfce.Geral.VersaoQRCode = NFeVersaoQRCode.v300;

var nfce = NotaFiscal.Load("nfce-sem-assinatura.xml");
nfce.GerarChave();
nfce.Assinar(certificado);
nfce.GerarQrCode(configNfce);
nfce.Validar(configNfce.Arquivos.PathSchemas);
nfce.Save("nfce-assinada.xml");
```

Na contingência offline, preencha `TpEmis = TipoEmissao.OffLine`, `DhCont` e `XJust` **antes** de gerar a chave e assinar. Configure também `Geral.FormaEmissao = DFeTipoEmissao.OffLine` no cliente correspondente. O QR Code 2 utiliza o digest da assinatura XML; o QR Code 3 assina os parâmetros com a chave RSA do certificado.

Para NF-e em SVC, configure `Geral.FormaEmissao` com `SVCAN` ou `SVCRS` e mantenha o mesmo tipo de emissão no documento. A NFC-e usa o autorizador normal da UF para emissão normal ou offline.

A impressão apresenta as indicações de homologação, contingência e pendência de autorização. Para indicar cancelamento após consulta ou evento, use `NFeProc.Cancelado = true`; o XML da nota autorizada permanece preservado.

---

## 🌐 Integração com ASP.NET Core

Instale `OpenAC.Net.NFe.Web` e, para PDF por injeção de dependências, `OpenAC.Net.NFe.DANFe.PDFSharp.Web`.

Para uma única empresa, registre a configuração direta:

```csharp
using Microsoft.Extensions.DependencyInjection;
using OpenAC.Net.NFe.Web;

var services = new ServiceCollection();
services.AddNFe(options =>
{
    options.Certificado = certificado;
    options.Geral.Modelo = ModeloNFe.NFe;
    options.WebServices.UF = DFeSiglaUF.SP;
    options.WebServices.Ambiente = DFeTipoAmbiente.Homologacao;
    options.Arquivos.PathSchemas = config.Arquivos.PathSchemas;
});
```

Para múltiplas empresas, implemente `INFeConfigurationProvider` e `INFeCertificateProvider` e registre `AddOpenNFeWebMultiTenant<MeuConfigurationProvider, MeuCertificateProvider>()` no lugar de `AddNFe`.

Receba `IOpenNFeClientFactory` por injeção e abra uma sessão para a empresa:

```csharp
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var factory = scope.ServiceProvider.GetRequiredService<IOpenNFeClientFactory>();

using var sessao = await factory.CreateAsync("empresa-1", cancellationToken);
var statusEmpresa = await sessao.Client.ConsultarSituacaoServicoAsync(cancellationToken);
```

Descarte a sessão após o uso. A fábrica separa os handlers HTTP por empresa, certificado e autoridade HTTPS. A instrumentação usa `ActivitySource` e `Meter` chamados `OpenAC.Net.NFe.Web`, sem conteúdo fiscal ou CSC. Veja o [README Web](src/OpenAC.Net.NFe.Web/README.md).

Para PDF, use `AddDANFePdfSharp` e injete `IOpenDANFeWeb`. `IDANFeConfigurationProvider` permite definir a configuração de impressão por empresa; cada chamada utiliza uma instância independente de `OpenDANFe`. Veja o [README PDFSharp.Web](src/OpenAC.Net.NFe.DANFe.PDFSharp.Web/README.md).

---

## ⚙️ Configurações e Persistência

- **Documentos processados:** habilite `Arquivos.Salvar` e configure `Arquivos.PathSalvar`.
- **Envelopes de comunicação:** habilite `WebServices.Salvar`.
- **Organização de diretórios:** use as opções de separação por contribuinte, modelo, ano, mês e dia.
- **Armazenamento personalizado:** implemente `IDFeDocumentStore`, fornecido pelo DFe.Core.
- **Transporte personalizado:** configure `NFeConfig.HttpTransport` com uma implementação de `IDFeHttpTransport`.
- **Endereços de serviço:** use `WebServices.Enderecos[TipoServicoNFe]` ou um catálogo XML em `Arquivos.ArquivoServicos`.

O catálogo de serviços fica em `Resources/services.xml`. A aplicação controla retomadas e consultas; os POSTs fiscais não são repetidos automaticamente. Providers, transportes e stores compartilhados devem aceitar concorrência.

---

## 🧪 Compilar e Testar

Para desenvolver a solução, utilize o **SDK .NET 10** e o **runtime .NET 8** para os testes e a demonstração:

```bash
dotnet build src/OpenAC.Net.NFe.slnx
dotnet test src/OpenAC.Net.NFe.Tests/OpenAC.Net.NFe.Tests.csproj
```

A biblioteca referencia `OpenAC.Net.DFe.Core` **1.8.0-beta.5**. Para desenvolver com um projeto local do Core, informe explicitamente `-p:DFeCoreProject=/caminho/OpenAC.Net.DFe.Core.csproj`.

`SchemaCoverageTests` percorre os XSDs compilados 3.10/4.00 e os contratos do assembly, conferindo campos, alternativas, enums, coleções, ordem e filtros por versão. Os testes também verificam documentos assinados, protocolos, serviços simulados e impressão. Essa validação utiliza schemas locais e não substitui a homologação junto à SEFAZ.

As bibliotecas .NET Framework podem ser compiladas no Linux com os reference assemblies; sua execução deve ser validada no Windows. Para atualizar schemas, revise os modelos afetados e execute a auditoria e a suíte de testes.

O projeto `OpenAC.Net.NFe.Demo` permite gerar saídas locais a partir de um XML fornecido pela aplicação:

```bash
dotnet run --project src/OpenAC.Net.NFe.Demo -- danfe nota-autorizada.xml danfe.pdf
dotnet run --project src/OpenAC.Net.NFe.Demo -- escpos-preview nfce-autorizada.xml nfce.png 32
```

---

## 🤝 Como Contribuir

Contribuições de código, testes e documentação são bem-vindas:

1. Abra uma issue para relatar um problema ou discutir uma melhoria.
2. Crie uma branch com a alteração e os testes pertinentes.
3. Execute a compilação e os testes e envie um pull request descrevendo o comportamento alterado.

Ao atualizar um leiaute, inclua o schema de referência e preserve a cobertura das versões suportadas. Os XSDs e o catálogo de serviços são mantidos no projeto; as regras fiscais e os requisitos específicos de cada operação e UF devem ser atendidos pela aplicação emissora.

---

## 💖 Apoie o Projeto OpenAC .Net

- Participe da comunidade no [Discord](https://discord.com/invite/brdmJ7Yv6w).
- Compartilhe sugestões e contribuições no [GitHub](https://github.com/OpenAC-Net/OpenAC.Net.NFe).

---

## 📄 Licença

Este projeto é distribuído sob a licença **MIT**. Consulte [LICENSE](LICENSE) para mais detalhes.
