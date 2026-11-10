"""Gera notas fiscais eletrônicas (NF-e) de TESTE para o Previsio.

Cria XMLs, DANFEs em PDF e um .zip, separados em pastas: cada pasta é um e-mail
a enviar para a caixa dedicada. O LEIAME.md gerado diz o resultado esperado de cada um.

Tudo é fictício e em ambiente de homologação (tpAmb=2, "SEM VALOR FISCAL").
Os XMLs não são assinados (o Previsio não confere assinatura).
Os vencimentos contam a partir de hoje, para caírem na janela de 30 dias do painel.

Uso:
    python gerar_notas.py
    python gerar_notas.py --empresas 11222333000181 11222333000262
    python gerar_notas.py --serie 2      (chaves novas, para testar de novo sem limpar o banco)

Os PDFs são impressos pelo Microsoft Edge (ou Chrome) sem janela. Sem navegador, use --sem-pdf.
"""

import argparse
import base64
import hashlib
import html
import os
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
import zipfile
from datetime import date, datetime, time, timedelta
from decimal import ROUND_DOWN, ROUND_HALF_UP, Decimal
from pathlib import Path

NS = 'http://www.portalfiscal.inf.br/nfe'
ET.register_namespace('', NS)

TEXTO_HOMOLOGACAO = 'NF-E EMITIDA EM AMBIENTE DE HOMOLOGACAO - SEM VALOR FISCAL'
CENTAVO = Decimal('0.01')
MARCA_LEIAME = '<!-- gerado por gerar_notas.py -->'


# ---------------------------------------------------------------- documentos

def digito_mod11(numeros, pesos):
    soma = 0
    for numero, peso in zip(numeros, pesos):
        soma += int(numero) * peso
    resto = soma % 11
    if resto < 2:
        return '0'
    return str(11 - resto)


def cnpj(raiz, filial):
    base = raiz + filial
    d1 = digito_mod11(base, [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2])
    d2 = digito_mod11(base + d1, [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2])
    return base + d1 + d2


def cpf(base):
    d1 = digito_mod11(base, [10, 9, 8, 7, 6, 5, 4, 3, 2])
    d2 = digito_mod11(base + d1, [11, 10, 9, 8, 7, 6, 5, 4, 3, 2])
    return base + d1 + d2


def digito_chave(chave43):
    soma = 0
    peso = 2
    for numero in reversed(chave43):
        soma += int(numero) * peso
        peso += 1
        if peso > 9:
            peso = 2
    resto = soma % 11
    if resto < 2:
        return '0'
    return str(11 - resto)


# ---------------------------------------------------------------- dados fictícios

FORNECEDORES = {
    'alfa': {
        'doc': cnpj('45987123', '0001'), 'nome': 'ALFA DISTRIBUIDORA DE AUTOPECAS LTDA', 'fantasia': 'ALFA AUTOPECAS',
        'ie': '114582937110', 'lgr': 'RUA DAS OFICINAS', 'nro': '1200', 'bairro': 'MOOCA',
        'cmun': '3550308', 'mun': 'SAO PAULO', 'uf': 'SP', 'cuf': '35', 'cep': '03105000', 'fone': '1133334444',
    },
    'beta': {
        'doc': cnpj('30754218', '0001'), 'nome': 'BETA LUBRIFICANTES INDUSTRIA E COMERCIO LTDA', 'fantasia': 'BETA LUB',
        'ie': '0623517840081', 'lgr': 'AVENIDA DAS INDUSTRIAS', 'nro': '5400', 'bairro': 'DISTRITO INDUSTRIAL',
        'cmun': '3106200', 'mun': 'BELO HORIZONTE', 'uf': 'MG', 'cuf': '31', 'cep': '30110100', 'fone': '3132225555',
    },
    'gama': {
        'doc': cnpj('61239874', '0001'), 'nome': 'GAMA PNEUS E RODAS LTDA', 'fantasia': 'GAMA PNEUS',
        'ie': '9087654321', 'lgr': 'RUA DOS BORRACHEIROS', 'nro': '880', 'bairro': 'CENTRO',
        'cmun': '4106902', 'mun': 'CURITIBA', 'uf': 'PR', 'cuf': '41', 'cep': '80010010', 'fone': '4130306666',
    },
    'delta': {
        'doc': cnpj('27418536', '0001'), 'nome': 'DELTA FERRAMENTAS E EQUIPAMENTOS LTDA', 'fantasia': 'DELTA FERRAMENTAS',
        'ie': '244123456119', 'lgr': 'RUA DOS MECANICOS', 'nro': '310', 'bairro': 'VILA INDUSTRIAL',
        'cmun': '3509502', 'mun': 'CAMPINAS', 'uf': 'SP', 'cuf': '35', 'cep': '13060000', 'fone': '1932327777',
    },
    'epsilon': {
        'doc': cnpj('52863071', '0001'), 'nome': 'EPSILON BATERIAS AUTOMOTIVAS LTDA', 'fantasia': 'EPSILON BATERIAS',
        'ie': '254876321', 'lgr': 'RUA DOS ELETRICISTAS', 'nro': '455', 'bairro': 'DISTRITO INDUSTRIAL',
        'cmun': '4209102', 'mun': 'JOINVILLE', 'uf': 'SC', 'cuf': '42', 'cep': '89219000', 'fone': '4734348888',
    },
    'pf': {
        'doc': cpf('123456789'), 'nome': 'FORNECEDOR PESSOA FISICA TESTE', 'fantasia': '',
        'ie': '0012345670091', 'lgr': 'ESTRADA RURAL', 'nro': 'S/N', 'bairro': 'ZONA RURAL',
        'cmun': '3170206', 'mun': 'UBERLANDIA', 'uf': 'MG', 'cuf': '31', 'cep': '38400000', 'fone': '3432329999',
    },
}

PRODUTOS = {
    'PAST': ('PAST-1020', 'PASTILHA DE FREIO DIANTEIRA', '87083090', 'JG', '89.90'),
    'FILT': ('FILT-0450', 'FILTRO DE OLEO DO MOTOR', '84212300', 'UN', '24.50'),
    'OLEO': ('OLEO-5W30', 'OLEO LUBRIFICANTE SINTETICO 5W30 1L', '27101932', 'UN', '42.00'),
    'PNEU': ('PNEU-17565', 'PNEU 175/65 R14', '40111000', 'UN', '389.00'),
    'BAT': ('BAT-60AH', 'BATERIA AUTOMOTIVA 60AH', '85071000', 'UN', '520.00'),
    'AMORT': ('AMORT-D300', 'AMORTECEDOR DIANTEIRO', '87088000', 'UN', '215.00'),
    'CHAVE': ('CHAVE-C12', 'JOGO DE CHAVES COMBINADAS 12 PECAS', '82041100', 'JG', '159.90'),
    'VELA': ('VELA-IG04', 'VELA DE IGNICAO', '85111000', 'UN', '18.90'),
    'CORREIA': ('CORR-DT01', 'CORREIA DENTADA', '40103100', 'UN', '76.40'),
}

