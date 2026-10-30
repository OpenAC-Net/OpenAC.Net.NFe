# OpenAC.Net.NFe

Biblioteca .NET para NF-e (modelo 55) e NFC-e (modelo 65), organizada nos moldes do OpenAC.Net.CTe e integrada ao OpenAC.Net.DFe.Core.

| Leiaute | Ler, gerar, assinar e validar XML | PDF | Comunicação SEFAZ |
| --- | --- | --- | --- |
| 3.10 | Sim | NF-e, NFC-e e eventos | Bloqueada |
| 4.00 | Sim | NF-e, NFC-e e eventos | Sim |

O escopo adotado é **3.10 e 4.00**. A versão 2.00 não integra este projeto.

## Solução

| Projeto | Finalidade | Frameworks |
| --- | --- | --- |
| `OpenAC.Net.NFe` | Modelos, XML, assinatura, validação, QR Code e serviços | net462, net48, netstandard2.0, net8.0, net9.0, net10.0 |
| `OpenAC.Net.NFe.DANFe.PDFSharp` | DANFE A4 retrato/paisagem, NFC-e 58/80 mm, eventos e inutilização | Os mesmos do núcleo |
| `OpenAC.Net.NFe.DANFe.EscPos` | Impressão NFC-e em comandos Epson ESC/POS | Os mesmos do núcleo |
| `OpenAC.Net.NFe.Web` | DI, configurações/certificados por empresa, IHttpClientFactory e observabilidade | net8.0, net9.0, net10.0 |
| `OpenAC.Net.NFe.DANFe.PDFSharp.Web` | Serviço de geração de PDF para APIs/Workers | net8.0, net9.0, net10.0 |
| `OpenAC.Net.NFe.Demo` | Exemplo de linha de comando para impressão local | net8.0 |
| `OpenAC.Net.NFe.Tests` | Testes de documentos, serviços simulados e impressão | net8.0 |

A biblioteca principal segue a organização do CT-e: configurações e utilitários em `Common`,
modelos, documentos, eventos e respostas em `Common/Model`, enums em `Common/Types` e serviços
por operação em `Services`. Envio fica em `Services/Recepcao` e consulta de recibo em
`Services/RetRecepcao`. Os métodos de composição de diretórios ficam em
`Common/NFeConfigArquivos.Paths.cs`; certificados, schemas e catálogo permanecem em
`Certificates`, `Schemas` e `Resources`.

Os modelos formam uma única família de classes no namespace `OpenAC.Net.NFe`, organizada em
`Common/Model`, como no CT-e. `NFe.InfNFe.Versao` seleciona `NFeVersao.v310` ou `NFeVersao.v400`;
os grupos compartilham a referência `Parent` do Core e usam `ShouldSerialize` para os campos de cada
leiaute. Os grupos ICMS, IPI, PIS, COFINS, ISSQN, IS e IBS/CBS são gerados dos dois XSD sem duplicar
classes por versão. Códigos, datas e identificadores permanecem strings; valores e quantidades são
`decimal`, com campos opcionais anuláveis. As diferenças de obrigatoriedade e quantidade são
validadas pelo XSD correspondente à versão.

`InfNFe.Pag` e `Det.Prod.Med` são coleções nos dois leiautes: o schema 4.00 limita cada um desses grupos
a uma ocorrência. Na 3.10, o pagamento fica diretamente em `NFePag.TPag`, `VPag` e `Card`; na 4.00,
fica em `NFePag.DetPag`, com `VTroco` no grupo. Selecionar outra versão controla as tags serializadas,
mas não converte automaticamente informações fiscais exclusivas de um leiaute.

## Compilar e testar

Requer SDK .NET 10 e runtime .NET 8 para o exemplo/testes.

```sh
cd src
dotnet build OpenAC.Net.NFe.slnx
dotnet test OpenAC.Net.NFe.Tests/OpenAC.Net.NFe.Tests.csproj
```

A biblioteca principal usa o pacote `OpenAC.Net.DFe.Core` **1.8.0-beta.5**, incluindo o source generator.
Para desenvolver o Core em conjunto, informe explicitamente `-p:DFeCoreProject=/caminho/OpenAC.Net.DFe.Core.csproj`.
A existência de um checkout irmão não troca automaticamente a dependência usada pelo build.

