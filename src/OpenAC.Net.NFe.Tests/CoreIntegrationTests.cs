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
using Xunit;

namespace OpenAC.Net.NFe.Tests;

public sealed class CoreIntegrationTests
{
    private const string Status = "<retConsStatServ xmlns='http://www.portalfiscal.inf.br/nfe'><cStat>107</cStat></retConsStatServ>";

    [Fact]
    public async Task ClienteIsolaConfiguracaoEEndpointsDoChamador()
    {
        using var cert = Fixtures.Certificate();
        var transport = new CaptureTransport((_, _) => Task.FromResult(Reply(Status)));
        var config = Fixtures.Config(cert);
        config.HttpTransport = transport;
        config.WebServices.Enderecos[TipoServicoNFe.NFeStatusServico] = "https://empresa-a.test/status?wsdl";
        var client = new OpenNFe(config);
        config.WebServices.UF = DFeSiglaUF.MS;
        config.WebServices.Enderecos[TipoServicoNFe.NFeStatusServico] = "https://empresa-b.test/status";
        config.Geral.VersaoDFe = NFeVersao.v310;
        var response = await client.ConsultarSituacaoServicoAsync();
        Assert.Equal("empresa-a.test", Assert.Single(transport.Requests).Url.Host);
        Assert.Equal("", transport.Requests.Single().Url.Query);
        Assert.Contains("<cUF>35</cUF>", response.XmlEnvio);
        Assert.Contains("soapenv:Envelope", response.EnvelopeEnvio);
        Assert.Contains("retConsStatServ", response.EnvelopeRetorno);
    }

