using System.IO;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Types;
using System.Text;
using System.Xml.Linq;
using OpenAC.Net.DFe.Core.Common;
using OpenAC.Net.NFe.Services;
using System.Threading.Tasks;

namespace OpenAC.Net.NFe.Tests;

public sealed class CatalogTests
{
    [Test]
    public async Task ExportaERecarregaCatalogoSemPerderModeloVersaoOuEnderecos()
    {
        using var input = typeof(OpenNFe).Assembly.GetManifestResourceStream("OpenAC.Net.NFe.Resources.services.xml")!;
        var catalog = NFeServiceManager.CarregarCatalogo(input);
        await Assert.That(catalog.Webservices.Count).IsEqualTo(4);
        foreach (var group in catalog.Webservices)
            await Assert.That(group.Versao).IsEqualTo("4.00");
        using var exported = new MemoryStream(Encoding.UTF8.GetBytes(catalog.GetXml()));
        var reloaded = NFeServiceManager.CarregarCatalogo(exported);
        await Assert.That(reloaded.GetXml()).IsEqualTo(catalog.GetXml());
        foreach (var model in new[] { ModeloNFe.NFe, ModeloNFe.NFCe })
        foreach (var environment in new[] { DFeTipoAmbiente.Producao, DFeTipoAmbiente.Homologacao })
        foreach (var state in Enum.GetValues<DFeSiglaUF>().Where(uf => uf is not (DFeSiglaUF.AN or DFeSiglaUF.EX or DFeSiglaUF.SU)))
        {
            var config = new NFeConfig(); config.Geral.Modelo = model; config.WebServices.UF = state; config.WebServices.Ambiente = environment;
            foreach (var service in new[] { TipoServicoNFe.NFeAutorizacao, TipoServicoNFe.NFeConsultaProtocolo, TipoServicoNFe.NFeStatusServico, TipoServicoNFe.RecepcaoEvento })
                await Assert.That(new Uri(NFeServiceManager.ObterEndereco(config, service)).Scheme).IsEqualTo("https");
                }
    }

    [Test]
    [Arguments("versao")]
    [Arguments("modelo")]
    [Arguments("emissao")]
    [Arguments("ambiente")]
    [Arguments("uf")]
    [Arguments("servico")]
    [Arguments("duplicado")]
    [Arguments("http")]
    public async Task RejeitaCatalogoInvalidoAntesDeUsarEndpoints(string mutation)
    {
        XNamespace ns = "https://www.openac.net.br/";
        var address = new XElement(ns + "Endereco", new XAttribute("Tipo", "NFeStatusServico"), "https://empresa.test/status");
        var environment = new XElement(ns + "Ambiente", new XAttribute("Tipo", "2"), new XAttribute("UF", "SP"), new XElement(ns + "Enderecos", address));
        var group = new XElement(ns + "Services", new XAttribute("Tipo", "NFe"), new XAttribute("TipoEmissao", "1"), new XAttribute("Versao", "4.00"), environment);
        var root = new XElement(ns + "DFeServices", group);
        switch (mutation)
        {
            case "versao": group.SetAttributeValue("Versao", "3.10"); break;
            case "modelo": group.SetAttributeValue("Tipo", "CTe"); break;
            case "emissao": group.SetAttributeValue("TipoEmissao", "8"); break;
            case "ambiente": environment.SetAttributeValue("Tipo", "3"); break;
            case "uf": environment.SetAttributeValue("UF", "AN"); break;
            case "servico": address.SetAttributeValue("Tipo", "NfeRecepcao"); break;
            case "duplicado": address.Parent!.Add(new XElement(address)); break;
            case "http": address.Value = "http://empresa.test/status"; break;
        }
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(root.ToString()));
        if (mutation is "versao" or "modelo" or "servico" or "duplicado") await Assert.That(() => NFeServiceManager.CarregarCatalogo(input)).ThrowsExactly<NotSupportedException>();
        else await Assert.That(() => NFeServiceManager.CarregarCatalogo(input)).Throws<ArgumentException>();
    }

    [Test]
    public async Task CatalogoCustomizadoAtualizadoEhUsadoNaProximaOperacao()
    {
        var path = Path.Combine(Path.GetTempPath(), "nfe-services-" + Guid.NewGuid().ToString("N") + ".xml");
        using var cert = Fixtures.Certificate();
        const string xml = """
            <DFeServices xmlns="https://www.openac.net.br/"><Services Tipo="NFe" TipoEmissao="1" Versao="4.00">
            <Ambiente Tipo="2" UF="SP"><Enderecos><Endereco Tipo="NFeStatusServico">https://catalogo-a.test/status</Endereco></Enderecos></Ambiente>
            </Services></DFeServices>
            """;
        try
        {
            await File.WriteAllTextAsync(path, xml);
            var config = Fixtures.Config(cert); config.Arquivos.ArquivoServicos = path;
            var transport = new CoreIntegrationTests.CaptureTransport((_, _) => Task.FromResult(CoreIntegrationTests.Reply("<retConsStatServ xmlns='http://www.portalfiscal.inf.br/nfe'><cStat>107</cStat></retConsStatServ>")));
            config.HttpTransport = transport;
            var client = new OpenNFe(config);
            await File.WriteAllTextAsync(path, xml.Replace("catalogo-a", "catalogo-b"));
            config.Arquivos.ArquivoServicos = "";
            config.Arquivos.ArquivoServicos = path;
            await Assert.That((await client.ConsultarSituacaoServicoAsync()).CStat).IsEqualTo(107);
            await Assert.That((await Assert.That(transport.Requests).HasSingleItem()).Url.Host).IsEqualTo("catalogo-b.test");
        }
        finally { File.Delete(path); }
    }

    [Test]
    [Arguments(DFeTipoEmissao.SVCAN, "sefazvirtual.fazenda.gov.br")]
    [Arguments(DFeTipoEmissao.SVCRS, "nfe-homologacao.svrs.rs.gov.br")]
    public async Task ContingenciaNaoAlteraDistribuicaoOuQrCode(DFeTipoEmissao emission, string host)
    {
        var config = new NFeConfig(); config.Geral.FormaEmissao = emission;
        await Assert.That(NFeServiceManager.ObterEndereco(config, TipoServicoNFe.NFeAutorizacao)).Contains(host);
        await Assert.That(NFeServiceManager.ObterEndereco(config, TipoServicoNFe.NFeDistribuicaoDFe)).Contains("hom1.nfe.fazenda.gov.br");
        var address = NFeServiceManager.UrlConsulta(config);
        config.Geral.FormaEmissao = DFeTipoEmissao.Normal;
        await Assert.That(NFeServiceManager.UrlConsulta(config)).IsEqualTo(address);
        config.Geral.Modelo = ModeloNFe.NFCe; config.Geral.FormaEmissao = emission;
        await Assert.That(() => NFeServiceManager.ObterEndereco(config, TipoServicoNFe.NFeAutorizacao)).ThrowsExactly<NotSupportedException>();
    }
}