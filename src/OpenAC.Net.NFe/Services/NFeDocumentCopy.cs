using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenAC.Net.NFe.Services;

// Copia o grafo de dados fiscal, inclusive listas e referências Parent dos modelos.
// Não usa a serialização fiscal, que pode substituir nomes conforme o ambiente.
internal static class NFeDocumentCopy
{
    private static readonly MethodInfo Memberwise = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);

    internal static T Copiar<T>(T documento) => (T)Copiar(documento, new Dictionary<object, object>(new IdentityComparer()));

    private static object Copiar(object source, Dictionary<object, object> copies)
    {
        if (source == null) return null;
        var type = source.GetType();
        if (type.IsValueType || source is string) return source;
        if (copies.TryGetValue(source, out var previous)) return previous;
        var copy = source is Array sourceArray ? sourceArray.Clone() : Memberwise.Invoke(source, null);
        copies.Add(source, copy);
        if (source is Array array)
        {
            // Os modelos fiscais usam somente vetores unidimensionais.
            var result = (Array)copy;
            for (var i = 0; i < array.Length; i++) result.SetValue(Copiar(array.GetValue(i), copies), i);
            return result;
        }
        for (var current = type; current != null; current = current.BaseType)
            foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                if (!field.FieldType.IsValueType && field.FieldType != typeof(string))
                    field.SetValue(copy, Copiar(field.GetValue(source), copies));
        return copy;
    }

    private sealed class IdentityComparer : IEqualityComparer<object>
    {
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);
        public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
    }
}
