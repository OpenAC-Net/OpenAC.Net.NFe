using System.IO;
using System.Reflection;
using NotaFiscal = OpenAC.Net.NFe.Common.Model.NFe;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Types;
using OpenAC.Net.NFe.Certificates;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using OpenAC.Net.DFe.Core;
using OpenAC.Net.DFe.Core.Common;
using OpenAC.Net.DFe.Core.Http;
using OpenAC.Net.DFe.Core.Storage;
using System.Threading.Tasks;

namespace OpenAC.Net.NFe.Tests;

public sealed class CoreIntegrationTests
{
    private const string Status = "<retConsStatServ xmlns='http://www.portalfiscal.inf.br/nfe'><cStat>107</cStat></retConsStatServ>";

    [Test]
    public async Task ClienteReutilizaConfiguracoesAlteradasAntesDaOperacaoComoNoCTe()
    {
        using var cert = Fixtures.Certificate();
        var transport = new CaptureTransport((_, _) => Task.FromResult(Reply(Status)));
        var config = Fixtures.Config(cert);
        config.HttpTransport = transport;
        config.WebServices.Enderecos[TipoServicoNFe.NFeStatusServico] = "https://empresa-a.test/status?wsdl";
        var client = new OpenNFe(config);
        config.WebServices.UF = DFeSiglaUF.MS;
        config.WebServices.Enderecos[TipoServicoNFe.NFeStatusServico] = "https://empresa-b.test/status";
        var response = await client.ConsultarSituacaoServicoAsync();
        await Assert.That((await Assert.That(transport.Requests).HasSingleItem()).Url.Host).IsEqualTo("empresa-b.test");
        await Assert.That(transport.Requests.Single().Url.Query).IsEqualTo("");
        await Assert.That(response.XmlEnvio).Contains("<cUF>50</cUF>");
        await Assert.That(response.EnvelopeEnvio).Contains("soapenv:Envelope");
        await Assert.That(response.EnvelopeRetorno).Contains("retConsStatServ");
        await Assert.That(ReferenceEquals(config, client.Configuracoes)).IsTrue();
        client.Configuracoes.Geral.VersaoDFe = NFeVersao.v310;
        await Assert.That(() => client.ConsultarSituacaoServicoAsync()).ThrowsExactly<NotSupportedException>();
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
    }

    [Test]
    public async Task OperacaoEmAndamentoUsaCopiaMasProximaChamadaUsaNovasConfiguracoes()
    {
        using var certificate = Fixtures.Certificate();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new CaptureTransport(async (request, token) =>
        {
            if (request.Url.Host == "empresa-a.test")
            {
                started.TrySetResult(true);
                await release.Task.WaitAsync(token);
            }
            return Reply(Status);
        });
        var config = Fixtures.Config(certificate);
        config.HttpTransport = transport;
        config.WebServices.Enderecos[TipoServicoNFe.NFeStatusServico] = "https://empresa-a.test/status";
        var client = new OpenNFe(config);
        var first = client.ConsultarSituacaoServicoAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        config.WebServices.UF = DFeSiglaUF.MS;
        config.WebServices.Enderecos[TipoServicoNFe.NFeStatusServico] = "https://empresa-b.test/status";
        release.TrySetResult(true);
        var firstResponse = await first;
        var secondResponse = await client.ConsultarSituacaoServicoAsync();
        await Assert.That(firstResponse.XmlEnvio).Contains("<cUF>35</cUF>");
        await Assert.That(secondResponse.XmlEnvio).Contains("<cUF>50</cUF>");
        await Assert.That(transport.Requests.Any(request => request.Url.Host == "empresa-a.test")).IsTrue();
        await Assert.That(transport.Requests.Any(request => request.Url.Host == "empresa-b.test")).IsTrue();
    }