# vencimentos = dias a partir de hoje; lista vazia = nota à vista, sem <cobr>
NOTAS = [
    {'id': '01', 'arquivo': '01-parcela-unica', 'numero': 48213, 'emit': 'alfa', 'dest': 'A', 'emitida_ha': 1,
     'itens': [('PAST', 4), ('FILT', 10), ('VELA', 16)], 'vencimentos': [7]},
    {'id': '02', 'arquivo': '02-tres-parcelas', 'numero': 10571, 'emit': 'beta', 'dest': 'A', 'emitida_ha': 2,
     'itens': [('OLEO', 60), ('FILT', 25)], 'vencimentos': [5, 15, 25]},
    {'id': '03', 'arquivo': '03-a-vista-sem-duplicatas', 'numero': 3307, 'emit': 'delta', 'dest': 'A', 'emitida_ha': 0,
     'itens': [('CHAVE', 2)], 'vencimentos': []},
    {'id': '04', 'arquivo': '04-empresa-nao-cadastrada', 'numero': 48290, 'emit': 'alfa', 'dest': 'X', 'emitida_ha': 1,
     'itens': [('AMORT', 2), ('PAST', 2)], 'vencimentos': [12]},
    {'id': '05', 'arquivo': '05-segunda-empresa', 'numero': 7745, 'emit': 'gama', 'dest': 'B', 'emitida_ha': 3,
     'itens': [('PNEU', 8)], 'vencimentos': [10, 20]},
    {'id': '06a', 'arquivo': '06a-lote', 'numero': 2210, 'emit': 'epsilon', 'dest': 'B', 'emitida_ha': 2,
     'itens': [('BAT', 3)], 'vencimentos': [14]},
    {'id': '06b', 'arquivo': '06b-lote', 'numero': 48302, 'emit': 'alfa', 'dest': 'A', 'emitida_ha': 0,
     'itens': [('AMORT', 4), ('CORREIA', 3)], 'vencimentos': [9, 23]},
    {'id': '08', 'arquivo': '08-nfce-modelo-65', 'numero': 99120, 'emit': 'delta', 'dest': 'A', 'emitida_ha': 0,
     'itens': [('VELA', 4)], 'vencimentos': [], 'modelo': '65'},
    {'id': '09', 'arquivo': '09-sem-xml', 'numero': 5521, 'emit': 'gama', 'dest': 'A', 'emitida_ha': 1,
     'itens': [('PNEU', 4)], 'vencimentos': [16]},
    {'id': '10', 'arquivo': '10-sem-protocolo', 'numero': 10598, 'emit': 'beta', 'dest': 'B', 'emitida_ha': 0,
     'itens': [('OLEO', 24)], 'vencimentos': [18], 'protocolo': False},
    {'id': '11', 'arquivo': '11-fornecedor-cpf', 'numero': 152, 'emit': 'pf', 'dest': 'A', 'emitida_ha': 4,
     'itens': [('CORREIA', 5)], 'vencimentos': [21]},
]

ARQUIVO_CANCELAMENTO = '07-cancelamento-da-nota-01'
ARQUIVO_ZIP = '06-lote-duas-notas.zip'

# Cada item vira uma pasta = um e-mail. ('01', 'xml') = XML da nota 01; ('zip',) = o .zip do lote.
EMAILS = [
    {'pasta': 'email-1-parcela-unica', 'assunto': 'NF-e 48213 - Alfa Autopecas',
     'anexos': [('01', 'xml'), ('01', 'pdf')]},
    {'pasta': 'email-2-tres-parcelas', 'assunto': 'NF-e 10571 - Beta Lubrificantes',
     'anexos': [('02', 'xml'), ('02', 'pdf')]},
    {'pasta': 'email-3-tres-notas-no-mesmo-email', 'assunto': 'Notas da semana',
     'anexos': [('03', 'xml'), ('03', 'pdf'), ('04', 'xml'), ('04', 'pdf'), ('05', 'xml'), ('05', 'pdf')]},
    {'pasta': 'email-4-zip', 'assunto': 'Lote de notas (zip)',
     'anexos': [('zip',)]},
    {'pasta': 'email-5-devem-ser-ignorados', 'assunto': 'Cancelamento, NFC-e e DANFE sem XML',
     'anexos': [('07', 'xml'), ('08', 'xml'), ('09', 'pdf')]},
    {'pasta': 'email-6-sem-protocolo-e-cpf', 'assunto': 'NF-e 10598 e 152',
     'anexos': [('10', 'xml'), ('10', 'pdf'), ('11', 'xml'), ('11', 'pdf')]},
    {'pasta': 'email-7-nota-repetida', 'assunto': 'Reenvio NF-e 48213',
     'anexos': [('01', 'xml'), ('01', 'pdf')]},
]


def montar_destinatarios(args):
    comum = {'lgr': 'AVENIDA DOS AUTOMOVEIS', 'bairro': 'CENTRO', 'cmun': '3550308', 'mun': 'SAO PAULO',
             'uf': 'SP', 'cuf': '35', 'cep': '01001000', 'fone': '1130301010'}
    dados = {
        'A': {'doc': args.empresas[0], 'nome': 'AUTOPECAS PILOTO MATRIZ', 'nro': '100', 'ie': '110000000111'},
        'B': {'doc': args.empresas[1], 'nome': 'AUTOPECAS PILOTO FILIAL 2', 'nro': '200', 'ie': '110000000222'},
        'X': {'doc': args.nao_cadastrada, 'nome': 'AUTOPECAS PILOTO FILIAL 3', 'nro': '300', 'ie': '110000000333'},
    }
    for destinatario in dados.values():
        destinatario.update(comum)
    return dados


# ---------------------------------------------------------------- cálculo

def dinheiro(valor):
    return valor.quantize(CENTAVO, rounding=ROUND_HALF_UP)


def parcelar(total, quantidade):
    """Divide em partes iguais; os centavos que sobram vão para a última parcela."""
    if quantidade == 0:
        return []
    parte = (total / quantidade).quantize(CENTAVO, rounding=ROUND_DOWN)
    valores = [parte] * quantidade
    valores[-1] = total - parte * (quantidade - 1)
    return valores


