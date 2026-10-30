#!/usr/bin/env python3
"""Gera uma família única de modelos NF-e 3.10/4.00 para o serializador do DFe.Core."""
from pathlib import Path
from dataclasses import dataclass, replace
import html
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1] / 'OpenAC.Net.NFe'
XS = '{http://www.w3.org/2001/XMLSchema}'
VERSIONS = ('3.10', '4.00')
# O grupo de cartão é o mesmo; na 4.00 passou a ficar dentro de detPag.
ALIASES = {'NFePagDetPagCard': 'NFePagCard'}


@dataclass
class Field:
    xml: str
    prop: str
    type: str
    format: str | None
    optional: bool
    repeated: bool
    attribute: bool
    description: str


def pascal(value):
    return value[:1].upper() + value[1:]


def read_schema(version):
    types, loaded, classes, names, by_node = {}, set(), {}, set(), {}

    def load(file):
        if file in loaded:
            return
        loaded.add(file)
        for node in ET.parse(ROOT / 'Schemas' / file).getroot():
            if node.tag == XS + 'include':
                load(node.attrib['schemaLocation'])
            if node.tag in (XS + 'complexType', XS + 'simpleType'):
                types[node.attrib['name']] = node

    def make(node, path):
        if id(node) in by_node:
            return by_node[id(node)]
        name = 'NFe' if path == 'NFe' else 'NFe' + ''.join(pascal(part) for part in path.split('/')[1:])
        if name != 'NFeInfNFe':
            name = name.replace('InfNFe', '')
        if name in names:
            name += 'Grupo' + str(len(names))
        name = ALIASES.get(name, name)
        names.add(name)
        by_node[id(node)] = name
        classes[name] = []
        fields = []

        def walk(parent, optional=False, repeated=False):
            for child in parent:
                tag = child.tag.removeprefix(XS)
                if tag in ('sequence', 'all', 'choice'):
                    walk(child, optional or child.get('minOccurs') == '0' or tag == 'choice',
                         repeated or child.get('maxOccurs', '1') != '1')
                elif tag in ('element', 'attribute'):
                    if child.get('ref') == 'ds:Signature':
                        continue
                    if child.get('ref'):
                        raise ValueError('Referência não suportada: ' + child.get('ref'))
                    xml = child.attrib['name']
                    is_optional = optional or child.get('minOccurs') == '0' or (tag == 'attribute' and child.get('use') != 'required')
                    is_repeated = repeated or child.get('maxOccurs', '1') != '1'
                    target = child.find(XS + 'complexType')
                    simple = child.find(XS + 'simpleType')
                    typename = child.get('type', '').split(':')[-1]
                    if target is None and typename in types and types[typename].tag == XS + 'complexType':
                        target = types[typename]
                    if target is not None:
                        csharp_type, format = make(target, path + '/' + xml), None
                    else:
                        if simple is None:
                            simple = types.get(typename)
                        visited, patterns = set(), []
                        while simple is not None:
                            patterns += [item.get('value', '') for item in simple.iter(XS + 'pattern')]
                            restriction = simple.find(XS + 'restriction')
                            base = restriction.get('base', '').split(':')[-1] if restriction is not None else ''
                            if base in visited:
                                break
                            visited.add(base)
                            simple = types.get(base)
                        decimal_pattern = next((pattern for pattern in patterns if r'\.' in pattern), None)
                        if decimal_pattern and xml not in ('versao', 'qrCode'):
                            fractions = re.findall(r'\[0-9\]\{(?:[0-9]+,)?([0-9]+)\}', decimal_pattern)
                            scale = int(fractions[-1]) if fractions else 2
                            format = {2: 'De2', 3: 'De3', 4: 'De4', 6: 'De6', 10: 'De10'}.get(scale, 'De10')
                            csharp_type = 'decimal'
                        else:
                            csharp_type, format = 'string', 'Str'
                    description = ' '.join(' '.join(item.itertext()).strip() for item in child.findall(XS + 'annotation/' + XS + 'documentation'))
                    fields.append(Field(xml, pascal(xml), csharp_type, format, is_optional, is_repeated, tag == 'attribute', description))
                elif tag not in ('annotation', 'simpleType'):
                    raise ValueError('Construto XSD sem suporte: ' + tag)

        walk(node)
        unique = {}
        for field in fields:
            if field.xml in unique and unique[field.xml].type != field.type:
                raise ValueError('Tipos incompatíveis no mesmo leiaute: ' + name + '/' + field.xml)
            unique.setdefault(field.xml, field)
        classes[name] = list(unique.values())
        return name

    load('leiauteNFe_v' + version + '.xsd')
    make(types['TNFe'], 'NFe')
    return classes