    [Test]
    public async Task CertificadoAtualizadoEntreChamadasEhUsadoPelaMesmaInstancia()
    {
        using var first = Fixtures.Certificate();
        using var second = Fixtures.Certificate();
        var transport = new CaptureTransport((_, _) => Task.FromResult(Reply(Status)));
        var client = new OpenNFe();
        client.Configuracoes.HttpTransport = transport;
        client.Configuracoes.Certificado = first;
        await client.ConsultarSituacaoServicoAsync();
        client.Configuracoes.Certificado = second;
        await client.ConsultarSituacaoServicoAsync();
        await Assert.That(transport.Requests.Any(request => request.Thumbprint == first.Thumbprint)).IsTrue();
        await Assert.That(transport.Requests.Any(request => request.Thumbprint == second.Thumbprint)).IsTrue();
    }

    [Test]
    public async Task AutorizacaoNaoModificaDocumentoOriginalESalvaPeloStoreDoCore()
    {
        using var cert = Fixtures.Certificate();
        var nota = Fixtures.Nota();
        var original = nota.GetXml();
        var store = new RecordingStore();
        var config = Fixtures.Config(cert);
        config.DocumentStore = store;
        config.Arquivos.PathSalvar = Path.Combine(Path.GetTempPath(), "nfe-store-" + Guid.NewGuid().ToString("N"));
        config.Arquivos.Salvar = true;
        config.Arquivos.SepararPorCNPJ = true;
        config.Arquivos.SepararPorModelo = true;
        config.WebServices.Salvar = true;
        config.HttpTransport = new CaptureTransport(async (request, _) =>
        {
            var enviada = NotaFiscal.Load(request.Xml.Descendants(XmlFixture.Ns + "NFe").Single().ToString(SaveOptions.DisableFormatting));
            await Assert.That(enviada.ValidarAssinatura()).IsTrue();
            return Reply(new XElement(XmlFixture.Ns + "retEnviNFe", new XElement(XmlFixture.Ns + "cStat", "104"), Fixtures.Protocolo(enviada).WriteToXml()).ToString(SaveOptions.DisableFormatting));
        });
        var result = await new OpenNFe(config).EnviarAsync(new NFeEnvioSolicitacao { Documentos = [nota] });
        await Assert.That(nota.GetXml()).IsEqualTo(original);
        await Assert.That(nota.Assinado).IsFalse();
        await Assert.That((await Assert.That(result.Documentos).HasSingleItem()).Autorizado).IsTrue();
        await Assert.That(result.Documentos[0].ValidarAssinatura()).IsTrue();
        await Assert.That(store.Documents.Count).IsEqualTo(3);
        await Assert.That(store.Documents).Contains(doc => doc.Caminho.EndsWith(nota.Chave + "-procNFe.xml") && doc.Caminho.Contains(Path.Combine("12345678000195", "NFe")));
        await Assert.That(Directory.Exists(config.Arquivos.PathSalvar)).IsFalse();
        await Assert.That(config.Arquivos.OrdenacaoPath).IsEmpty();
    }

    [Test]
    public async Task MesmoClienteExecutaChamadasConcorrentesSemEstadoCompartilhado()
    {
        using var cert = Fixtures.Certificate();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        var transport = new CaptureTransport(async (_, token) =>
        {
            if (Interlocked.Increment(ref count) == 2) entered.TrySetResult();
            await entered.Task.WaitAsync(token);
            return Reply(Status);
        });
        var config = Fixtures.Config(cert);
        config.HttpTransport = transport;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var client = new OpenNFe(config);
        var results = await Task.WhenAll(client.ConsultarSituacaoServicoAsync(deadline.Token), client.ConsultarSituacaoServicoAsync(deadline.Token));
        foreach (var result in results)
            await Assert.That(result.CStat).IsEqualTo(107);
        await Assert.That(transport.Requests.Count).IsEqualTo(2);
        await Assert.That(transport.Requests.Last().Certificate).IsNotSameReferenceAs(transport.Requests.First().Certificate);
        await Assert.That(cert.HasPrivateKey).IsTrue();
    }

    [Test]
    public async Task CancelamentoAntecipadoNaoAbreCertificadoNemTransporte()
    {
        var provider = new CountingCertificates();
        var transport = new CaptureTransport((_, _) => Task.FromResult(Reply(Status)));
        var config = new NFeConfig { HttpTransport = transport };
        var client = new OpenNFe(config, provider);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Assert.That(() => client.ConsultarSituacaoServicoAsync(canceled.Token)).Throws<OperationCanceledException>();
        await Assert.That(provider.Count).IsEqualTo(0);
        await Assert.That(transport.Requests).IsEmpty();
    }