As bibliotecas .NET Framework são compiladas no Linux usando os reference assemblies do SDK/NuGet; a execução desses alvos deve ser validada no Windows. PDF e testes são executados em .NET 8 no Linux.

## Ler, gerar e imprimir

```csharp
using OpenAC.Net.NFe;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.DANFe.PDFSharp;
using OpenAC.Net.NFe.DANFe.PDFSharp.Configuracao;
using OpenAC.Net.NFe.DANFe.EscPos;
using OpenAC.Net.Devices;
using OpenAC.Net.EscPos;
using OpenAC.Net.EscPos.Commom;
using System.Text;

var documento = NFeProc.Load("nota-processada.xml"); // nfeProc, 3.10 ou 4.00
// Para um XML sem protocolo: var nota = NFe.Load("nota.xml");
var danfe = new OpenDANFe(new DANFeConfig
{
    Paisagem = false,
    MensagemRodape = "Minha empresa",
    LarguraNFCeMilimetros = 80
});
byte[] pdf = danfe.GerarPDF(documento);
danfe.GerarPDF("danfe.pdf", documento);

// Para NFC-e, o modelo 65 seleciona o cupom automaticamente.
danfe.Configuracoes.LarguraNFCeMilimetros = 58; // ou 80

// Documentos carregados pelo DFe.Core:
var evento = NFeProcEvento.Load("evento-processado.xml");
byte[] eventoPdf = danfe.GerarEventoPDF(evento);
// Para um evento sem retorno: danfe.GerarEventoPDF(NFeEvento.Load("evento.xml"));
var inutilizacao = NFeProcInutilizacao.Load("inutilizacao-processada.xml");
byte[] inutilizacaoPdf = danfe.GerarInutilizacaoPDF(inutilizacao);

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // .NET moderno
using var impressora = new EscPosPrinter<TCPConfig>(new TCPConfig
{
    IP = "192.168.0.100",
    Porta = 9100
});
impressora.PaginaCodigo = PaginaCodigo.pc850;
impressora.Colunas = 48; // 32 para papel estreito
impressora.Conectar();
var cupom = new OpenDANFeEscPos();
cupom.Imprimir(documento, impressora); // exige modelo 65
// await cupom.ImprimirAsync(documento, impressora, cancellationToken);
```

A API PDF recebe `NFe`, `NFeProc`, `NFeEvento`, `NFeProcEvento` e `NFeProcInutilizacao`,
e lê os modelos diretamente. Os layouts ficam em `Layout`, a configuração em `Configuracao`
e a infraestrutura gráfica em `Common`. O cabeçalho e as colunas dos produtos se repetem nas
continuações do A4; descrições extensas continuam nas páginas seguintes. O cupom ajusta a altura
ao conteúdo e continua em outra página ao atingir o limite de tamanho do PDF.

`DANFeConfig` permite margens, logotipo em memória, mensagem de rodapé, canhoto, orientação,
senhas e permissões do PDF. A configuração é copiada por operação. `OpenDANFe` pode ser estendido
sobrescrevendo `RenderizarNota`, `RenderizarEvento` ou `RenderizarInutilizacao`.
`GerarPDF(CancellationToken, params NFeProc[])` observa o cancelamento durante a renderização;
`GravarPDFAsync` grava o resultado em arquivo de forma assíncrona. As sobrecargas com `Stream`
mantêm o stream do chamador aberto.

A impressão ESC/POS usa `EscPosPrinter` do OpenAC.Net.EscPos para texto, logotipo, QR Code, codificação, avanço, corte e envio. A aplicação configura e conecta o componente; a conexão permanece disponível após imprimir. `Gerar` exporta bytes pelo mesmo componente com saída em memória e recebe protocolo e página de código como argumentos. `GerarTexto` exporta texto e `GerarPreview` gera PNG sem conectar uma impressora. O layout permite configurar itens, logotipo, cópias e rodapé; a configuração é copiada por operação. `ImprimirAsync` permite aguardar a fila da impressora de forma assíncrona; o envio utiliza a API síncrona do componente. Veja os detalhes no [README ESC/POS](src/OpenAC.Net.NFe.DANFe.EscPos/README.md).

Exemplo executável, sem transmissão:

```sh
cd src
dotnet run --project OpenAC.Net.NFe.Demo -- danfe ../samples/nfe-55-4.00.xml /tmp/danfe.pdf
dotnet run --project OpenAC.Net.NFe.Demo -- danfe ../samples/nfe-65-4.00.xml /tmp/nfce.pdf 80
dotnet run --project OpenAC.Net.NFe.Demo -- escpos ../samples/nfe-65-4.00.xml /tmp/nfce.bin 48
dotnet run --project OpenAC.Net.NFe.Demo -- escpos-preview ../samples/nfe-65-4.00.xml /tmp/nfce.png 32
```

Os exemplos em `samples` possuem dados fictícios e certificados de teste. Os PDFs de referência mostram protocolos simulados. Não são documentos fiscais válidos.

## Modelos DFe.Core

```csharp
using OpenAC.Net.NFe;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Types;
using OpenAC.Net.NFe.Common.Model;

var modelo = NFe.Load(File.ReadAllText("nota-sem-assinatura.xml"));
modelo.InfNFe.Versao = NFeVersao.v400;
modelo.InfNFe.Ide.NNF = "123";
modelo.InfNFe.Ide.CNF = "87654321";
modelo.InfNFe.Ide.CDV = "0";
modelo.GerarChave();
modelo.Assinar(certificado);
modelo.Validar(pathSchemas);
modelo.Save("nota-assinada.xml");

// O protocolo retornado pela SEFAZ também é um documento tipado.
var protocolo = NFeProtNFe.Load("protocolo.xml");
NFeProc processada = modelo.ComProtocolo(protocolo);
processada.Save(modelo.Chave + "-procNFe.xml");
```

Para criar do zero, instancie os grupos dos modelos, por exemplo `NFeInfNFe`, `NFeIde`, `NFeEmit`, `NFeDet`, `NFeDetProd`, `NFeTotal`. As classes mantêm os nomes XML nas propriedades e a documentação dos schemas. Preencha apenas uma alternativa de cada `xs:choice`; a validação XSD verifica as combinações e as ocorrências obrigatórias. A biblioteca não calcula automaticamente os tributos ou totais da nota.

`NFe` herda de `DFeSignDocument<NFe>` e `NFeProc` herda de `DFeDocument<NFeProc>`,
seguindo a organização da CTe. `Load`, `Save` e `GetXml` são os métodos do Core; a serialização
é gerada pelos atributos dos modelos. A NF-e contém `InfNFe` e a assinatura; o documento processado
contém `NFe`, `ProtNFe` e `Versao`.

Edite o modelo antes de assinar. `ValidarAssinatura()` verifica os dados atuais e detecta alterações
nos campos assinados. `ComProtocolo` verifica chave, versão e `DigestValue`, e copia a nota e o protocolo
para o documento processado. O envio também valida a assinatura e a correspondência da chave com os campos.

## Autorização 4.00

```csharp
using OpenAC.Net.NFe;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Types;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.DFe.Core.Common;

var config = new NFeConfig
{
    Certificado = certificado,
    Geral = { Modelo = ModeloNFe.NFCe, VersaoDFe = NFeVersao.v400 },
    WebServices = { UF = DFeSiglaUF.SP, Ambiente = DFeTipoAmbiente.Homologacao },
    Arquivos = { PathSchemas = pathSchemas }
};
IOpenNFeClient nfe = new OpenNFe(config);
var resposta = await nfe.EnviarAsync(new NFeEnvioSolicitacao
{
    Lote = "1",
    Sincrono = true,
    Documentos = new[] { NFe.Load("nfce-para-enviar.xml") }
}, cancellationToken);
foreach (var autorizada in resposta.Documentos)
    autorizada.Save(autorizada.Chave + "-procNFe.xml");
```

`EnviarAsync` aceita uma nota no modo síncrono e até 50 NF-e no modo assíncrono. NFC-e exige envio individual síncrono. O lote assíncrono retorna recibo; consulte-o com `ConsultaReciboAsync(new NFeReciboSolicitacao { Recibo = recibo, Documentos = documentosDoLote })`. Para associar protocolos, forneça os documentos de `respostaEnvio.DocumentosEnviados`, que contém as cópias assinadas enviadas à SEFAZ. O XML fiscal do lote também fica disponível em `XmlEnvio`. Sem documentos na consulta, a resposta contém apenas os protocolos retornados. Status de lote recebido/em processamento não significa autorização. `Documentos` contém somente notas com protocolo 100/150. As respostas usam `DFeResposta<T>` do Core. Protocolos e rejeições ficam disponíveis em `Resultado`,
`CStat` e `XMotivo`; o XML recebido permanece em `XmlRetorno`.