def merge_order(class_name, schemas):
    # Uma ordenação topológica preserva simultaneamente a sequência dos dois XSD.
    order, outgoing, incoming = [], {}, {}
    for version in reversed(VERSIONS):
        fields = schemas[version].get(class_name, [])
        elements = [field.xml for field in fields if not field.attribute]
        for xml in elements:
            if xml not in outgoing:
                order.append(xml)
                outgoing[xml], incoming[xml] = set(), 0
        for first, second in zip(elements, elements[1:]):
            if second not in outgoing[first]:
                outgoing[first].add(second)
                incoming[second] += 1
    result = []
    while len(result) < len(order):
        node = next((xml for xml in order if incoming[xml] == 0 and xml not in result), None)
        if node is None:
            raise ValueError('Ordens incompatíveis entre versões: ' + class_name)
        result.append(node)
        for child in outgoing[node]:
            incoming[child] -= 1
    return result


def generate():
    schemas = {version: read_schema(version) for version in VERSIONS}
    class_names = list(schemas['4.00']) + [name for name in schemas['3.10'] if name not in schemas['4.00']]
    output = ROOT / 'Common' / 'Model'
    output.mkdir(parents=True, exist_ok=True)
    generated = set()
    for name in class_names:
        fields = {}
        for version in VERSIONS:
            for field in schemas[version].get(name, []):
                fields.setdefault(field.xml, {})[version] = field
        element_order = merge_order(name, schemas)
        attribute_order = [xml for xml, versions in fields.items() if next(iter(versions.values())).attribute]
        if name == 'NFeInfNFe':
            attribute_order = ['versao', 'Id']
        sequence = attribute_order + element_order
        lines = ['// <auto-generated />', '// Fonte: Schemas/leiauteNFe_v3.10.xsd e leiauteNFe_v4.00.xsd.',
                 '// Regenerar: python3 src/tools/generate-models.py',
                 'using OpenAC.Net.DFe.Core.Attributes;', 'using OpenAC.Net.DFe.Core.Collection;',
                 'using OpenAC.Net.DFe.Core.Document;', 'using OpenAC.Net.DFe.Core.Serializer;',
                 'using OpenAC.Net.NFe.Common.Types;', 'using OpenAC.Net.NFe.Common.Model;', '',
                 'namespace OpenAC.Net.NFe.Common.Model;' if name == 'NFe' else 'namespace OpenAC.Net.NFe;', '']
        if name == 'NFe':
            lines += ['[DFeSignInfoElement("infNFe")]', '[DFeRoot("NFe", Namespace = "http://www.portalfiscal.inf.br/nfe")]']
        lines += ['public sealed partial class ' + name + (' : DFeSignDocument<NFe>' if name == 'NFe' else ' : NFeItem<' + name + '>'), '{']
        bindings, conditions = [], []
        for index, xml in enumerate(sequence):
            versions = fields[xml]
            field = versions.get('4.00') or versions['3.10']
            if any(other.type != field.type for other in versions.values()):
                raise ValueError('Tipo precisa de regra explícita: ' + name + '/' + xml)
            if any(other.format != field.format for other in versions.values()):
                raise ValueError('Precisão precisa de regra explícita: ' + name + '/' + xml)
            # Os dois grupos cujo maxOccurs mudou continuam coleções na API unificada.
            repeated = any(other.repeated for other in versions.values())
            optional = any(other.optional for other in versions.values())
            csharp_type = field.type + ('?' if optional and field.type == 'decimal' else '')
            child = field.format is None
            if name == 'NFeInfNFe' and xml == 'versao':
                csharp_type = 'NFeVersao'
                field = replace(field, format='Enum', description='Versão do leiaute da NF-e (3.10 ou 4.00).')
            if repeated:
                csharp_type = ('DFeParentCollection<' + csharp_type + ', NFeInfNFe>' if child else 'DFeCollection<' + csharp_type + '>')
            backing = field.prop[:1].lower() + field.prop[1:] + 'Value'
            bind = child and name != 'NFe'
            root_child = child and name == 'NFe'
            if root_child:
                initializer = ' = new();' if xml == 'infNFe' else ';'
                lines += ['    private ' + csharp_type + ' ' + backing + initializer, '']
            if bind:
                lines += ['    private ' + csharp_type + ' ' + backing + ';', '']
                bindings.append(backing)
            if field.description:
                lines += ['    /// <summary>' + html.escape(' '.join(field.description.split())) + '</summary>']
            kind = 'DFeAttribute' if field.attribute else 'DFeCollection' if repeated else 'DFeElement'
            order = index * 10 if field.attribute else (element_order.index(xml) + 1) * 10
            args = (('TipoCampo.' + field.format + ', ') if field.format else '') + '"' + xml + '", Ordem = ' + str(order) + ', Ocorrencia = Ocorrencia.' + ('NaoObrigatoria' if optional else 'Obrigatoria')
            lines += ['    [' + kind + '(' + args + ')]']
            if bind:
                getter = backing + ' ??= new(Raiz)' if repeated else backing
                lines += ['    public ' + csharp_type + ' ' + field.prop,
                          '    {', '        get => ' + getter + ';', '        set => ' + backing + ' = Vincular(value);', '    }', '']
            elif root_child:
                lines += ['    public ' + csharp_type + ' ' + field.prop, '    {', '        get => ' + backing + ';', '        set', '        {', '            ' + backing + ' = value;']
                if xml == 'infNFe':
                    lines += ['            if (infNFeSuplValue != null) infNFeSuplValue.Parent = value;']
                else:
                    lines += ['            if (value != null) value.Parent = InfNFe;']
                lines += ['        }', '    }', '']
            else:
                initializer = ' = new();' if repeated or name == 'NFe' and xml == 'infNFe' else ' = NFeVersao.v400;' if name == 'NFeInfNFe' and xml == 'versao' else ''
                lines += ['    public ' + csharp_type + ' ' + field.prop + ' { get; set; }' + initializer, '']
            owner_versions = {version for version in VERSIONS if name in schemas[version]}
            if set(versions) != owner_versions:
                version = next(iter(versions))
                conditions += ['    internal bool ShouldSerialize' + field.prop + '() => Leiaute == NFeVersao.v' + version.replace('.', '') + ';']
        if conditions:
            lines += conditions + ['']
        if bindings:
            lines += ['    protected override void OnParentChanged()', '    {', '        base.OnParentChanged();']
            lines += ['        Vincular(' + backing + ');' for backing in bindings]
            lines += ['    }', '']
        lines += ['}', '']
        path = output / (name + '.cs')
        path.write_text('\n'.join(lines))
        generated.add(path.name)
    for path in output.glob('NFe*.cs'):
        if path.name not in generated and path.read_text().startswith('// <auto-generated />'):
            path.unlink()
    print(str(len(class_names)) + ' classes unificadas; ' + str(sum(len(fields) for fields in schemas.values())) + ' definições por versão lidas dos XSD.')


if __name__ == '__main__':
    generate()
