# OpenAC.Net.NFe.DANFe.PDFSharp.Web

Integração por injeção de dependência para gerar PDFs em APIs e Workers.
A impressão usa configuração e instância do gerador próprias por chamada, independentemente da emissão.

```csharp
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.DANFe.PDFSharp.Web;

services.AddDANFePdfSharp(config =>
{
    config.MensagemRodape = "Minha empresa";
    config.LarguraNFCeMilimetros = 80;
});

// Injete IOpenDANFeWeb no endpoint ou Worker.
var documento = NFeProc.Load("nota-processada.xml");
var bytes = await danfe.GerarPDFAsync(documento, empresaId, cancellationToken);
```

Registre `IDANFeConfigurationProvider` para obter configurações diferentes por empresa.
`GerarPDFAsync` também aceita coleções; `GerarEventosPDFAsync` recebe `NFeProcEvento` e
`GerarInutilizacaoPDFAsync` recebe `NFeProcInutilizacao`.

O cancelamento é observado antes de obter configurações e durante a renderização.
