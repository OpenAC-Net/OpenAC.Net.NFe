using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Collection;
using OpenAC.Net.NFe.Common.Types;

namespace OpenAC.Net.NFe.Common.Model;

/// <summary>Vincula os grupos à informação fiscal que define o leiaute do documento.</summary>
public abstract class NFeItem<TItem> : DFeParentItem<TItem, NFeInfNFe> where TItem : class
{
    [DFeIgnore]
    protected NFeInfNFe Raiz => this is NFeInfNFe raiz ? raiz : Parent;

    [DFeIgnore]
    protected NFeVersao Leiaute => Raiz?.Versao ?? NFeVersao.v400;

    protected TChild Vincular<TChild>(TChild child) where TChild : NFeItem<TChild>
    {
        if (child != null) child.Parent = Raiz;
        return child;
    }

    protected DFeParentCollection<TChild, NFeInfNFe> Vincular<TChild>(DFeParentCollection<TChild, NFeInfNFe> children)
        where TChild : NFeItem<TChild>
    {
        if (children != null) children.Parent = Raiz;
        return children;
    }
}
