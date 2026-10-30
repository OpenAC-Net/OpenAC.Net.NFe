using System;
using System.Security.Cryptography.X509Certificates;
using OpenAC.Net.DFe.Core.Common;
using OpenAC.Net.NFe.Common.Types;
using OpenAC.Net.NFe.Services;

namespace OpenAC.Net.NFe.Common;

/// <summary>Opções capturadas na criação do cliente. Transporte e armazenamento injetados devem aceitar concorrência.</summary>
public sealed class NFeConfig : DFeConfigBase<NFeConfigGeral, NFeConfigWebServices, NFeConfigCertificados, NFeConfigArquivos>
{
    public NFeConfig()
    {
        Geral = new NFeConfigGeral();
        WebServices = new NFeConfigWebServices();
        Certificados = new NFeConfigCertificados();
        Arquivos = new NFeConfigArquivos(Geral) { Salvar = false };
    }

    /// <summary>Certificado emprestado pela aplicação. Cada operação obtém uma cópia própria.</summary>
    public X509Certificate2 Certificado { get; set; }

    public NFeConfig Copiar()
    {
        var copy = new NFeConfig
        {
            Certificado = Certificado,
            HttpTransport = HttpTransport ?? throw new InvalidOperationException("Transporte HTTP não configurado."),
            DocumentStore = DocumentStore ?? throw new InvalidOperationException("Armazenamento não configurado.")
        };
        copy.Geral.VersaoDFe = Geral.VersaoDFe;
        copy.Geral.Modelo = Geral.Modelo;
        copy.Geral.FormaEmissao = Geral.FormaEmissao;
        copy.Geral.VersaoQRCode = Geral.VersaoQRCode;
        copy.Geral.CSC = Geral.CSC;
        copy.Geral.IdCSC = Geral.IdCSC;
        copy.Geral.Salvar = Geral.Salvar;
        copy.Geral.ExibirErroSchema = Geral.ExibirErroSchema;
        copy.Geral.FormatoAlerta = Geral.FormatoAlerta;
        copy.Geral.RetirarAcentos = Geral.RetirarAcentos;
        copy.Geral.RetirarEspacos = Geral.RetirarEspacos;
        copy.Geral.IdentarXml = Geral.IdentarXml;
        copy.Geral.ValidarDigest = Geral.ValidarDigest;
        copy.WebServices.UF = WebServices.UF;
        copy.WebServices.TimeoutOperacao = WebServices.TimeoutOperacao;
        copy.WebServices.Ambiente = WebServices.Ambiente;
        copy.WebServices.Salvar = WebServices.Salvar;
        copy.WebServices.Protocolos = WebServices.Protocolos;
        copy.WebServices.Proxy = WebServices.Proxy;
        copy.WebServices.TimeOut = WebServices.TimeOut;
        copy.WebServices.Tentativas = WebServices.Tentativas;
        copy.WebServices.IntervaloTentativas = WebServices.IntervaloTentativas;
        copy.WebServices.AjustaAguardaConsultaRet = WebServices.AjustaAguardaConsultaRet;
        copy.WebServices.AguardarConsultaRet = WebServices.AguardarConsultaRet;
        foreach (var item in WebServices.Enderecos) copy.WebServices.Enderecos.Add(item.Key, item.Value);
        copy.Certificados.Certificado = Certificados.Certificado;
        copy.Certificados.Senha = Certificados.Senha;
        copy.Certificados.CertificadoBytes = Certificados.CertificadoBytes == null ? null : (byte[])Certificados.CertificadoBytes.Clone();
        copy.Arquivos.PathSchemas = Arquivos.PathSchemas;
        copy.Arquivos.PathSalvar = Arquivos.PathSalvar;
        copy.Arquivos.Salvar = Arquivos.Salvar;
        copy.Arquivos.AdicionarLiteral = Arquivos.AdicionarLiteral;
        copy.Arquivos.SepararPorCNPJ = Arquivos.SepararPorCNPJ;
        copy.Arquivos.SepararPorModelo = Arquivos.SepararPorModelo;
        copy.Arquivos.SepararPorAno = Arquivos.SepararPorAno;
        copy.Arquivos.SepararPorMes = Arquivos.SepararPorMes;
        copy.Arquivos.SepararPorDia = Arquivos.SepararPorDia;
        copy.Arquivos.OrdenacaoPath.AddRange(Arquivos.OrdenacaoPath);
        copy.Arquivos.CopiarCatalogo(Arquivos);
        return copy;
    }

    internal string ObterEndereco(TipoServicoNFe service) => NFeServiceManager.ObterEndereco(this, service);
}
