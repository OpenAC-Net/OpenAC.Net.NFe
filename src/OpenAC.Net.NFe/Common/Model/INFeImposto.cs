namespace OpenAC.Net.NFe;

/// <summary>Grupo tributário vinculado às informações fiscais do documento.</summary>
public interface INFeGrupoTributacao
{
    NFeInfNFe Parent { get; set; }
}

/// <summary>Alternativa ICMS ou ISSQN do item.</summary>
public interface INFeImposto : INFeGrupoTributacao { }

/// <summary>Uma única modalidade de ICMS por item.</summary>
public interface IImpostoICMS : INFeGrupoTributacao { }

/// <summary>Uma única modalidade de PIS por item.</summary>
public interface INFePIS : INFeGrupoTributacao { }

/// <summary>Uma única modalidade de COFINS por item.</summary>
public interface INFeCOFINS : INFeGrupoTributacao { }

/// <summary>IPI tributado ou não tributado.</summary>
public interface INFeIPI : INFeGrupoTributacao { }
