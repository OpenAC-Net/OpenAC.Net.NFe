# OpenAC.Net.NFe.Demo

Execute dotnet run --project OpenAC.Net.NFe.Demo para ver os comandos. Aceita XML NF-e, nfeProc, evento ou procEventoNFe. Gera PDF ou arquivo ESC/POS; não transmite à SEFAZ.

Veja o README da raiz para exemplos completos, referências e limites da validação.

Para gerar uma prévia PNG da NFC-e sem conectar uma impressora:

```sh
dotnet run --project src/OpenAC.Net.NFe.Demo -- escpos-preview samples/nfe-65-4.00.xml /tmp/nfce.png 32
```
