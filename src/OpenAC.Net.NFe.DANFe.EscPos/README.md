# OpenAC.Net.NFe.DANFe.EscPos

Compõe o DANFE NFC-e com os modelos tipados da NF-e e usa **OpenAC.Net.EscPos 1.7.0** para texto, logotipo, QR Code, codificação, avanço, corte e envio ao dispositivo configurado (TCP, serial, RAW ou arquivo).

```csharp
using System.Text;
using OpenAC.Net.Devices;
using OpenAC.Net.EscPos;
using OpenAC.Net.EscPos.Commom;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.DANFe.EscPos;
using OpenAC.Net.NFe.DANFe.EscPos.Configuracao;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // .NET moderno

using var impressora = new EscPosPrinter<TCPConfig>(new TCPConfig
{
    IP = "192.168.0.100",
    Porta = 9100
});
impressora.Protocolo = ProtocoloEscPos.EscPos;
impressora.PaginaCodigo = PaginaCodigo.pc850;
impressora.Colunas = 48; // 32 para papel estreito
impressora.Conectar();

var danfe = new OpenDANFeEscPos(new DANFeEscPosConfig
{
    MensagemRodape = "Obrigado pela preferência!",
    CortarPapel = true,
    LinhasAvanco = 5
});
var documento = NFeProc.Load("nfce-processada.xml");
await danfe.ImprimirAsync(documento, impressora, cancellationToken);
// Ou: danfe.Imprimir(documento, impressora);
```

A aplicação configura, conecta e descarta a impressora. O renderizador preserva sua conexão, protocolo, página de código e colunas. As operações deste renderizador que compartilham a mesma instância de impressora são serializadas, inclusive entre instâncias diferentes de `OpenDANFeEscPos`.

`ImprimirAsync` aguarda a fila de forma assíncrona e permite cancelar essa espera. O envio ao dispositivo utiliza a API síncrona do componente; um envio iniciado não é interrompido nem repetido automaticamente.

As sobrecargas recebem `NFe` ou `NFeProc` do modelo 65. O layout inclui emitente, itens, totais, pagamentos, consumidor, chave, protocolo, QR Code e informações adicionais. A configuração é copiada por operação e permite ocultar itens/nome fantasia, definir logotipo, cópias, módulo do QR Code, rodapé e corte. `Colunas` usa o valor do componente quando não informada; exportações usam 48 como padrão. `RenderizarCupom` é virtual para personalizações.

Para exportação e prévia sem impressora conectada:

```csharp
danfe.Configuracoes.Colunas = 32;
byte[] comandos = danfe.Gerar(documento, ProtocoloEscPos.EscPos, PaginaCodigo.pc850);
byte[] imagemPng = danfe.GerarPreview(documento);
string texto = danfe.GerarTexto(documento);
```

`Gerar` usa o próprio componente com um dispositivo em memória. `GerarPreview` usa a prévia PNG do componente. `PrepararCupom(documento, impressora)` adiciona o cupom ao buffer de uma impressora, inclusive desconectada, para composição ou chamada posterior a `impressora.GerarPreview()`.

O exemplo de console usa `EscPosPrinter<FileConfig>` para gravar o cupom em arquivo e também oferece o comando `escpos-preview`. Veja o README da raiz para os comandos executáveis. Os testes comparam exportação e dispositivo de arquivo; a validação em hardware depende da impressora utilizada.