def calcular_nota(nota, emitente, destinatario, hoje, serie):
    modelo = nota.get('modelo', '55')
    interestadual = emitente['uf'] != destinatario['uf']
    if interestadual:
        cfop = '6102'
        aliquota = Decimal('12.00')
    else:
        cfop = '5102'
        aliquota = Decimal('18.00')

    itens = []
    for codigo, quantidade in nota['itens']:
        cprod, descricao, ncm, unidade, unitario = PRODUTOS[codigo]
        unitario = Decimal(unitario)
        valor = dinheiro(unitario * quantidade)
        itens.append({
            'cprod': cprod, 'descricao': descricao, 'ncm': ncm, 'unidade': unidade, 'quantidade': quantidade,
            'unitario': unitario, 'valor': valor, 'cfop': cfop, 'aliquota': aliquota,
            'icms': dinheiro(valor * aliquota / 100),
            'pis': dinheiro(valor * Decimal('0.0165')),
            'cofins': dinheiro(valor * Decimal('0.076')),
        })

    total = sum((item['valor'] for item in itens), Decimal('0.00'))
    valores = parcelar(total, len(nota['vencimentos']))
    parcelas = []
    for indice, dias in enumerate(nota['vencimentos']):
        parcelas.append({'numero': str(indice + 1).zfill(3), 'vencimento': hoje + timedelta(days=dias),
                         'valor': valores[indice]})

    emissao = datetime.combine(hoje - timedelta(days=nota['emitida_ha']), time(9, 0))
    emissao = emissao + timedelta(minutes=nota['numero'] % 50)

    doc_chave = emitente['doc'].zfill(14)
    numero_nf = str(nota['numero']).zfill(9)
    codigo_numerico = str((nota['numero'] * 7919 + serie * 104729) % 100000000).zfill(8)
    if codigo_numerico == numero_nf[-8:]:
        codigo_numerico = str((int(codigo_numerico) + 1) % 100000000).zfill(8)
    chave43 = (emitente['cuf'] + emissao.strftime('%y%m') + doc_chave + modelo + str(serie).zfill(3)
               + numero_nf + '1' + codigo_numerico)
    chave = chave43 + digito_chave(chave43)

    recebimento = emissao + timedelta(minutes=2)
    protocolo = '1' + emitente['cuf'] + emissao.strftime('%y') + str(nota['numero']).zfill(10)
    digest = base64.b64encode(hashlib.sha1(chave.encode('ascii')).digest()).decode('ascii')

    return {
        'nota': nota, 'modelo': modelo, 'serie': serie, 'emitente': emitente, 'destinatario': destinatario,
        'interestadual': interestadual, 'itens': itens, 'total': total, 'parcelas': parcelas,
        'emissao': emissao, 'recebimento': recebimento, 'chave': chave, 'codigo_numerico': codigo_numerico,
        'protocolo': protocolo, 'digest': digest, 'com_protocolo': nota.get('protocolo', True),
        'icms': sum((item['icms'] for item in itens), Decimal('0.00')),
        'pis': sum((item['pis'] for item in itens), Decimal('0.00')),
        'cofins': sum((item['cofins'] for item in itens), Decimal('0.00')),
    }


# ---------------------------------------------------------------- XML

def el(pai, tag, texto=None, **atributos):
    elemento = ET.SubElement(pai, '{' + NS + '}' + tag, atributos)
    if texto is not None:
        elemento.text = str(texto)
    return elemento


def raiz(tag, **atributos):
    return ET.Element('{' + NS + '}' + tag, atributos)


def data_hora(valor):
    return valor.strftime('%Y-%m-%dT%H:%M:%S') + '-03:00'


def doc_xml(pai, documento):
    if len(documento) == 11:
        el(pai, 'CPF', documento)
    else:
        el(pai, 'CNPJ', documento)


def endereco_xml(pai, tag, pessoa):
    endereco = el(pai, tag)
    el(endereco, 'xLgr', pessoa['lgr'])
    el(endereco, 'nro', pessoa['nro'])
    el(endereco, 'xBairro', pessoa['bairro'])
    el(endereco, 'cMun', pessoa['cmun'])
    el(endereco, 'xMun', pessoa['mun'])
    el(endereco, 'UF', pessoa['uf'])
    el(endereco, 'CEP', pessoa['cep'])
    el(endereco, 'cPais', '1058')
    el(endereco, 'xPais', 'BRASIL')
    el(endereco, 'fone', pessoa['fone'])


