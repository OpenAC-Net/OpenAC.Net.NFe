using OpenAC.Net.NFe.Common.Model;
using System.Xml.Linq;
using OpenAC.Net.DFe.Core;
using Xunit;

namespace OpenAC.Net.NFe.Tests;

public sealed class TypedDocumentTests
{
    private static string Schemas => Path.Combine(AppContext.BaseDirectory, "Schemas");

    [Theory]
    [InlineData(55, "3.10")]
    [InlineData(65, "3.10")]
    [InlineData(55, "4.00")]
    [InlineData(65, "4.00")]
    public void DocumentoProcessadoSalvaECarregaPeloCoreComAssinaturaEProtocolo(int modelo, string versao)
    {
        using var certificado = Fixtures.Certificate();
        var nota = Fixtures.Nota(modelo, versao);
        nota.Assinar(certificado);
        if (modelo == 65 && versao == "4.00") nota.GerarQrCode(Fixtures.Config(certificado, 65));
        var protocolo = Fixtures.Protocolo(nota);
        var processada = nota.ComProtocolo(protocolo);
        using var stream = new MemoryStream();
        processada.Save(stream);
        var carregada = NFeProc.Load(stream);
        carregada.Validar(Schemas);
        Assert.True(carregada.ValidarAssinatura());
        Assert.Equal(nota.Chave, carregada.Chave);
        Assert.Equal(protocolo.InfProt.NProt, carregada.ProtNFe.InfProt.NProt);
        Assert.Equal(nota.QrCode, carregada.QrCode);
        Assert.Same(carregada.NFe.InfNFe, carregada.NFe.InfNFe.Det[0].Prod.Parent);

        nota.InfNFe.Ide.NNF = 2;
        protocolo.InfProt.NProt = "OUTRO";
        Assert.True(processada.ValidarAssinatura());
        Assert.Equal("135260000000001", processada.ProtNFe.InfProt.NProt);
    }

    [Fact]
    public void ProtocoloPreservaAssinaturaSefazEMultiplosParesDeMensagens()
    {
        using var certificado = Fixtures.Certificate();
        var nota = Fixtures.Nota();
        nota.Assinar(certificado);
        var protocolo = Fixtures.Protocolo(nota);
        protocolo.InfProt.Id = "ID135260000000001";
        protocolo.InfProt.Mensagens.Add(new NFeCodigoMensagem { Valor = "1" });
        protocolo.InfProt.Mensagens.Add(new NFeTextoMensagem { Valor = "Primeiro aviso" });
        protocolo.InfProt.Mensagens.Add(new NFeCodigoMensagem { Valor = "2" });
        protocolo.InfProt.Mensagens.Add(new NFeTextoMensagem { Valor = "Segundo aviso" });
        var assinado = XmlSigning.AssinarXml(protocolo.GetXml(), "protNFe", "infProt", certificado, showDeclaration: false);
        var carregado = NFeProtNFe.Load(assinado);
        var processada = nota.ComProtocolo(carregado);
        processada.Validar(Schemas);
        var recarregada = NFeProc.Load(processada.GetXml());
        Assert.Equal(new[] { "1", "Primeiro aviso", "2", "Segundo aviso" },
            recarregada.ProtNFe.InfProt.Mensagens.Select(item => item.Valor));
        Assert.True(XmlSigning.ValidarAssinatura(recarregada.ProtNFe, true));
        Assert.True(recarregada.ValidarAssinatura());
    }

    [Fact]
    public async Task InutilizacaoGravaDocumentoTipadoValidoEComAssinatura()
    {
        using var certificado = Fixtures.Certificate();
        const string retorno = """
            <retInutNFe xmlns="http://www.portalfiscal.inf.br/nfe" versao="4.00"><infInut>
            <tpAmb>2</tpAmb><verAplic>TESTE1.0</verAplic><cStat>102</cStat><xMotivo>Inutilizacao homologada</xMotivo>
            <cUF>35</cUF><ano>26</ano><CNPJ>12345678000195</CNPJ><mod>55</mod><serie>1</serie>
            <nNFIni>1</nNFIni><nNFFin>3</nNFFin><dhRecbto>2026-10-28T10:01:00-03:00</dhRecbto>
            <nProt>135260000000001</nProt></infInut></retInutNFe>
            """;
        var store = new MemoryStore();
        var configuracao = Fixtures.Config(certificado);
        configuracao.Arquivos.Salvar = true;
        configuracao.Arquivos.SepararPorCNPJ = true;
        configuracao.DocumentStore = store;
        var cliente = Fixtures.Client(configuracao, new FakeTransport { Response = _ => retorno });
        var resposta = await cliente.InutilizarAsync(new NFeInutilizacaoSolicitacao
        {
            Cnpj = "12345678000195", Ano = 26, Serie = 1, NumeroInicial = 1, NumeroFinal = 3,
            Justificativa = "Inutilizacao de numeracao para teste"
        });
        Assert.Equal(102, resposta.CStat);
        var documento = Assert.Single(store.Documents);
        Assert.EndsWith("-procInutNFe.xml", documento.Caminho);
        Assert.Contains("12345678000195", documento.Caminho);
        AssertSchema(documento.Conteudo, "procInutNFe_v4.00.xsd");
        var carregada = NFeProcInutilizacao.Load(documento.Conteudo);
        Assert.Equal("135260000000001", carregada.Retorno.InfInut.Protocolo);
        Assert.True(XmlSigning.ValidarAssinatura(carregada.Inutilizacao, true));
        Assert.Same(resposta.Resultado, resposta.Documento.Retorno);
    }