    [Test]
    public async Task TimeoutCancelaTransporteERetornaSemRepetirPost()
    {
        using var cert = Fixtures.Certificate();
        var transport = new CaptureTransport(async (_, token) => { await Task.Delay(Timeout.Infinite, token); return Reply(Status); });
        var config = Fixtures.Config(cert);
        config.HttpTransport = transport;
        config.WebServices.Tentativas = 5;
        config.WebServices.TimeoutOperacao = TimeSpan.FromMilliseconds(70);
        await Assert.That(() => new OpenNFe(config).ConsultarSituacaoServicoAsync()).Throws<OperationCanceledException>();
        await Assert.That(transport.Requests).HasSingleItem();
    }

    [Test]
    [Arguments(HttpStatusCode.OK)]
    [Arguments(HttpStatusCode.InternalServerError)]
    public async Task SoapFaultPassaPeloTratamentoDoCore(HttpStatusCode status)
    {
        using var cert = Fixtures.Certificate();
        var transport = new CaptureTransport((_, _) => Task.FromResult(Reply("<s:Fault xmlns:s='http://www.w3.org/2003/05/soap-envelope'><s:Code><s:Value>s:Receiver</s:Value></s:Code><s:Reason><s:Text>Falha fiscal simulada</s:Text></s:Reason></s:Fault>", status)));
        var config = Fixtures.Config(cert); config.HttpTransport = transport;
        var error = await Assert.That(() => new OpenNFe(config).ConsultarSituacaoServicoAsync()).ThrowsExactly<OpenDFeCommunicationException>();
        await Assert.That(error.Message).Contains("Falha fiscal simulada");
        await Assert.That(transport.Requests).HasSingleItem();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CertificadoConfiguradoEmPfxFuncionaSemInstanciaEmprestada(bool arquivo)
    {
        using var cert = Fixtures.Certificate();
        var pfx = cert.Export(X509ContentType.Pfx, "test");
        var path = Path.Combine(Path.GetTempPath(), "nfe-cert-" + Guid.NewGuid().ToString("N") + ".pfx");
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var config = new NFeConfig();
        config.HttpTransport = new CaptureTransport(async (_, token) =>
        {
            started.TrySetResult(true);
            await release.Task.WaitAsync(token);
            return Reply(Status);
        });
        config.Certificados.Senha = "test";
        if (arquivo) { await File.WriteAllBytesAsync(path, pfx); config.Certificados.Certificado = path; }
        else config.Certificados.CertificadoBytes = pfx;
        try
        {
            var client = new OpenNFe(config);
            var operation = client.ConsultarSituacaoServicoAsync();
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (!arquivo) Array.Clear(pfx);
            release.TrySetResult(true);
            await Assert.That((await operation).CStat).IsEqualTo(107);
        }
        finally { release.TrySetResult(true); if (File.Exists(path)) File.Delete(path); }
    }

    [Test]
    public async Task ReciboAssociaSomenteDocumentosExplicitos()
    {
        using var cert = Fixtures.Certificate();
        var nota = Fixtures.Nota(); nota.Assinar(cert);
        var config = Fixtures.Config(cert);
        config.HttpTransport = new CaptureTransport((_, _) => Task.FromResult(Reply(new XElement(XmlFixture.Ns + "retConsReciNFe", new XElement(XmlFixture.Ns + "cStat", "104"), Fixtures.Protocolo(nota).WriteToXml()).ToString(SaveOptions.DisableFormatting))));
        var client = new OpenNFe(config);
        var result = await client.ConsultaReciboAsync(new NFeReciboSolicitacao { Recibo = "351260000000001", Documentos =
            [nota]
        });
        await Assert.That((await Assert.That(result.Documentos).HasSingleItem()).Autorizado).IsTrue();
        await Assert.That(nota.ValidarAssinatura()).IsTrue();
        await Assert.That((await client.ConsultaReciboAsync(new NFeReciboSolicitacao { Recibo = "351260000000001" })).Documentos).IsEmpty();
    }

    [Test]
    public async Task LoteAssincronoRetornaCopiasAssinadasParaAssociacaoDoRecibo()
    {
        using var certificate = Fixtures.Certificate();
        var sourceXml = XmlFixture.Parse(Fixtures.Nota().GetXml());
        sourceXml.Element(XmlFixture.Ns + "infNFe")!.SetAttributeValue("Id", "");
        var original = NotaFiscal.Load(sourceXml.ToString());
        var otherXml = new XElement(sourceXml);
        otherXml.Descendants(XmlFixture.Ns + "nNF").Single().Value = "2";
        var other = NotaFiscal.Load(otherXml.ToString());
        var phase = 0;
        NotaFiscal[] sent = [];
        var config = Fixtures.Config(certificate);
        config.HttpTransport = new CaptureTransport(async (request, _) =>
        {
            if (Interlocked.Increment(ref phase) == 1)
            {
                await Assert.That(request.Xml.Descendants(XmlFixture.Ns + "indSinc").Single().Value).IsEqualTo("0");
                sent =
                [
                    .. request.Xml.Descendants(XmlFixture.Ns + "NFe").Select(xml =>
                        NotaFiscal.Load(xml.ToString(SaveOptions.DisableFormatting)))
                ];
                return Reply("<retEnviNFe xmlns='http://www.portalfiscal.inf.br/nfe'><cStat>103</cStat><infRec><nRec>351260000000001</nRec></infRec></retEnviNFe>");
            }
            return Reply(new XElement(XmlFixture.Ns + "retConsReciNFe", new XElement(XmlFixture.Ns + "cStat", "104"), sent.Select(note => Fixtures.Protocolo(note).WriteToXml())).ToString(SaveOptions.DisableFormatting));
        });
        var client = new OpenNFe(config);
        var envio = await client.EnviarAsync(new NFeEnvioSolicitacao { Lote = "42", Sincrono = false, Documentos =
            [original, other]
        });
        await Assert.That(envio.Documentos).IsEmpty();
        await Assert.That(envio.DocumentosEnviados.Count).IsEqualTo(2);
        await Assert.That(envio.DocumentosEnviados).All(note => note.ValidarAssinatura());
        await Assert.That(original.Assinado).IsFalse(); await Assert.That(other.Assinado).IsFalse();
        var retorno = await client.ConsultaReciboAsync(new NFeReciboSolicitacao { Recibo = envio.Recibo, Documentos = envio.DocumentosEnviados });
        await Assert.That(retorno.Documentos.Count).IsEqualTo(2);
        foreach (var note in retorno.Documentos)
        {
            await Assert.That(note.Autorizado).IsTrue();
            await Assert.That(note.ValidarAssinatura()).IsTrue();
        }
    }

    [Test]
    public async Task ModeloTipadoPreservaQrCodeComoTexto()
    {
        var note = NotaFiscal.Load(Fixtures.Nota(65).GetXml());
        note.InfNFeSupl = new NFeSupl { QrCode = "https://consulta.test/qrcode?p=chave|3|2", UrlChave = "https://consulta.test" };
        var reloaded = NotaFiscal.Load(note.GetXml());
        await Assert.That(reloaded.InfNFeSupl.QrCode).IsEqualTo(note.InfNFeSupl.QrCode);
    }

    internal static HttpResponseMessage Reply(string payload, HttpStatusCode status = HttpStatusCode.OK) => new(status)
    { Content = new StringContent("<s:Envelope xmlns:s='http://www.w3.org/2003/05/soap-envelope'><s:Body>" + payload + "</s:Body></s:Envelope>", Encoding.UTF8, "application/soap+xml") };

    private sealed class CountingCertificates : INFeCertificadoProvider
    {
        internal int Count;
        public Task<X509Certificate2> ObterAsync(NFeConfigCertificados config, CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref Count); return Task.FromResult(Fixtures.Certificate()); }
    }