def montar_nfe(pai, dados):
    nota = dados['nota']
    emitente = dados['emitente']
    destinatario = dados['destinatario']
    nfce = dados['modelo'] == '65'

    nfe = el(pai, 'NFe')
    inf = el(nfe, 'infNFe', versao='4.00', Id='NFe' + dados['chave'])

    ide = el(inf, 'ide')
    el(ide, 'cUF', emitente['cuf'])
    el(ide, 'cNF', dados['codigo_numerico'])
    el(ide, 'natOp', 'VENDA DE MERCADORIA')
    el(ide, 'mod', dados['modelo'])
    el(ide, 'serie', dados['serie'])
    el(ide, 'nNF', nota['numero'])
    el(ide, 'dhEmi', data_hora(dados['emissao']))
    if not nfce:
        el(ide, 'dhSaiEnt', data_hora(dados['emissao']))
    el(ide, 'tpNF', '1')
    if dados['interestadual']:
        el(ide, 'idDest', '2')
    else:
        el(ide, 'idDest', '1')
    el(ide, 'cMunFG', emitente['cmun'])
    if nfce:
        el(ide, 'tpImp', '4')
    else:
        el(ide, 'tpImp', '1')
    el(ide, 'tpEmis', '1')
    el(ide, 'cDV', dados['chave'][-1])
    el(ide, 'tpAmb', '2')
    el(ide, 'finNFe', '1')
    if nfce:
        el(ide, 'indFinal', '1')
        el(ide, 'indPres', '1')
    else:
        el(ide, 'indFinal', '0')
        el(ide, 'indPres', '9')
        el(ide, 'indIntermed', '0')
    el(ide, 'procEmi', '0')
    el(ide, 'verProc', 'PREVISIO-TESTE 1.0')

    emit = el(inf, 'emit')
    doc_xml(emit, emitente['doc'])
    el(emit, 'xNome', emitente['nome'])
    if emitente['fantasia'] != '':
        el(emit, 'xFant', emitente['fantasia'])
    endereco_xml(emit, 'enderEmit', emitente)
    el(emit, 'IE', emitente['ie'])
    el(emit, 'CRT', '3')

    dest = el(inf, 'dest')
    doc_xml(dest, destinatario['doc'])
    el(dest, 'xNome', TEXTO_HOMOLOGACAO)
    endereco_xml(dest, 'enderDest', destinatario)
    el(dest, 'indIEDest', '1')
    el(dest, 'IE', destinatario['ie'])

    for indice, item in enumerate(dados['itens']):
        det = el(inf, 'det', nItem=str(indice + 1))
        prod = el(det, 'prod')
        el(prod, 'cProd', item['cprod'])
        el(prod, 'cEAN', 'SEM GTIN')
        el(prod, 'xProd', item['descricao'])
        el(prod, 'NCM', item['ncm'])
        el(prod, 'CFOP', item['cfop'])
        el(prod, 'uCom', item['unidade'])
        el(prod, 'qCom', '%.4f' % item['quantidade'])
        el(prod, 'vUnCom', '%.10f' % item['unitario'])
        el(prod, 'vProd', item['valor'])
        el(prod, 'cEANTrib', 'SEM GTIN')
        el(prod, 'uTrib', item['unidade'])
        el(prod, 'qTrib', '%.4f' % item['quantidade'])
        el(prod, 'vUnTrib', '%.10f' % item['unitario'])
        el(prod, 'indTot', '1')

        imposto = el(det, 'imposto')
        icms = el(el(imposto, 'ICMS'), 'ICMS00')
        el(icms, 'orig', '0')
        el(icms, 'CST', '00')
        el(icms, 'modBC', '3')
        el(icms, 'vBC', item['valor'])
        el(icms, 'pICMS', item['aliquota'])
        el(icms, 'vICMS', item['icms'])
        pis = el(el(imposto, 'PIS'), 'PISAliq')
        el(pis, 'CST', '01')
        el(pis, 'vBC', item['valor'])
        el(pis, 'pPIS', '1.65')
        el(pis, 'vPIS', item['pis'])
        cofins = el(el(imposto, 'COFINS'), 'COFINSAliq')
        el(cofins, 'CST', '01')
        el(cofins, 'vBC', item['valor'])
        el(cofins, 'pCOFINS', '7.60')
        el(cofins, 'vCOFINS', item['cofins'])

    tot = el(el(inf, 'total'), 'ICMSTot')
    zero = '0.00'
    campos_total = [
        ('vBC', dados['total']), ('vICMS', dados['icms']), ('vICMSDeson', zero), ('vFCP', zero),
        ('vBCST', zero), ('vST', zero), ('vFCPST', zero), ('vFCPSTRet', zero), ('vProd', dados['total']),
        ('vFrete', zero), ('vSeg', zero), ('vDesc', zero), ('vII', zero), ('vIPI', zero), ('vIPIDevol', zero),
        ('vPIS', dados['pis']), ('vCOFINS', dados['cofins']), ('vOutro', zero), ('vNF', dados['total']),
    ]
    for nome, valor in campos_total:
        el(tot, nome, valor)

    el(el(inf, 'transp'), 'modFrete', '9')

    if len(dados['parcelas']) > 0:
        cobr = el(inf, 'cobr')
        fat = el(cobr, 'fat')
        el(fat, 'nFat', nota['numero'])
        el(fat, 'vOrig', dados['total'])
        el(fat, 'vDesc', zero)
        el(fat, 'vLiq', dados['total'])
        for parcela in dados['parcelas']:
            dup = el(cobr, 'dup')
            el(dup, 'nDup', parcela['numero'])
            el(dup, 'dVenc', parcela['vencimento'].isoformat())
            el(dup, 'vDup', parcela['valor'])

    det_pag = el(el(inf, 'pag'), 'detPag')
    if len(dados['parcelas']) > 0:
        el(det_pag, 'indPag', '1')
        el(det_pag, 'tPag', '15')
    else:
        el(det_pag, 'indPag', '0')
        el(det_pag, 'tPag', '01')
    el(det_pag, 'vPag', dados['total'])

    el(el(inf, 'infAdic'), 'infCpl', texto_adicional(dados))


def texto_adicional(dados):
    texto = 'DOCUMENTO DE TESTE GERADO PARA O PREVISIO. NAO TEM VALOR FISCAL.'
    quantidade = len(dados['parcelas'])
    if quantidade == 1:
        texto += ' PAGAMENTO EM 1 PARCELA POR BOLETO.'
    elif quantidade > 1:
        texto += ' PAGAMENTO EM ' + str(quantidade) + ' PARCELAS POR BOLETO.'
    else:
        texto += ' PAGAMENTO A VISTA.'
    return texto


def xml_nfe(dados):
    if not dados['com_protocolo']:
        # Só a tag NFe, como alguns sistemas mandam: o Previsio tira a chave do atributo Id
        suporte = ET.Element('suporte')
        montar_nfe(suporte, dados)
        return serializar(suporte[0])

    documento = raiz('nfeProc', versao='4.00')
    montar_nfe(documento, dados)
    prot = el(documento, 'protNFe', versao='4.00')
    inf = el(prot, 'infProt', Id='ID' + dados['protocolo'])
    el(inf, 'tpAmb', '2')
    el(inf, 'verAplic', 'PREVISIO-TESTE')
    el(inf, 'chNFe', dados['chave'])
    el(inf, 'dhRecbto', data_hora(dados['recebimento']))
    el(inf, 'nProt', dados['protocolo'])
    el(inf, 'digVal', dados['digest'])
    el(inf, 'cStat', '100')
    el(inf, 'xMotivo', 'Autorizado o uso da NF-e')
    return serializar(documento)


def xml_cancelamento(dados, quando):
    emitente = dados['emitente']
    chave = dados['chave']
    protocolo_evento = '1' + emitente['cuf'] + quando.strftime('%y') + '9' + str(dados['nota']['numero']).zfill(9)

    documento = raiz('procEventoNFe', versao='1.00')
    evento = el(documento, 'evento', versao='1.00')
    inf = el(evento, 'infEvento', Id='ID110111' + chave + '01')
    el(inf, 'cOrgao', emitente['cuf'])
    el(inf, 'tpAmb', '2')
    doc_xml(inf, emitente['doc'])
    el(inf, 'chNFe', chave)
    el(inf, 'dhEvento', data_hora(quando))
    el(inf, 'tpEvento', '110111')
    el(inf, 'nSeqEvento', '1')
    el(inf, 'verEvento', '1.00')
    det = el(inf, 'detEvento', versao='1.00')
    el(det, 'descEvento', 'Cancelamento')
    el(det, 'nProt', dados['protocolo'])
    el(det, 'xJust', 'NOTA EMITIDA COM ERRO DE DIGITACAO - DOCUMENTO DE TESTE')

    retorno = el(el(documento, 'retEvento', versao='1.00'), 'infEvento')
    el(retorno, 'tpAmb', '2')
    el(retorno, 'verAplic', 'PREVISIO-TESTE')
    el(retorno, 'cOrgao', emitente['cuf'])
    el(retorno, 'cStat', '135')
    el(retorno, 'xMotivo', 'Evento registrado e vinculado a NF-e')
    el(retorno, 'chNFe', chave)
    el(retorno, 'tpEvento', '110111')
    el(retorno, 'xEvento', 'Cancelamento registrado')
    el(retorno, 'nSeqEvento', '1')
    el(retorno, 'dhRegEvento', data_hora(quando + timedelta(minutes=1)))
    el(retorno, 'nProt', protocolo_evento)
    return serializar(documento)