    [Fact]
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
        config.HttpTransport = new CaptureTransport((request, _) =>
        {
            var enviada = NotaFiscal.Load(request.Xml.Descendants(XmlFixture.Ns + "NFe").Single().ToString(SaveOptions.DisableFormatting));
            Assert.True(enviada.ValidarAssinatura());
            return Task.FromResult(Reply(new XElement(XmlFixture.Ns + "retEnviNFe", new XElement(XmlFixture.Ns + "cStat", "104"), Fixtures.Protocolo(enviada).WriteToXml()).ToString(SaveOptions.DisableFormatting)));
        });
        var result = await new OpenNFe(config).EnviarAsync(new NFeEnvioSolicitacao { Documentos = [nota] });
        Assert.Equal(original, nota.GetXml());
        Assert.False(nota.Assinado);
        Assert.True(Assert.Single(result.Documentos).Autorizado);
        Assert.True(result.Documentos[0].ValidarAssinatura());
        Assert.Equal(3, store.Documents.Count);
        Assert.Contains(store.Documents, doc => doc.Caminho.EndsWith(nota.Chave + "-procNFe.xml") && doc.Caminho.Contains(Path.Combine("12345678000195", "NFe")));
        Assert.False(Directory.Exists(config.Arquivos.PathSalvar));
        Assert.Empty(config.Arquivos.OrdenacaoPath);
    }

    [Fact]
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
        Assert.All(results, result => Assert.Equal(107, result.CStat));
        Assert.Equal(2, transport.Requests.Count);
        Assert.NotSame(transport.Requests.First().Certificate, transport.Requests.Last().Certificate);
        Assert.True(cert.HasPrivateKey);
    }

    [Fact]
    public async Task CancelamentoAntecipadoNaoAbreCertificadoNemTransporte()
    {
        var provider = new CountingCertificates();
        var transport = new CaptureTransport((_, _) => Task.FromResult(Reply(Status)));
        var config = new NFeConfig { HttpTransport = transport };
        var client = new OpenNFe(config, provider);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ConsultarSituacaoServicoAsync(canceled.Token));
        Assert.Equal(0, provider.Count);
        Assert.Empty(transport.Requests);
    }

    [Fact]
    public async Task TimeoutCancelaTransporteERetornaSemRepetirPost()
    {
        using var cert = Fixtures.Certificate();
        var transport = new CaptureTransport(async (_, token) => { await Task.Delay(Timeout.Infinite, token); return Reply(Status); });
        var config = Fixtures.Config(cert);
        config.HttpTransport = transport;
        config.WebServices.Tentativas = 5;
        config.WebServices.TimeoutOperacao = TimeSpan.FromMilliseconds(70);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new OpenNFe(config).ConsultarSituacaoServicoAsync());
        Assert.Single(transport.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task SoapFaultPassaPeloTratamentoDoCore(HttpStatusCode status)
    {
        using var cert = Fixtures.Certificate();
        var transport = new CaptureTransport((_, _) => Task.FromResult(Reply("<s:Fault xmlns:s='http://www.w3.org/2003/05/soap-envelope'><s:Code><s:Value>s:Receiver</s:Value></s:Code><s:Reason><s:Text>Falha fiscal simulada</s:Text></s:Reason></s:Fault>", status)));
        var config = Fixtures.Config(cert); config.HttpTransport = transport;
        var error = await Assert.ThrowsAsync<OpenDFeCommunicationException>(() => new OpenNFe(config).ConsultarSituacaoServicoAsync());
        Assert.Contains("Falha fiscal simulada", error.Message);
        Assert.Single(transport.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CertificadoConfiguradoEmPfxFuncionaSemInstanciaEmprestada(bool arquivo)
    {
        using var cert = Fixtures.Certificate();
        var pfx = cert.Export(X509ContentType.Pfx, "test");
        var path = Path.Combine(Path.GetTempPath(), "nfe-cert-" + Guid.NewGuid().ToString("N") + ".pfx");
        var config = new NFeConfig();
        config.HttpTransport = new CaptureTransport((_, _) => Task.FromResult(Reply(Status)));
        config.Certificados.Senha = "test";
        if (arquivo) { await File.WriteAllBytesAsync(path, pfx); config.Certificados.Certificado = path; }
        else config.Certificados.CertificadoBytes = pfx;
        try
        {
            var client = new OpenNFe(config);
            if (!arquivo) Array.Clear(pfx);
            Assert.Equal(107, (await client.ConsultarSituacaoServicoAsync()).CStat);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
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
        Assert.True(Assert.Single(result.Documentos).Autorizado);
        Assert.True(nota.ValidarAssinatura());
        Assert.Empty((await client.ConsultaReciboAsync(new NFeReciboSolicitacao { Recibo = "351260000000001" })).Documentos);
    }

    [Fact]
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
        config.HttpTransport = new CaptureTransport((request, _) =>
        {
            if (Interlocked.Increment(ref phase) == 1)
            {
                Assert.Equal("0", request.Xml.Descendants(XmlFixture.Ns + "indSinc").Single().Value);
                sent =
                [
                    .. request.Xml.Descendants(XmlFixture.Ns + "NFe").Select(xml =>
                        NotaFiscal.Load(xml.ToString(SaveOptions.DisableFormatting)))
                ];
                return Task.FromResult(Reply("<retEnviNFe xmlns='http://www.portalfiscal.inf.br/nfe'><cStat>103</cStat><infRec><nRec>351260000000001</nRec></infRec></retEnviNFe>"));
            }
            return Task.FromResult(Reply(new XElement(XmlFixture.Ns + "retConsReciNFe", new XElement(XmlFixture.Ns + "cStat", "104"), sent.Select(note => Fixtures.Protocolo(note).WriteToXml())).ToString(SaveOptions.DisableFormatting)));
        });
        var client = new OpenNFe(config);
        var envio = await client.EnviarAsync(new NFeEnvioSolicitacao { Lote = "42", Sincrono = false, Documentos =
            [original, other]
        });
        Assert.Empty(envio.Documentos);
        Assert.Equal(2, envio.DocumentosEnviados.Count);
        Assert.All(envio.DocumentosEnviados, note => Assert.True(note.ValidarAssinatura()));
        Assert.False(original.Assinado); Assert.False(other.Assinado);
        var retorno = await client.ConsultaReciboAsync(new NFeReciboSolicitacao { Recibo = envio.Recibo, Documentos = envio.DocumentosEnviados });
        Assert.Equal(2, retorno.Documentos.Count);
        Assert.All(retorno.Documentos, note => { Assert.True(note.Autorizado); Assert.True(note.ValidarAssinatura()); });
    }

    [Fact]
    public void ModeloTipadoPreservaQrCodeComoTexto()
    {
        var note = NotaFiscal.Load(Fixtures.Nota(65).GetXml());
        note.InfNFeSupl = new NFeSupl { QrCode = "https://consulta.test/qrcode?p=chave|3|2", UrlChave = "https://consulta.test" };
        var reloaded = NotaFiscal.Load(note.GetXml());
        Assert.Equal(note.InfNFeSupl.QrCode, reloaded.InfNFeSupl.QrCode);
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

    internal sealed record Request(Uri Url, XElement Xml, X509Certificate2 Certificate);
    internal sealed class CaptureTransport(Func<Request, CancellationToken, Task<HttpResponseMessage>> send) : IDFeHttpTransport
    {
        internal ConcurrentBag<Request> Requests = [];
        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, DFeHttpTransportOptions options, CancellationToken cancellationToken = default)
        {
            Assert.False(options.AllowAutoRedirect);
            Assert.False(options.UseCookies);
            Assert.Equal(DecompressionMethods.None, options.AutomaticDecompression);
            var captured = new Request(request.RequestUri!, XmlFixture.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)), options.Certificate!);
            Requests.Add(captured);
            return await send(captured, cancellationToken);
        }
    }
}

/// <summary>Confere os contratos do assembly contra os XSDs compilados, sem depender de um gerador.</summary>
public sealed class SchemaCoverageTests(Xunit.Abstractions.ITestOutputHelper output)
{
    private sealed record Binding(System.Reflection.PropertyInfo Property, Type Type, int Order);

    [Theory]
    [InlineData("3.10")]
    [InlineData("4.00")]
    public void TodosOsCamposDosDoisLeiautesPossuemContratoESaoSerializados(string version)
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
        output.WriteLine(json);
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, report.errors));
    }
}