Eventos usam `NFeEvento : DFeSignDocument<NFeEvento>`, `NFeEventoDetalhe` e `NFeProcEvento`.
Os métodos de criação de cancelamento, CC-e, manifestação e cancelamento por substituição preenchem
os detalhes tipados. Para especializar um detalhe, derive de `NFeEventoDetalhe`; seus métodos de serialização
continuam virtuais e podem ser sobrescritos para o leiaute do evento. A inutilização usa `NFeInutilizacao` e retorna `NFeProcInutilizacao` em `Documento`
quando a SEFAZ confirma a inutilização com status 102.

Outras operações da interface: `ConsultarSituacaoServicoAsync`, `ConsultarAsync`, `ConsultaCadastroAsync`,
`InutilizarAsync(NFeInutilizacaoSolicitacao)`, `EnviarEventoAsync(NFeEventoSolicitacao)` e
`DistribuicaoDFeAsync(NFeDistribuicaoSolicitacao)`. Distribuição aceita exatamente um filtro:
`UltimoNsu`, `Nsu` ou `Chave`. `DocumentosDistribuidos()` descompacta os `docZip` com limite de tamanho.

`OpenNFe` captura a configuração e copia os documentos e eventos antes de executar a operação.
Não possui `NotasFiscais`, configuração mutável pública ou `Dispose`. Chamadas podem usar o mesmo
cliente simultaneamente; os providers, transportes e stores injetados devem aceitar concorrência.
Cada operação obtém e descarta sua própria instância de certificado. `Certificado` é uma opção emprestada,
que deve permanecer válida durante o uso do cliente; também são aceitos PFX em `Certificados.CertificadoBytes`,
arquivo PFX e certificado instalado. `INFeCertificadoProvider` permite especializar a obtenção assíncrona.

Os serviços de autorização, recibo, consulta, status, eventos, cadastro, inutilização e distribuição
herdam de `NFeServiceClient<TResult, TResposta>`, baseado em `DFeSoapServiceClient` do Core. A infraestrutura compartilhada
monta os envelopes SOAP, envia HTTP, aplica cancelamento/timeout e trata SOAP Fault, inclusive com HTTP 500.
O contrato de transporte é `IDFeHttpTransport`, configurado em `NFeConfig.HttpTransport`.
Não há repetição automática dos POST fiscais, compartilhamento de cookies ou redirecionamento automático.
Para SVC, configure `Geral.FormaEmissao` com `DFeTipoEmissao.SVCAN` ou `DFeTipoEmissao.SVCRS` e use
o mesmo `tpEmis` no XML. NFC-e usa emissão normal ou offline, com o autorizador normal da UF.

A persistência usa `IDFeDocumentStore` do Core. Habilite `Arquivos.Salvar` para documentos processados
e `WebServices.Salvar` para envelopes; configure `Arquivos.PathSalvar` e as opções de separação por
contribuinte, modelo e data. A resolução do caminho não cria diretórios nem altera a configuração;
o store controla a gravação. A aplicação continua responsável pela frequência das consultas e retomada após falhas.

## NFC-e e contingência

QR Code **2** usa `IdCSC` e `CSC`; QR Code **3** é o padrão e não usa CSC. Em contingência offline, a versão 2 utiliza o digest da assinatura XML; a versão 3 assina os parâmetros com a chave RSA do certificado. Após preencher `tpEmis=9`, `dhCont` e `xJust`, gere novamente a chave e assine a nota antes de criar o QR Code.

```csharp
documento.Assinar(certificado);
documento.GerarQrCode(config);
documento.Validar(config.Arquivos.PathSchemas);
```

A impressão inclui contingência, pendência de autorização, homologação, situação cancelada, pagamentos, troco, consumidor, chave e QR Code. A situação cancelada deve vir de consulta/evento: configure `documento.Cancelado = true` para imprimir essa indicação; o XML da nota autorizada permanece preservado.

