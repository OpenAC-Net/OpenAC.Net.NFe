using System;

namespace OpenAC.Net.NFe.DANFe.EscPos.Configuracao;

/// <summary>Apresentação do cupom; conexão, protocolo e página de código pertencem ao EscPosPrinter.</summary>
public sealed class DANFeEscPosConfig
{
    /// <summary>Quando não informada, usa as colunas configuradas no componente; exportações usam 48.</summary>
    public int? Colunas { get; set; }
    public bool CortarPapel { get; set; } = true;
    public bool CorteParcial { get; set; }
    public int LinhasAvanco { get; set; } = 5;
    public int ModuloQrCode { get; set; } = 4;
    public int Copias { get; set; } = 1;
    public bool ExibirItens { get; set; } = true;
    public bool ExibirNomeFantasia { get; set; } = true;
    public bool UsarLogoDaImpressora { get; set; }
    public byte[] Logo { get; set; }
    public string MensagemRodape { get; set; }

    public DANFeEscPosConfig Copiar()
    {
        var copia = (DANFeEscPosConfig)MemberwiseClone();
        copia.Logo = Logo == null ? null : (byte[])Logo.Clone();
        return copia;
    }
    internal void Validar(int colunas)
    {
        if (colunas < 24 || colunas > 80) throw new ArgumentOutOfRangeException(nameof(Colunas));
        if (ModuloQrCode < 1 || ModuloQrCode > 8) throw new ArgumentOutOfRangeException(nameof(ModuloQrCode));
        if (LinhasAvanco < 0 || LinhasAvanco > 255) throw new ArgumentOutOfRangeException(nameof(LinhasAvanco));
        if (Copias < 1 || Copias > 99) throw new ArgumentOutOfRangeException(nameof(Copias));
    }
}