def serializar(elemento):
    ET.indent(elemento, space='  ')
    texto = ET.tostring(elemento, encoding='unicode')
    return '<?xml version="1.0" encoding="UTF-8"?>\n' + texto + '\n'


# ---------------------------------------------------------------- DANFE (HTML -> PDF)

def br_dinheiro(valor):
    texto = '{:,.2f}'.format(valor)
    return texto.replace(',', '#').replace('.', ',').replace('#', '.')


def br_quantidade(valor):
    return br_dinheiro(Decimal(valor)).replace(',00', '')


def br_data(valor):
    return valor.strftime('%d/%m/%Y')


def br_doc(documento):
    if len(documento) == 11:
        return documento[0:3] + '.' + documento[3:6] + '.' + documento[6:9] + '-' + documento[9:11]
    return (documento[0:2] + '.' + documento[2:5] + '.' + documento[5:8] + '/' + documento[8:12]
            + '-' + documento[12:14])


def br_chave(chave):
    grupos = []
    for inicio in range(0, 44, 4):
        grupos.append(chave[inicio:inicio + 4])
    return ' '.join(grupos)


def br_numero_nf(numero):
    texto = str(numero).zfill(9)
    return texto[0:3] + '.' + texto[3:6] + '.' + texto[6:9]


def br_cep(cep):
    return cep[0:5] + '-' + cep[5:8]


def campo(rotulo, valor, largura=None, classe=''):
    estilo = ''
    if largura is not None:
        estilo = ' style="width:' + largura + '"'
    return ('<td' + estilo + '><span class="rotulo">' + html.escape(rotulo) + '</span><span class="valor '
            + classe + '">' + html.escape(str(valor)) + '</span></td>')


def linha(*campos):
    return '<table><tr>' + ''.join(campos) + '</tr></table>'


CSS_DANFE = """
@page { size: A4; margin: 8mm; }
* { box-sizing: border-box; }
body { font-family: Arial, Helvetica, sans-serif; font-size: 8pt; color: #000; margin: 0; }
.aviso { border: 1.5px solid #b00020; color: #b00020; text-align: center; font-weight: bold;
         font-size: 9pt; padding: 4px; margin-bottom: 4px; }
.marca { position: fixed; top: 40%; left: -10%; right: -10%; text-align: center; font-size: 64pt;
         font-weight: bold; color: rgba(176, 0, 32, 0.13); transform: rotate(-28deg); z-index: 10; }
table { width: 100%; border-collapse: collapse; margin-bottom: 2px; table-layout: fixed; }
td, th { border: 1px solid #000; padding: 2px 4px; vertical-align: top; overflow: hidden; }
.rotulo { display: block; font-size: 6pt; text-transform: uppercase; color: #333; }
.valor { display: block; font-size: 8.5pt; font-weight: bold; min-height: 11px; }
.secao { font-size: 7pt; font-weight: bold; text-transform: uppercase; margin: 5px 0 1px; }
.titulo { text-align: center; vertical-align: middle; }
.titulo h1 { font-size: 17pt; margin: 2px 0 0; letter-spacing: 1px; }
.emitente-nome { font-size: 11pt; font-weight: bold; margin: 2px 0 4px; }
.chave { font-family: 'Courier New', monospace; font-size: 9.5pt; letter-spacing: 0.3px; }
.barras { height: 36px; border: 1px dashed #888; color: #777; font-size: 7pt; text-align: center;
          line-height: 36px; margin-bottom: 3px; }
.caixa { display: inline-block; border: 1px solid #000; padding: 0 5px; font-weight: bold; margin-left: 4px; }
.produtos th { font-size: 6pt; background: #eee; text-transform: uppercase; font-weight: bold; }
.produtos td { font-size: 7.5pt; }
.num { text-align: right; }
.dup { display: inline-block; border: 1px solid #000; padding: 2px 8px; margin: 0 4px 3px 0; }
.dup b { display: block; }
.vazio { color: #555; font-style: italic; padding: 3px 0; }
.adicionais { height: 70px; }
"""