## Eventos

```csharp
var evento = NFeEvento.Cancelamento(chave, cnpjAutor, protocolo, justificativa);
var resposta = await nfe.EnviarEventoAsync(new NFeEventoSolicitacao
{
    Lote = "1",
    Eventos = new[] { evento }
}, cancellationToken);
foreach (var processado in resposta.Eventos)
    processado.Save("evento-processado.xml");
```

Factories incluídas: cancelamento, carta de correção (modelo 55), cancelamento por substituição e as quatro manifestações do destinatário. Eventos adicionais podem ser construídos com `Codigo`, `Versao`, `Orgao` e `Detalhe` (`NFeEventoDetalhe`). O detalhe é validado no schema `e{codigo}_v{versao}.xsd`; o lote é validado no schema genérico de eventos. Cada evento retornado é associado por chave/tipo/sequência. Verifique o `CStat` individual em `Resultado.Eventos`, além do status do lote.

## Web por empresa

```csharp
services.AddOpenNFeWebMultiTenant<MeuConfigurationProvider, MeuCertificateProvider>();
services.AddDANFePdfSharp(config => config.MensagemRodape = "Minha empresa");

// Para impressão, injete IOpenDANFeWeb:
var bytesPdf = await danfeWeb.GerarPDFAsync(documentoProcessado, empresaId, cancellationToken);
// GerarEventosPDFAsync e GerarInutilizacaoPDFAsync recebem os documentos tipados.

// Para emissão, obtenha IOpenNFeClientFactory por injeção.
using var sessao = await factory.CreateAsync(empresaId, cancellationToken);
var status = await sessao.Client.ConsultarSituacaoServicoAsync(cancellationToken);
```

A geração de PDF usa uma instância de `OpenDANFe` por chamada e configuração independente da emissão.
Registre `IDANFeConfigurationProvider` para obter `DANFeConfig` por empresa. A biblioteca aguarda
a configuração de forma assíncrona e renderiza observando o cancelamento, sem abrir tarefas auxiliares.

Implemente `INFeConfigurationProvider` e `INFeCertificateProvider`. A fábrica copia a configuração e o certificado para cada sessão; os handlers HTTP são separados por empresa, certificado e autoridade HTTPS. Para uma única empresa, `services.AddNFe(configure)` aceita a configuração direta. As sessões devem ser descartadas. Instrumentação: `ActivitySource` e `Meter` chamados `OpenAC.Net.NFe.Web`, sem conteúdo dos documentos ou CSC.

## Schemas e referências

Os XSD e os endereços de origem foram copiados do ACBr local informado pelo usuário. O catálogo foi convertido para XML `DFeServices`, preservando os endereços de comunicação 4.00, eventos nacionais, distribuição e URLs públicas de QR Code 2/3, sem manter o leitor INI em produção. A origem e os hashes estão em `src/tools/acbr-sources.json`. Para atualizar, substitua os schemas, execute `python3 src/tools/generate-models.py` e rode os testes novamente. Atualize também o catálogo `Resources/services.xml` quando necessário. URLs podem variar por UF e período; `WebServices.Enderecos[TipoServicoNFe]` permite substituição explícita por instância. `Arquivos.ArquivoServicos` carrega um catálogo XML específico da configuração, sem modificar o catálogo de outros clientes. O carregamento valida as entradas e usa `ReadXml` do Core; a exportação preserva modelo, emissão e versão.

Referências técnicas: [schemas oficiais NF-e](https://www.nfe.fazenda.gov.br/portal/listaConteudo.aspx?tipoConteudo=BMPFMBoln3w%3D), [QR Code NFC-e — SEFA/PR](https://sped.fazenda.pr.gov.br/NFCe/Pagina/QR-Code), [NT Conjunta 2025.001 — CNPJ alfanumérico](https://www.nfe.fazenda.gov.br/Portal/exibirArquivo.aspx?conteudo=5ZkvIZt10mQ%3D).

A validação automatizada usa schemas locais, certificados temporários e transporte simulado; não constitui teste de autorização real na SEFAZ. A saída ESC/POS precisa ser conferida no equipamento de destino. As regras fiscais de cada operação, credenciamento e requisitos específicos de UF devem ser atendidos pela aplicação emissora.