    [Fact]
    public async Task EventoProcessadoMantemDadosOriginaisESalvaXmlValido()
    {
        using var certificado = Fixtures.Certificate();
        var evento = NFeEvento.Cancelamento(Fixtures.Nota().Chave, "12345678000195",
            "135260000000001", "Cancelamento de teste solicitado pelo cliente");
        var store = new MemoryStore();
        var configuracao = Fixtures.Config(certificado);
        configuracao.Arquivos.Salvar = true;
        configuracao.DocumentStore = store;
        var transporte = new FakeTransport
        {
            Response = _ => $"""
                <retEnvEvento xmlns="http://www.portalfiscal.inf.br/nfe" versao="1.00">
                <idLote>1</idLote><tpAmb>2</tpAmb><verAplic>TESTE1.0</verAplic><cOrgao>35</cOrgao>
                <cStat>128</cStat><xMotivo>Lote de Evento Processado</xMotivo><retEvento versao="1.00"><infEvento>
                <tpAmb>2</tpAmb><verAplic>TESTE1.0</verAplic><cOrgao>35</cOrgao><cStat>135</cStat>
                <xMotivo>Evento registrado e vinculado a NF-e</xMotivo><chNFe>{evento.Chave}</chNFe>
                <tpEvento>110111</tpEvento><xEvento>Cancelamento</xEvento><nSeqEvento>1</nSeqEvento>
                <dhRegEvento>2026-10-28T10:01:00-03:00</dhRegEvento><nProt>135260000000002</nProt>
                </infEvento></retEvento></retEnvEvento>
                """
        };
        var cliente = Fixtures.Client(configuracao, transporte);
        var resposta = await cliente.EnviarEventoAsync(new NFeEventoSolicitacao { Eventos = [evento] });
        var processado = Assert.Single(resposta.Eventos);
        Assert.True(processado.Evento.ValidarAssinatura());
        Assert.True(string.IsNullOrEmpty(evento.Signature.SignatureValue));
        Assert.Null(evento.InfEvento.Id);
        AssertSchema(resposta.Resultado.GetXml(), "retEnvEvento_v1.00.xsd");
        var documento = Assert.Single(store.Documents);
        AssertSchema(documento.Conteudo, "procEventoNFe_v1.00.xsd");
        var carregado = NFeProcEvento.Load(documento.Conteudo);
        Assert.Equal(135, carregado.RetEvento.InfEvento.CStat);
        Assert.True(carregado.Evento.ValidarAssinatura());
        carregado.Evento.Detalhe.Justificativa = "Justificativa alterada depois da assinatura";
        Assert.False(carregado.Evento.ValidarAssinatura());
    }

    [Fact]
    public async Task ConsultaReciboRecusaDocumentoAlteradoDepoisDeAssinar()
    {
        using var certificado = Fixtures.Certificate();
        var nota = Fixtures.Nota();
        nota.Assinar(certificado);
        var protocolo = Fixtures.Protocolo(nota);
        nota.InfNFe.Ide.NatOp = "OPERACAO ALTERADA";
        var transporte = new FakeTransport
        {
            Response = _ => new XElement(XmlFixture.Ns + "retConsReciNFe",
                new XElement(XmlFixture.Ns + "cStat", "104"), protocolo.WriteToXml()).ToString()
        };
        var cliente = Fixtures.Client(Fixtures.Config(certificado), transporte);
        await Assert.ThrowsAsync<OpenDFeValidationException>(() => cliente.ConsultaReciboAsync(
            new NFeReciboSolicitacao { Recibo = "351260000000001", Documentos = [nota] }));
    }

    private static void AssertSchema(string xml, string schema)
    {
        Assert.True(XmlSchemaValidation.ValidarXml(xml, Path.Combine(Schemas, schema), out var erros, out _),
            string.Join(Environment.NewLine, erros));
    }

    private sealed class MemoryStore : OpenAC.Net.DFe.Core.Storage.IDFeDocumentStore
    {
        internal List<OpenAC.Net.DFe.Core.Storage.IDFeDocument> Documents { get; } = [];
        public Task SaveAsync(OpenAC.Net.DFe.Core.Storage.IDFeDocument documento, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Documents.Add(documento);
            return Task.CompletedTask;
        }
    }
}