def danfe_html(dados):
    nota = dados['nota']
    emitente = dados['emitente']
    destinatario = dados['destinatario']
    numero = br_numero_nf(nota['numero'])
    serie = str(dados['serie']).zfill(3)
    partes = []

    partes.append('<!doctype html><html lang="pt-BR"><head><meta charset="utf-8"><title>DANFE '
                  + numero + '</title><style>' + CSS_DANFE + '</style></head><body>')
    partes.append('<div class="marca">SEM VALOR FISCAL</div>')
    partes.append('<div class="aviso">DOCUMENTO DE TESTE DO PREVISIO &mdash; NF-e emitida em ambiente de '
                  'homologação &mdash; SEM VALOR FISCAL</div>')

    # Canhoto
    partes.append('<table><tr><td colspan="2"><span class="rotulo">Recebemos de ' + html.escape(emitente['nome'])
                  + ' os produtos constantes da nota fiscal eletrônica indicada ao lado. Emissão: '
                  + br_data(dados['emissao']) + ' &mdash; Valor total: R$ ' + br_dinheiro(dados['total'])
                  + '</span></td><td rowspan="2" class="titulo" style="width:18%"><b>NF-e</b><br>Nº '
                  + numero + '<br>SÉRIE ' + serie + '</td></tr><tr>'
                  + campo('Data de recebimento', '', '25%')
                  + campo('Identificação e assinatura do recebedor', '') + '</tr></table>')

    # Cabeçalho
    if dados['com_protocolo']:
        protocolo = dados['protocolo'] + ' - ' + dados['recebimento'].strftime('%d/%m/%Y %H:%M:%S')
    else:
        protocolo = 'XML ENVIADO SEM PROTOCOLO (TESTE)'
    endereco = (emitente['lgr'] + ', ' + emitente['nro'] + ' - ' + emitente['bairro'] + '<br>'
                + emitente['mun'] + ' - ' + emitente['uf'] + ' - CEP ' + br_cep(emitente['cep'])
                + '<br>Fone: ' + emitente['fone'])
    partes.append('<table><tr>'
                  '<td style="width:38%"><span class="rotulo">Identificação do emitente</span>'
                  '<div class="emitente-nome">' + html.escape(emitente['nome']) + '</div>' + endereco + '</td>'
                  '<td class="titulo" style="width:17%"><h1>DANFE</h1>Documento Auxiliar da<br>Nota Fiscal '
                  'Eletrônica<br><br>0 - Entrada<br>1 - Saída <span class="caixa">1</span><br><br><b>Nº '
                  + numero + '</b><br><b>SÉRIE ' + serie + '</b><br>Folha 1/1</td>'
                  '<td><div class="barras">código de barras omitido (documento de teste)</div>'
                  '<span class="rotulo">Chave de acesso</span><span class="valor chave">'
                  + br_chave(dados['chave']) + '</span><br>Chave fictícia: não existe na SEFAZ.</td>'
                  '</tr></table>')
    partes.append(linha(campo('Natureza da operação', 'VENDA DE MERCADORIA', '55%'),
                        campo('Protocolo de autorização de uso', protocolo)))
    partes.append(linha(campo('Inscrição estadual', emitente['ie'], '34%'),
                        campo('Insc. estadual do subst. tributário', '', '33%'),
                        campo('CNPJ / CPF', br_doc(emitente['doc']))))

    # Destinatário
    partes.append('<div class="secao">Destinatário / remetente</div>')
    partes.append(linha(campo('Nome / razão social', TEXTO_HOMOLOGACAO, '55%'),
                        campo('CNPJ / CPF', br_doc(destinatario['doc']), '25%'),
                        campo('Data da emissão', br_data(dados['emissao']))))
    partes.append(linha(campo('Endereço', destinatario['lgr'] + ', ' + destinatario['nro'], '45%'),
                        campo('Bairro / distrito', destinatario['bairro'], '20%'),
                        campo('CEP', br_cep(destinatario['cep']), '15%'),
                        campo('Data da saída / entrada', br_data(dados['emissao']))))
    partes.append(linha(campo('Município', destinatario['mun'], '45%'),
                        campo('UF', destinatario['uf'], '8%'),
                        campo('Inscrição estadual', destinatario['ie'], '27%'),
                        campo('Hora da saída', dados['emissao'].strftime('%H:%M:%S'))))

    # Duplicatas
    partes.append('<div class="secao">Fatura / duplicatas</div>')
    if len(dados['parcelas']) == 0:
        partes.append('<div class="vazio">Pagamento à vista &mdash; nota sem duplicatas.</div>')
    else:
        caixas = []
        for parcela in dados['parcelas']:
            caixas.append('<span class="dup">Nº ' + parcela['numero'] + '<b>Venc. ' + br_data(parcela['vencimento'])
                          + '</b>R$ ' + br_dinheiro(parcela['valor']) + '</span>')
        partes.append('<div>' + ''.join(caixas) + '</div>')

    # Impostos
    partes.append('<div class="secao">Cálculo do imposto</div>')
    partes.append(linha(campo('Base de cálculo do ICMS', br_dinheiro(dados['total'])),
                        campo('Valor do ICMS', br_dinheiro(dados['icms'])),
                        campo('Base de cálc. ICMS ST', '0,00'),
                        campo('Valor do ICMS ST', '0,00'),
                        campo('Valor total dos produtos', br_dinheiro(dados['total']))))
    partes.append(linha(campo('Valor do frete', '0,00'), campo('Valor do seguro', '0,00'),
                        campo('Desconto', '0,00'), campo('Outras despesas', '0,00'),
                        campo('Valor do IPI', '0,00'),
                        campo('Valor total da nota', br_dinheiro(dados['total']))))

    partes.append('<div class="secao">Transportador / volumes transportados</div>')
    partes.append(linha(campo('Razão social', '', '60%'),
                        campo('Frete por conta', '9 - Sem ocorrência de transporte')))

    # Produtos
    partes.append('<div class="secao">Dados dos produtos / serviços</div>')
    partes.append('<table class="produtos"><tr>'
                  '<th style="width:11%">Código</th><th style="width:29%">Descrição</th>'
                  '<th style="width:8%">NCM</th><th style="width:5%">CST</th><th style="width:6%">CFOP</th>'
                  '<th style="width:5%">Un.</th><th style="width:6%">Qtd.</th><th style="width:9%">V. unit.</th>'
                  '<th style="width:9%">V. total</th><th style="width:7%">V. ICMS</th>'
                  '<th style="width:5%">Alíq.</th></tr>')
    for item in dados['itens']:
        partes.append('<tr><td>' + item['cprod'] + '</td><td>' + html.escape(item['descricao']) + '</td><td>'
                      + item['ncm'] + '</td><td>000</td><td>' + item['cfop'] + '</td><td>' + item['unidade']
                      + '</td><td class="num">' + br_quantidade(item['quantidade']) + '</td><td class="num">'
                      + br_dinheiro(item['unitario']) + '</td><td class="num">' + br_dinheiro(item['valor'])
                      + '</td><td class="num">' + br_dinheiro(item['icms']) + '</td><td class="num">'
                      + br_quantidade(item['aliquota']) + '%</td></tr>')
    partes.append('</table>')

    partes.append('<div class="secao">Dados adicionais</div>')
    partes.append('<table><tr><td class="adicionais" style="width:65%"><span class="rotulo">Informações '
                  'complementares</span>' + html.escape(texto_adicional(dados)) + '</td>'
                  '<td><span class="rotulo">Reservado ao fisco</span></td></tr></table>')
    partes.append('</body></html>')
    return ''.join(partes)


def achar_navegador(informado):
    candidatos = []
    if informado:
        candidatos.append(informado)
    candidatos.extend([
        r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',
        r'C:\Program Files\Microsoft\Edge\Application\msedge.exe',
        r'C:\Program Files\Google\Chrome\Application\chrome.exe',
        r'C:\Program Files (x86)\Google\Chrome\Application\chrome.exe',
    ])
    for candidato in candidatos:
        if Path(candidato).exists():
            return candidato
    return None


def encerrar(processo):
    # O navegador abre processos filhos: no Windows, taskkill /T derruba a árvore toda
    if os.name == 'nt':
        subprocess.run(['taskkill', '/T', '/F', '/PID', str(processo.pid)],
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=False)
    else:
        processo.kill()
    processo.wait()


