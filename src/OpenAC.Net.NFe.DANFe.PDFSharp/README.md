# OpenAC.Net.NFe.DANFe.PDFSharp

Gera DANFE NF-e A4 em retrato/paisagem, DANFE NFC-e de 58/80 mm, representação de eventos
e comprovante de inutilização com PDFsharp. Recebe os modelos tipados, usa fontes Liberation Sans
incorporadas e QR Code vetorial, e funciona sem impressora ou interface gráfica.

```csharp
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.DANFe.PDFSharp;
using OpenAC.Net.NFe.DANFe.PDFSharp.Configuracao;

var gerador = new OpenDANFe(new DANFeConfig
{
    Paisagem = false,
    LarguraNFCeMilimetros = 80,
    MensagemRodape = "Minha empresa"
});
var documento = NFeProc.Load("nota-processada.xml");
byte[] pdf = gerador.GerarPDF(documento);
gerador.GerarPDF("danfe.pdf", documento);
await gerador.GravarPDFAsync("danfe.pdf", new[] { documento }, cancellationToken);
```

`GerarEventoPDF` recebe `NFeEvento` ou `NFeProcEvento`; `GerarInutilizacaoPDF` recebe
`NFeProcInutilizacao`. Os documentos são carregados pelos métodos do DFe.Core.

A configuração permite margens, canhoto, logotipo em bytes, rodapé, senhas e permissões.
Cada geração captura sua configuração. Os layouts ficam em `Layout` e os pontos de extensão
são `RenderizarNota`, `RenderizarEvento` e `RenderizarInutilizacao`.

Descrições extensas continuam em outras páginas, com repetição do cabeçalho e das colunas.
O cupom ajusta a altura e pode usar várias páginas para documentos extensos.

Os layouts FPDF da NF-e e NFC-e do ACBr foram consultados como referência para a organização
dos blocos; a implementação C# é própria. Veja o README da raiz para exemplos e validação.
