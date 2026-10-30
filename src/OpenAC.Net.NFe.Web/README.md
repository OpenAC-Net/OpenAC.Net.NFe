# OpenAC.Net.NFe.Web

DI e configuração/certificados por empresa com IHttpClientFactory, mTLS e métricas. API: AddOpenNFeWebMultiTenant, INFeConfigurationProvider, INFeCertificateProvider e IOpenNFeClientFactory. O transporte implementa `IDFeHttpTransport` do DFe.Core e deixa montagem e leitura do SOAP para os serviços da biblioteca principal. A factory usa `NFeConfig.Copiar()` e expõe `IOpenNFeClient` na sessão. Descarte `NFeSession` após o uso para liberar sua cópia do certificado.

Veja o README da raiz para exemplos completos, referências e limites da validação.