    private sealed class RecordingStore : IDFeDocumentStore
    {
        internal ConcurrentBag<DFeDocument> Documents = [];
        public Task SaveAsync(IDFeDocument document, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); Documents.Add(new DFeDocument(document.Caminho, document.Conteudo, document.ArquivoExiste)); return Task.CompletedTask; }
    }

    internal sealed record Request(Uri Url, XElement Xml, X509Certificate2 Certificate)
    {
        internal string Thumbprint { get; } = Certificate.Thumbprint;
    }
    internal sealed class CaptureTransport(Func<Request, CancellationToken, Task<HttpResponseMessage>> send) : IDFeHttpTransport
    {
        internal ConcurrentBag<Request> Requests = [];
        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, DFeHttpTransportOptions options, CancellationToken cancellationToken = default)
        {
            await Assert.That(options.AllowAutoRedirect).IsFalse();
            await Assert.That(options.UseCookies).IsFalse();
            await Assert.That(options.AutomaticDecompression).IsEqualTo(DecompressionMethods.None);
            var captured = new Request(request.RequestUri!, XmlFixture.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)), options.Certificate!);
            Requests.Add(captured);
            return await send(captured, cancellationToken);
        }
    }
}

/// <summary>Confere os contratos do assembly contra os XSDs compilados, sem depender de um gerador.</summary>
public sealed class SchemaCoverageTests
{
    private sealed record Binding(System.Reflection.PropertyInfo Property, Type Type, int Order);

