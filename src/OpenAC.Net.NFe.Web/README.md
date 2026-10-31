# OpenAC.Net.NFe.Web

Integração ASP.NET Core com IHttpClientFactory, isolamento por empresa, cancelamento e observabilidade.
O exemplo de uso está no [README principal](../../README.md).

`AddOpenNFe` registra uma configuração fixa. `AddOpenNFeMultiTenant` recebe implementações de
`INFeConfigurationProvider<NFeConfig>` e `INFeCertificateProvider` para resolver os dados da empresa.
`IOpenNFeClientFactory.CriarAsync` devolve `IOpenNFeClient`. A fábrica captura uma cópia da configuração
antes de aplicar certificado, transporte, timeout e diretório da empresa.

`INFeCertificadoProvider` (`OpenAC.Net.NFe.Certificates`) fornece certificados por operação e pode ser substituído na injeção de
dependências. Cada certificado retornado pertence à chamada e é descartado ao terminar. O provider
padrão lê PFX de arquivo assincronamente. As configurações e documentos recebidos pelo cliente
permanecem isolados das alterações feitas durante uma operação.

O pool HTTP usa empresa, certificado, protocolo TLS e autoridade do endpoint como identidade.
Redirecionamentos e cookies são desabilitados. Os certificados mantidos no pool pertencem ao host.
Os IDs de empresa devem ser resolvidos e autorizados pela aplicação.

A persistência respeita `Arquivos.Salvar` e `WebServices.Salvar` da configuração da empresa,
independentemente. O Web acrescenta o hash do ID da empresa ao `Arquivos.PathSalvar` configurado;
os caminhos específicos de documentos, quando disponíveis, também são isolados. A configuração
original do provider permanece intacta. Registre um `IDFeDocumentStore` antes da integração para usar outro armazenamento.
O store local grava em arquivo temporário e faz substituição atômica.

`WebServices.TimeoutOperacao` limita cada chamada SOAP, incluindo persistência dos envelopes e leitura da resposta.
O CancellationToken da aplicação também alcança a obtenção do certificado e as operações de storage.
POST fiscal não recebe retentativas automáticas. Rejeições fiscais ficam no resultado;
falhas de transporte geram `NFeTechnicalException`. Cancelamentos do chamador são preservados.

ActivitySource e Meter: `OpenAC.Net.NFe.Web`. Métricas: `openac.nfe.requests` e
`openac.nfe.request.duration`. Payloads fiscais e senhas não são incluídos nos logs da integração.
O circuit breaker é opcional e isolado pelo pool. `WebServices.TimeoutTentativa` limita a tentativa HTTP.

O pacote mantém os alvos .NET Framework 4.6.2/4.8, .NET Standard 2.0 e .NET 8/9/10.
No .NET 8 ou superior, o pool usa `SocketsHttpHandler`; nos demais alvos, usa `HttpClientHandler`
com GZip/Deflate. A renovação do handler e a resiliência usam as mesmas opções em todos os alvos;
`TempoVidaConexaoPool` aplica-se ao `SocketsHttpHandler`.

Para uma configuração fixa, preencha `Certificados.CertificadoBytes` e `Senha`, ou informe o caminho
PFX em `Certificados.Certificado`. Na integração Web, `INFeCertificateProvider.ConfigureAsync` configura
essas opções por empresa; `INFeCertificadoProvider.ObterAsync` lê e entrega uma instância própria por
operação. A factory retorna o cliente diretamente, sem uma sessão intermediária.

## Configuração compartilhada no DFe.Core

As políticas são definidas na configuração da empresa: `WebServices.TimeoutOperacao` (100 segundos,
mesmo valor de `TimeOut`), `TimeoutTentativa` (60 segundos), `TempoVidaHandler` (5 minutos),
`TempoVidaConexaoPool` (2 minutos) e `CircuitBreakerHabilitado` (false). A margem fica em
`Certificados.MargemValidadeCertificado` (5 minutos). Os transportes padrão e Web usam o mesmo
pipeline de timeout e circuit breaker do Core; POST fiscal não é repetido automaticamente.

`Arquivos.Salvar` e `WebServices.Salvar` controlam documentos e mensagens técnicas separadamente.
O Web preserva os diretórios definidos na configuração e acrescenta o hash da empresa a cada raiz.
As políticas do handler participam da identidade do pool, para não compartilhar configurações diferentes.