def imprimir_pdf(navegador, arquivo_html, arquivo_pdf, pasta_perfis):
    # O modo sem janela às vezes trava; cada tentativa usa um perfil novo e espera no máximo 45 s
    for tentativa in range(1, 4):
        perfil = pasta_perfis / (arquivo_pdf.stem + '-' + str(tentativa))
        comando = [
            navegador, '--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check',
            '--disable-extensions', '--user-data-dir=' + str(perfil), '--no-pdf-header-footer',
            '--print-to-pdf-no-header', '--print-to-pdf=' + str(arquivo_pdf), arquivo_html.as_uri(),
        ]
        processo = subprocess.Popen(comando, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        try:
            processo.wait(timeout=45)
        except subprocess.TimeoutExpired:
            encerrar(processo)
        if arquivo_pdf.exists() and arquivo_pdf.stat().st_size > 0:
            return
    raise RuntimeError('O navegador não gerou ' + arquivo_pdf.name + ' depois de 3 tentativas')


# ---------------------------------------------------------------- LEIAME

def resultado_esperado(dados, empresas):
    nota = dados['nota']
    if nota['dest'] == 'X':
        return ('Pendente: "Nenhuma empresa cadastrada com o CNPJ ' + dados['destinatario']['doc']
                + '". Não vai para o Bling (nem com Reprocessar depois de cadastrar o CNPJ).')
    if len(dados['parcelas']) == 0:
        return 'Pendente: "A nota não traz duplicatas (vencimentos)". Não vai para o Bling.'
    quantidade = len(dados['parcelas'])
    if quantidade == 1:
        texto = '1 conta a pagar'
    else:
        texto = str(quantidade) + ' contas a pagar'
    return texto + ' na ' + empresas[nota['dest']]


def gerar_leiame(geradas, cancelada, args, hoje, sem_pdf):
    empresas = {'A': 'matriz', 'B': 'filial 2'}
    linhas = [MARCA_LEIAME, '# Notas fiscais de teste',
              '',
              'Geradas em ' + br_data(hoje) + ' por `gerar_notas.py`, série ' + str(args.serie) + '.',
              'Tudo fictício, em ambiente de homologação e **sem valor fiscal**. Cada pasta é um e-mail.',
              '']
    if sem_pdf:
        linhas.extend(['> PDFs não gerados (`--sem-pdf` ou navegador não encontrado).', ''])

    linhas.extend([
        '## Antes de enviar', '',
        '1. No painel, como admin, cadastre o cliente (a rede) com a caixa IMAP na tela **Clientes**.',
        '2. Na tela **Empresas**, cadastre as duas empresas que recebem as notas:', '',
        '   | Empresa | CNPJ |', '   |---|---|',
        '   | Autopeças piloto matriz | `' + br_doc(args.empresas[0]) + '` |',
        '   | Autopeças piloto filial 2 | `' + br_doc(args.empresas[1]) + '` |', '',
        '   **Não** cadastre `' + br_doc(args.nao_cadastrada) + '`: a nota 04 testa justamente isso.',
        '3. Conecte o Bling nas duas. Sem isso as notas ficam como "Bling desconectado" em Notas pendentes.',
        '4. **Não abra os e-mails na caixa dedicada** antes de rodar a leitura: o Previsio só lê os não lidos.',
        '   Se abrir, marque como não lido. Confira também a pasta de spam.',
        '', '## E-mails', '',
        '| Pasta | Assunto sugerido | O que testa | Resultado esperado |', '|---|---|---|---|',
    ])

    descricoes = {
        'email-1-parcela-unica': ('Nota com uma duplicata, XML + DANFE', resultado_esperado(geradas['01'], empresas)),
        'email-2-tres-parcelas': ('Três duplicatas; os centavos sobram na última',
                                  resultado_esperado(geradas['02'], empresas)),
        'email-3-tres-notas-no-mesmo-email': (
            'Três notas num e-mail só',
            '03: ' + resultado_esperado(geradas['03'], empresas) + '<br>04: '
            + resultado_esperado(geradas['04'], empresas) + '<br>05: '
            + resultado_esperado(geradas['05'], empresas)),
        'email-4-zip': ('Duas notas dentro de um .zip (com os PDFs junto)',
                        '06a: ' + resultado_esperado(geradas['06a'], empresas) + '<br>06b: '
                        + resultado_esperado(geradas['06b'], empresas)),
        'email-5-devem-ser-ignorados': (
            'Evento de cancelamento da nota 01, NFC-e (modelo 65) e DANFE sem XML',
            'Nada é registrado. A nota 01 continua lançada: o Previsio não trata cancelamento.'),
        'email-6-sem-protocolo-e-cpf': (
            '10: XML só com a tag `NFe` (sem `nfeProc`)<br>11: fornecedor pessoa física (CPF)',
            '10: ' + resultado_esperado(geradas['10'], empresas) + '<br>11: '
            + resultado_esperado(geradas['11'], empresas) + ', contato do tipo F'),
        'email-7-nota-repetida': ('Reenvio da nota 01',
                                  'Nada novo. No log: "1 já existentes".'),
    }
    for email in EMAILS:
        oque, resultado = descricoes[email['pasta']]
        linhas.append('| `' + email['pasta'] + '` | ' + email['assunto'] + ' | ' + oque + ' | ' + resultado + ' |')

    linhas.extend([
        '', '## Depois de enviar', '',
        'Rode os jobs na ordem (Swagger, como admin) ou espere o agendamento:', '',
        '1. `POST /api/jobs/leitura-email/executar`',
        '2. `POST /api/jobs/lancamento/executar`',
        '3. `POST /api/jobs/sincronizacao/executar` (traz as contas do Bling para o painel)',
        '', '## Conferência', '',
    ])

    lancadas = []
    pendentes = []
    for chave_nota in ['01', '02', '03', '04', '05', '06a', '06b', '10', '11']:
        dados = geradas[chave_nota]
        if dados['nota']['dest'] == 'X' or len(dados['parcelas']) == 0:
            pendentes.append(dados)
        else:
            lancadas.append(dados)

    total_contas = sum(len(dados['parcelas']) for dados in lancadas)
    linhas.extend([
        '- **Notas registradas:** 9 (01, 02, 03, 04, 05, 06a, 06b, 10 e 11).',
        '- **Notas pendentes:** 03 (sem duplicatas) e 04 (empresa não cadastrada).',
        '- **Contas a pagar no Bling:** ' + str(total_contas) + ', todas com a chave da NF-e no histórico:',
        '',
        '| Nota | Fornecedor | Empresa | Nº do documento | Vencimento | Valor (R$) |', '|---|---|---|---|---|---|',
    ])
    for dados in lancadas:
        quantidade = len(dados['parcelas'])
        for parcela in dados['parcelas']:
            documento = str(dados['nota']['numero'])
            if quantidade > 1:
                documento = documento + '/' + parcela['numero'].lstrip('0')
            linhas.append('| ' + dados['nota']['id'] + ' | ' + dados['emitente']['nome'] + ' | '
                          + empresas[dados['nota']['dest']] + ' | ' + documento + ' | '
                          + br_data(parcela['vencimento']) + ' | ' + br_dinheiro(parcela['valor']) + ' |')

    linhas.extend(['', '| Nota pendente | Fornecedor | Valor (R$) |', '|---|---|---|'])
    for dados in pendentes:
        linhas.append('| ' + dados['nota']['id'] + ' | ' + dados['emitente']['nome'] + ' | '
                      + br_dinheiro(dados['total']) + ' |')

    por_empresa = {'A': Decimal('0.00'), 'B': Decimal('0.00')}
    for dados in lancadas:
        por_empresa[dados['nota']['dest']] += dados['total']
    linhas.extend([
        '',
        '**Painel (30 dias), depois da sincronização:** matriz R$ ' + br_dinheiro(por_empresa['A'])
        + ', filial 2 R$ ' + br_dinheiro(por_empresa['B']) + ', total R$ '
        + br_dinheiro(por_empresa['A'] + por_empresa['B'])
        + '. Somam-se a isso as contas que já existirem no Bling dessas empresas.',
    ])

    linhas.extend([
        '', '## Testar de novo', '',
        'As chaves de acesso não mudam dentro do mesmo mês: reenviar os mesmos arquivos dá "já existentes".',
        'Para um teste limpo, gere chaves novas com `python gerar_notas.py --serie ' + str(args.serie + 1) + '`.',
        '',
        'Nota cancelada pelo arquivo 07: ' + cancelada['nota']['id'] + ' (chave `' + cancelada['chave'] + '`).',
        '',
    ])
    return '\n'.join(linhas)


# ---------------------------------------------------------------- principal

def somente_digitos(texto):
    return ''.join(caractere for caractere in texto if caractere.isdigit())


def ler_argumentos():
    parser = argparse.ArgumentParser(description='Gera NF-e de teste (XML, DANFE em PDF e .zip) para o Previsio.')
    parser.add_argument('--empresas', nargs=2, metavar=('CNPJ_MATRIZ', 'CNPJ_FILIAL'),
                        default=[cnpj('11222333', '0001'), cnpj('11222333', '0002')],
                        help='CNPJs das duas empresas cadastradas no Previsio que recebem as notas')
    parser.add_argument('--nao-cadastrada', default=cnpj('11222333', '0003'),
                        help='CNPJ que NÃO está cadastrado (nota 04)')
    parser.add_argument('--serie', type=int, default=1, help='série das notas; mude para gerar chaves novas')
    parser.add_argument('--saida', default=str(Path(__file__).resolve().parent / 'arquivos'),
                        help='pasta de saída (é recriada a cada execução)')
    parser.add_argument('--navegador', help='caminho do msedge.exe ou chrome.exe para gerar os PDFs')
    parser.add_argument('--sem-pdf', action='store_true', help='não gera os DANFEs em PDF')
    args = parser.parse_args()

    args.empresas = [somente_digitos(args.empresas[0]), somente_digitos(args.empresas[1])]
    args.nao_cadastrada = somente_digitos(args.nao_cadastrada)
    for documento in args.empresas + [args.nao_cadastrada]:
        if len(documento) != 14:
            parser.error('CNPJ precisa ter 14 dígitos: ' + documento)
    if args.nao_cadastrada in args.empresas:
        parser.error('--nao-cadastrada precisa ser diferente das empresas cadastradas')
    if args.serie < 1 or args.serie > 999:
        parser.error('--serie vai de 1 a 999')
    return args


def preparar_saida(saida):
    if saida.exists():
        leiame = saida / 'LEIAME.md'
        criada_pelo_script = leiame.exists() and leiame.read_text(encoding='utf-8').startswith(MARCA_LEIAME)
        if not criada_pelo_script and any(saida.iterdir()):
            sys.exit('A pasta ' + str(saida) + ' já existe e não foi criada por este script. Use --saida.')
        shutil.rmtree(saida)
    saida.mkdir(parents=True)


def gravar(caminho, texto):
    caminho.write_bytes(texto.encode('utf-8'))


def main():
    args = ler_argumentos()
    hoje = date.today()
    destinatarios = montar_destinatarios(args)
    saida = Path(args.saida)

    navegador = None
    if not args.sem_pdf:
        navegador = achar_navegador(args.navegador)
        if navegador is None:
            print('Aviso: Edge/Chrome não encontrado; os PDFs não serão gerados (use --navegador).')
    sem_pdf = navegador is None

    preparar_saida(saida)
    trabalho = Path(tempfile.mkdtemp(prefix='previsio-notas-'))
    try:
        arquivos = {}
        geradas = {}
        for nota in NOTAS:
            dados = calcular_nota(nota, FORNECEDORES[nota['emit']], destinatarios[nota['dest']], hoje, args.serie)
            geradas[nota['id']] = dados

            caminho_xml = trabalho / (nota['arquivo'] + '.xml')
            gravar(caminho_xml, xml_nfe(dados))
            arquivos[(nota['id'], 'xml')] = caminho_xml

            if not sem_pdf and dados['modelo'] == '55':
                caminho_html = trabalho / (nota['arquivo'] + '-danfe.html')
                gravar(caminho_html, danfe_html(dados))
                caminho_pdf = trabalho / (nota['arquivo'] + '-danfe.pdf')
                imprimir_pdf(navegador, caminho_html, caminho_pdf, trabalho / 'perfis-navegador')
                arquivos[(nota['id'], 'pdf')] = caminho_pdf
                print('  ' + caminho_pdf.name)

        cancelada = geradas['01']
        quando = datetime.combine(hoje, time(10, 30))
        caminho_cancelamento = trabalho / (ARQUIVO_CANCELAMENTO + '.xml')
        gravar(caminho_cancelamento, xml_cancelamento(cancelada, quando))
        arquivos[('07', 'xml')] = caminho_cancelamento

        caminho_zip = trabalho / ARQUIVO_ZIP
        with zipfile.ZipFile(caminho_zip, 'w', zipfile.ZIP_DEFLATED) as pacote:
            for id_nota in ['06a', '06b']:
                for tipo in ['xml', 'pdf']:
                    origem = arquivos.get((id_nota, tipo))
                    if origem is not None:
                        pacote.write(origem, origem.name)
        arquivos[('zip',)] = caminho_zip

        for email in EMAILS:
            pasta = saida / email['pasta']
            pasta.mkdir()
            for anexo in email['anexos']:
                origem = arquivos.get(anexo)
                if origem is not None:
                    shutil.copy2(origem, pasta / origem.name)

        gravar(saida / 'LEIAME.md', gerar_leiame(geradas, cancelada, args, hoje, sem_pdf))
    finally:
        shutil.rmtree(trabalho, ignore_errors=True)

    print('Notas geradas em ' + str(saida))
    for email in EMAILS:
        nomes = sorted(arquivo.name for arquivo in (saida / email['pasta']).iterdir())
        print('  ' + email['pasta'] + ': ' + ', '.join(nomes))


if __name__ == '__main__':
    main()