    [Test]
    [Arguments("3.10")]
    [Arguments("4.00")]
    public async Task TodosOsCamposDosDoisLeiautesPossuemContratoESaoSerializados(string version)
    {
        var schemas = new System.Xml.Schema.XmlSchemaSet { XmlResolver = new System.Xml.XmlUrlResolver() };
        schemas.Add(null, Path.Combine(AppContext.BaseDirectory, "Schemas", "nfe_v" + version + ".xsd"));
        schemas.Compile();
        var root = (System.Xml.Schema.XmlSchemaElement)schemas.GlobalElements[
            new System.Xml.XmlQualifiedName("NFe", "http://www.portalfiscal.inf.br/nfe")]!;
        var elements = new HashSet<string>();
        var attributes = new HashSet<string>();
        var groups = new HashSet<string>();
        var enumValues = new HashSet<string>();
        var orders = new HashSet<string>();
        var errors = new HashSet<string>();
        var serializationChecks = new HashSet<string>();
        var schemaVersion = version == "3.10" ? NFeVersao.v310 : NFeVersao.v400;

        IEnumerable<string> Codes(System.Xml.Schema.XmlSchemaSimpleType type, HashSet<System.Xml.Schema.XmlSchemaType> visited)
        {
            if (!visited.Add(type)) yield break;
            if (type.Content is System.Xml.Schema.XmlSchemaSimpleTypeRestriction restriction)
                foreach (var facet in restriction.Facets.OfType<System.Xml.Schema.XmlSchemaEnumerationFacet>())
                    yield return facet.Value!;
            if (type.Content is System.Xml.Schema.XmlSchemaSimpleTypeUnion union && union.BaseMemberTypes != null)
                foreach (var member in union.BaseMemberTypes)
                    foreach (var code in Codes(member, visited)) yield return code;
            if (type.BaseXmlSchemaType is System.Xml.Schema.XmlSchemaSimpleType parent)
                foreach (var code in Codes(parent, visited)) yield return code;
        }

        void CheckEnum(System.Xml.Schema.XmlSchemaSimpleType type, Type modelType, string path)
        {
            modelType = Nullable.GetUnderlyingType(modelType) ?? modelType;
            if (!modelType.IsEnum) return;
            var available = modelType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Select(member => member.GetCustomAttribute<OpenAC.Net.DFe.Core.Attributes.DFeEnumAttribute>()?.Value ?? member.Name).ToHashSet();
            foreach (var code in Codes(type, new()))
            {
                enumValues.Add(path + "=" + code);
                if (!available.Contains(code)) errors.Add(path + ": código " + code + " ausente em " + modelType.Name);
            }
        }

        Dictionary<string, Binding> Bindings(Type type, bool attribute)
        {
            var result = new Dictionary<string, Binding>();
            foreach (var property in type.GetProperties())
            {
                if (property.GetCustomAttribute<OpenAC.Net.DFe.Core.Attributes.DFeIgnoreAttribute>() != null) continue;
                if (attribute)
                {
                    var map = property.GetCustomAttribute<OpenAC.Net.DFe.Core.Attributes.DFeAttributeAttribute>();
                    if (map != null) result.Add(map.Name, new(property, property.PropertyType, map.Ordem));
                    continue;
                }
                var element = property.GetCustomAttribute<OpenAC.Net.DFe.Core.Attributes.DFeElementAttribute>();
                var collection = property.GetCustomAttribute<OpenAC.Net.DFe.Core.Attributes.DFeCollectionAttribute>();
                if (element != null) result.Add(element.Name, new(property, property.PropertyType, element.Ordem));
                if (collection != null) result.Add(collection.Name, new(property, property.PropertyType.GetGenericArguments()[0], collection.Ordem));
                foreach (var item in property.GetCustomAttributes<OpenAC.Net.DFe.Core.Attributes.DFeItemAttribute>())
                    result.Add(item.Name, new(property, item.Tipo, 0));
            }
            return result;
        }

        IEnumerable<System.Xml.Schema.XmlSchemaElement> Children(System.Xml.Schema.XmlSchemaParticle particle)
        {
            if (particle is System.Xml.Schema.XmlSchemaElement element)
            {
                // A assinatura XMLDSig é implementada pelo Core e exercitada nos testes de assinatura.
                if (element.QualifiedName.Namespace != "http://www.w3.org/2000/09/xmldsig#") yield return element;
            }
            else if (particle is System.Xml.Schema.XmlSchemaGroupBase group)
                foreach (var child in group.Items.OfType<System.Xml.Schema.XmlSchemaParticle>())
                    foreach (var descendant in Children(child)) yield return descendant;
            else if (particle is System.Xml.Schema.XmlSchemaGroupRef reference && reference.Particle != null)
                foreach (var descendant in Children(reference.Particle)) yield return descendant;
        }

        void CheckOrder(System.Xml.Schema.XmlSchemaParticle particle, Dictionary<string, Binding> bindings, string path)
        {
            if (particle is System.Xml.Schema.XmlSchemaGroupRef reference && reference.Particle != null)
            {
                CheckOrder(reference.Particle, bindings, path);
                return;
            }
            if (particle is not System.Xml.Schema.XmlSchemaGroupBase group) return;
            var children = group.Items.OfType<System.Xml.Schema.XmlSchemaParticle>().ToArray();
            foreach (var child in children) CheckOrder(child, bindings, path);
            if (particle is not System.Xml.Schema.XmlSchemaSequence) return;
            for (var first = 0; first < children.Length; first++)
                for (var second = first + 1; second < children.Length; second++)
                    foreach (var left in Children(children[first]))
                        foreach (var right in Children(children[second]))
                        {
                            if (left.Name == right.Name || !bindings.TryGetValue(left.Name!, out var a) || !bindings.TryGetValue(right.Name!, out var b)) continue;
                            var pair = path + "/" + left.Name + " < " + right.Name;
                            orders.Add(pair);
                            if (a.Order >= b.Order) errors.Add(pair + $": ordem incompatível ({a.Order}, {b.Order})");
                        }
        }

        XElement Serialize(Type owner, string name, string value, bool attribute)
        {
            var instance = Activator.CreateInstance(owner)!;
            var input = new XElement("probe", attribute ? (object)new XAttribute(name, value) : new XElement(name, value));
            owner.GetMethod("ReadXml")!.Invoke(instance, new object[] { input, null! });
            if (instance is NFeInfNFe info) info.Versao = schemaVersion;
            else owner.GetProperty("Parent")?.SetValue(instance, new NFeInfNFe
            {
                Versao = schemaVersion,
                Ide = new NFeIde { Mod = ModeloNFe.NFe, TpEmis = TipoEmissao.OffLine }
            });
            if (instance is NFeIde ide)
            {
                ide.Mod = ModeloNFe.NFe;
                ide.TpEmis = TipoEmissao.OffLine;
            }
            return (XElement)owner.GetMethod("WriteToXml")!.Invoke(instance, new object[] { "probe", "", null! })!;
        }

        void CheckSerialization(Type owner, Binding binding, System.Xml.Schema.XmlSchemaSimpleType type, string name, string path, bool attribute)
        {
            if (!serializationChecks.Add(path)) return;
            var target = Nullable.GetUnderlyingType(binding.Type) ?? binding.Type;
            var sample = Codes(type, new()).FirstOrDefault() ?? (target == typeof(DateTime) ? "2026-10-28" :
                target == typeof(DateTimeOffset) ? "2026-10-28T10:00:00-04:00" : target == typeof(string) ? "VALUE" : "1");
            if (target.IsEnum && !Codes(type, new()).Any())
                sample = target.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    .Select(member => member.GetCustomAttribute<OpenAC.Net.DFe.Core.Attributes.DFeEnumAttribute>()?.Value ?? member.Name).First();
            if (name == "versao" && owner == typeof(NFeInfNFe)) sample = version;
            var xml = Serialize(owner, name, sample, attribute);
            if (attribute ? xml.Attribute(name) == null : xml.Element(name) == null)
                errors.Add(path + ": campo permitido pelo XSD foi descartado na serialização");
        }

        void Walk(System.Xml.Schema.XmlSchemaElement element, Type modelType, string path)
        {
            elements.Add(path);
            if (element.ElementSchemaType is System.Xml.Schema.XmlSchemaSimpleType simple)
            {
                CheckEnum(simple, modelType, path);
                return;
            }
            if (element.ElementSchemaType is not System.Xml.Schema.XmlSchemaComplexType complex) return;
            groups.Add(path);
            var bindings = Bindings(modelType, false);
            var attrBindings = Bindings(modelType, true);
            foreach (System.Xml.Schema.XmlSchemaAttribute attribute in complex.AttributeUses.Values)
            {
                var attrPath = path + "/@" + attribute.Name;
                attributes.Add(attrPath);
                if (!attrBindings.TryGetValue(attribute.Name!, out var binding)) errors.Add(attrPath + ": atributo ausente em " + modelType.Name);
                else
                {
                    CheckEnum(attribute.AttributeSchemaType!, binding.Type, attrPath);
                    CheckSerialization(modelType, binding, attribute.AttributeSchemaType!, attribute.Name!, attrPath, true);
                }
            }
            CheckOrder(complex.ContentTypeParticle, bindings, path);
            var children = Children(complex.ContentTypeParticle).ToArray();
            foreach (var child in children)
            {
                var childPath = path + "/" + child.Name;
                if (!bindings.TryGetValue(child.Name!, out var binding)) errors.Add(childPath + ": elemento ausente em " + modelType.Name);
                else
                {
                    var isCollection = binding.Property.PropertyType != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(binding.Property.PropertyType);
                    if (child.MaxOccurs > 1 && !isCollection) errors.Add(childPath + ": repetição não representada por coleção");
                    Walk(child, binding.Type, childPath);
                    if (child.ElementSchemaType is System.Xml.Schema.XmlSchemaSimpleType simpleType)
                        CheckSerialization(modelType, binding, simpleType, child.Name!, childPath, false);
                }
            }
            foreach (var field in bindings.Keys.Except(children.Select(child => child.Name)))
            {
                if (modelType == typeof(NotaFiscal) && field == "Signature") continue;
                var check = path + "/" + field + " (proibido em " + version + ")";
                if (!serializationChecks.Add(check)) continue;
                if (Serialize(modelType, field, "", false).Element(field) != null)
                    errors.Add(check + ": campo exclusivo de outro leiaute foi serializado");
            }
        }

        Walk(root, typeof(NotaFiscal), "NFe");
        var report = new
        {
            version, elements = elements.Count, attributes = attributes.Count, groups = groups.Count,
            enumValues = enumValues.Count, orderConstraints = orders.Count, serializationChecks = serializationChecks.Count,
            errors = errors.OrderBy(error => error).ToArray()
        };
        var json = System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "schema-coverage-" + version + ".json"), json);
        Console.WriteLine(json);
        await Assert.That(errors).IsEmpty();
    }
}