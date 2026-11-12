"""Segundo lote de notas fiscais (NF-e) de TESTE para o Previsio.

Complementa o gerar_notas.py com os cenários que ele não cobre: falha no Bling,
fornecedor novo e já existente, muitas parcelas, vencimentos fora da janela e vencidos,
nota para lançar manualmente no Bling, zip misturado, XML corrompido, CT-e,
caracteres especiais, duplicatas inválidas e várias empresas no mesmo e-mail.

Reaproveita as funções do gerar_notas.py (XML, DANFE e PDF). Cada pasta gerada é um
e-mail a enviar para a caixa de e-mail lida pelo Previsio; o LEIAME.md diz o resultado
esperado de cada um. Tudo é fictício e em ambiente de homologação ("SEM VALOR FISCAL").

Uso:
    python gerar_notas_2.py
    python gerar_notas_2.py --empresas 11222333000181 11222333000262
    python gerar_notas_2.py --serie 3      (chaves novas, para testar de novo sem limpar o banco)

Os PDFs são impressos pelo Microsoft Edge (ou Chrome) sem janela. Sem navegador, use --sem-pdf.
"""

import argparse
import re
import shutil
import sys
import tempfile
import zipfile
from datetime import date, datetime, time, timedelta
from decimal import Decimal
from pathlib import Path

import gerar_notas as base

MARCA_LEIAME = '<!-- gerado por gerar_notas_2.py -->'

# CNPJ com dígito verificador errado (o certo seria 45987123000104): o Bling deve recusar o contato
CNPJ_INVALIDO = '45987123000100'

FORNECEDORES = dict(base.FORNECEDORES)
FORNECEDORES['zeta'] = {
    'doc': base.cnpj('73519246', '0001'), 'nome': 'ZETA EMBREAGENS E TRANSMISSAO LTDA', 'fantasia': 'ZETA EMBREAGENS',
    'ie': '116734582110', 'lgr': 'RUA DAS ENGRENAGENS', 'nro': '77', 'bairro': 'IPIRANGA',
    'cmun': '3550308', 'mun': 'SAO PAULO', 'uf': 'SP', 'cuf': '35', 'cep': '04201000', 'fone': '1129291111',
}
FORNECEDORES['aco'] = {
    'doc': base.cnpj('39184627', '0001'), 'nome': 'AÇO & CIA FERRAGENS AUTOMOTIVAS LTDA - ME',
    'fantasia': 'AÇO & CIA', 'ie': '0712345678001', 'lgr': 'AVENIDA JOÃO PESSOA', 'nro': '1.450',
    'bairro': 'SÃO JOSÉ', 'cmun': '4314902', 'mun': 'PORTO ALEGRE', 'uf': 'RS', 'cuf': '43', 'cep': '90040000',
    'fone': '5132324444',
}
FORNECEDORES['eta'] = {
    'doc': base.cnpj('80426153', '0001'), 'nome': 'ETA ESCAPAMENTOS E SILENCIOSOS LTDA', 'fantasia': 'ETA ESCAPAMENTOS',
    'ie': '0034567890012', 'lgr': 'RODOVIA BR 116', 'nro': 'KM 12', 'bairro': 'DISTRITO INDUSTRIAL',
    'cmun': '2304400', 'mun': 'FORTALEZA', 'uf': 'CE', 'cuf': '23', 'cep': '60000000', 'fone': '8532325555',
}
FORNECEDORES['invalido'] = {
    'doc': CNPJ_INVALIDO, 'nome': 'OMEGA PECAS COM CNPJ INVALIDO LTDA', 'fantasia': 'OMEGA PECAS',
    'ie': '113456789110', 'lgr': 'RUA DO ERRO', 'nro': '404', 'bairro': 'CENTRO',
    'cmun': '3550308', 'mun': 'SAO PAULO', 'uf': 'SP', 'cuf': '35', 'cep': '01002000', 'fone': '1130302020',
}

# vencimentos = dias a partir de hoje (negativo = já venceu); lista vazia = nota à vista, sem <cobr>
NOTAS = [
    {'id': '21', 'arquivo': '21-fornecedor-novo', 'numero': 60101, 'emit': 'zeta', 'dest': 'A', 'emitida_ha': 1,
     'itens': [('AMORT', 4), ('CORREIA', 6)], 'vencimentos': [8, 22]},
    {'id': '22', 'arquivo': '22-fornecedor-ja-existente', 'numero': 60102, 'emit': 'zeta', 'dest': 'A', 'emitida_ha': 0,
     'itens': [('CORREIA', 10)], 'vencimentos': [16]},
    {'id': '23', 'arquivo': '23-seis-parcelas', 'numero': 70311, 'emit': 'eta', 'dest': 'B', 'emitida_ha': 2,
     'itens': [('PNEU', 7), ('VELA', 13)], 'vencimentos': [5, 15, 25, 35, 45, 55]},
    {'id': '24', 'arquivo': '24-vencimentos-fora-dos-30-dias', 'numero': 70312, 'emit': 'eta', 'dest': 'A',
     'emitida_ha': 1, 'itens': [('BAT', 2)], 'vencimentos': [45, 75]},
    {'id': '25', 'arquivo': '25-uma-parcela-vencida', 'numero': 48450, 'emit': 'alfa', 'dest': 'B', 'emitida_ha': 20,
     'itens': [('PAST', 6), ('FILT', 12)], 'vencimentos': [-5, 10]},
    {'id': '26', 'arquivo': '26-falha-no-bling-cnpj-invalido', 'numero': 404, 'emit': 'invalido', 'dest': 'A',
     'emitida_ha': 1, 'itens': [('CHAVE', 3)], 'vencimentos': [12]},
    {'id': '27', 'arquivo': '27-lancar-manualmente-no-bling', 'numero': 3390, 'emit': 'delta', 'dest': 'B',
     'emitida_ha': 0, 'itens': [('CHAVE', 4), ('VELA', 20)], 'vencimentos': []},
    {'id': '28', 'arquivo': '28-dentro-do-zip', 'numero': 10620, 'emit': 'beta', 'dest': 'B', 'emitida_ha': 1,
     'itens': [('OLEO', 36)], 'vencimentos': [11, 26]},
    {'id': '29', 'arquivo': '29-caracteres-especiais', 'numero': 1450, 'emit': 'aco', 'dest': 'A', 'emitida_ha': 2,
     'itens': [('CHAVE', 1), ('AMORT', 2)], 'vencimentos': [19]},
    {'id': '30', 'arquivo': '30-duplicatas-invalidas', 'numero': 5530, 'emit': 'gama', 'dest': 'A', 'emitida_ha': 1,
     'itens': [('PNEU', 2)], 'vencimentos': [10, 20]},
    {'id': '31', 'arquivo': '31-matriz', 'numero': 2240, 'emit': 'epsilon', 'dest': 'A', 'emitida_ha': 0,
     'itens': [('BAT', 1)], 'vencimentos': [6]},
    {'id': '32', 'arquivo': '32-filial-2', 'numero': 2241, 'emit': 'epsilon', 'dest': 'B', 'emitida_ha': 0,
     'itens': [('BAT', 2)], 'vencimentos': [13]},
    {'id': '33', 'arquivo': '33-empresa-nao-cadastrada', 'numero': 2242, 'emit': 'epsilon', 'dest': 'X',
     'emitida_ha': 0, 'itens': [('BAT', 3)], 'vencimentos': [20]},
    # Só serve de base para o XML corrompido (o Previsio não consegue ler)
    {'id': '34', 'arquivo': '34-xml-corrompido', 'numero': 9999, 'emit': 'beta', 'dest': 'A', 'emitida_ha': 0,
     'itens': [('OLEO', 1)], 'vencimentos': [9]},
]

# Notas que o Previsio registra (as que lança e as que ficam pendentes)
REGISTRADAS = ['21', '22', '23', '24', '25', '26', '27', '28', '29', '30', '31', '32', '33']

ARQUIVO_CTE = '35-cte-conhecimento-de-transporte.xml'
ARQUIVO_ZIP = '08-lote-misturado.zip'
ARQUIVO_ZIP_CORROMPIDO = '09-zip-corrompido.zip'
ARQUIVO_REPETIDA_ZIP = '13-reenvio-da-21.zip'

# Cada item vira uma pasta = um e-mail.
# ('21', 'xml') = XML da nota 21; ('arquivo', nome) = arquivo gerado à parte.
EMAILS = [
    {'pasta': 'email-01-sucesso-fornecedor-novo', 'assunto': 'NF-e 60101 - Zeta Embreagens',
     'anexos': [('21', 'xml'), ('21', 'pdf')]},
    {'pasta': 'email-02-sucesso-fornecedor-ja-existente', 'assunto': 'NF-e 60102 - Zeta Embreagens',
     'anexos': [('22', 'xml'), ('22', 'pdf')]},
    {'pasta': 'email-03-seis-parcelas', 'assunto': 'NF-e 70311 - Eta Escapamentos',
     'anexos': [('23', 'xml'), ('23', 'pdf')]},
    {'pasta': 'email-04-vencimentos-fora-dos-30-dias', 'assunto': 'NF-e 70312 - Eta Escapamentos',
     'anexos': [('24', 'xml'), ('24', 'pdf')]},
    {'pasta': 'email-05-uma-parcela-vencida', 'assunto': 'NF-e 48450 - Alfa Autopecas',
     'anexos': [('25', 'xml'), ('25', 'pdf')]},
    {'pasta': 'email-06-falha-no-bling', 'assunto': 'NF-e 404 - Omega Pecas',
     'anexos': [('26', 'xml'), ('26', 'pdf')]},
    {'pasta': 'email-07-lancar-manualmente-no-bling', 'assunto': 'NF-e 3390 - Delta Ferramentas',
     'anexos': [('27', 'xml'), ('27', 'pdf')]},
    {'pasta': 'email-08-zip-misturado', 'assunto': 'Lote de documentos (zip)',
     'anexos': [('arquivo', ARQUIVO_ZIP)]},
    {'pasta': 'email-09-arquivos-com-problema', 'assunto': 'Arquivos com problema',
     'anexos': [('34', 'xml'), ('arquivo', ARQUIVO_CTE), ('arquivo', ARQUIVO_ZIP_CORROMPIDO)]},
    {'pasta': 'email-10-caracteres-especiais', 'assunto': 'NF-e 1450 - Aço & Cia',
     'anexos': [('29', 'xml'), ('29', 'pdf')]},
    {'pasta': 'email-11-duplicatas-invalidas', 'assunto': 'NF-e 5530 - Gama Pneus',
     'anexos': [('30', 'xml'), ('30', 'pdf')]},
    {'pasta': 'email-12-tres-empresas-no-mesmo-email', 'assunto': 'Notas Epsilon Baterias',
     'anexos': [('31', 'xml'), ('32', 'xml'), ('33', 'xml')]},
    {'pasta': 'email-13-reenvio-dentro-de-zip', 'assunto': 'Reenvio NF-e 60101 (zip)',
     'anexos': [('arquivo', ARQUIVO_REPETIDA_ZIP)]},
]


# ---------------------------------------------------------------- arquivos especiais

def xml_duplicatas_invalidas(texto):
    """1ª duplicata sem vencimento e 2ª com valor zero: o Previsio ignora as duas."""
    texto = re.sub(r'(<dup>\s*<nDup>001</nDup>\s*)<dVenc>[^<]*</dVenc>\s*', r'\1', texto, count=1)
    texto = re.sub(r'(<dup>\s*<nDup>002</nDup>\s*<dVenc>[^<]*</dVenc>\s*)<vDup>[^<]*</vDup>',
                   r'\1<vDup>0.00</vDup>', texto, count=1)
    return texto


def xml_corrompido(texto):
    """Arquivo cortado no meio, como num download interrompido."""
    return texto[:len(texto) * 55 // 100]


def xml_cte(hoje, empresa):
    """CT-e (conhecimento de transporte): não é NF-e, o Previsio não registra."""
    chave = '35' + hoje.strftime('%y%m') + base.cnpj('61845273', '0001') + '57' + '001' + '000004512' + '1' + '12345678'
    chave = chave + base.digito_chave(chave)
    return ('<?xml version="1.0" encoding="UTF-8"?>\n'
            '<cteProc xmlns="http://www.portalfiscal.inf.br/cte" versao="4.00">\n'
            '  <CTe>\n'
            '    <infCte versao="4.00" Id="CTe' + chave + '">\n'
            '      <ide><cUF>35</cUF><mod>57</mod><serie>1</serie><nCT>4512</nCT>'
            '<dhEmi>' + hoje.strftime('%Y-%m-%d') + 'T08:00:00-03:00</dhEmi><tpAmb>2</tpAmb></ide>\n'
            '      <emit><CNPJ>' + base.cnpj('61845273', '0001') + '</CNPJ>'
            '<xNome>TRANSPORTADORA TESTE LTDA</xNome></emit>\n'
            '      <dest><CNPJ>' + empresa + '</CNPJ><xNome>' + base.TEXTO_HOMOLOGACAO + '</xNome></dest>\n'
            '      <vPrest><vTPrest>350.00</vTPrest></vPrest>\n'
            '    </infCte>\n'
            '  </CTe>\n'
            '  <protCTe versao="4.00"><infProt><chCTe>' + chave + '</chCTe><cStat>100</cStat></infProt></protCTe>\n'
            '</cteProc>\n')


def zip_corrompido():
    """Começa como zip, mas o conteúdo está quebrado: o Previsio ignora o arquivo."""
    return b'PK\x03\x04' + b'\x00' * 22 + b'arquivo truncado - zip de teste corrompido'


# ---------------------------------------------------------------- LEIAME

EMPRESAS = {'A': 'matriz', 'B': 'filial 2'}


def lancada(dados):
    return dados['nota']['dest'] != 'X' and len(dados['parcelas']) > 0 and dados['nota']['id'] not in ('26', '30')


def resultado(dados):
    nota = dados['nota']
    if nota['dest'] == 'X':
        return 'Pendente: "Empresa não encontrada" (CNPJ ' + base.br_doc(dados['destinatario']['doc']) + ').'
    if nota['id'] == '30':
        return 'Pendente: "Sem vencimentos". As duas duplicatas são inválidas e ignoradas.'
    if len(dados['parcelas']) == 0:
        return 'Pendente: "Sem vencimentos". Não vai para o Bling.'
    if nota['id'] == '26':
        return ('**Falha:** "Erro ao lançar". O Bling recusa o fornecedor (CNPJ ' + base.br_doc(CNPJ_INVALIDO)
                + ' com dígito errado). Aparece em Falhas, etapa "Cadastro do fornecedor", com a resposta do Bling.')
    quantidade = len(dados['parcelas'])
    if quantidade == 1:
        texto = '1 conta a pagar'
    else:
        texto = str(quantidade) + ' contas a pagar'
    return texto + ' na ' + EMPRESAS[nota['dest']]


def no_painel_30_dias(parcela, hoje):
    dias = (parcela['vencimento'] - hoje).days
    return 0 <= dias <= 30


def gerar_leiame(geradas, args, hoje, sem_pdf):
    g = geradas
    linhas = [MARCA_LEIAME, '# Notas fiscais de teste: segundo lote', '',
              'Geradas em ' + base.br_data(hoje) + ' por `gerar_notas_2.py`, série ' + str(args.serie) + '.',
              'Tudo fictício, em ambiente de homologação e **sem valor fiscal**. Cada pasta é um e-mail.',
              'Complementa o primeiro lote (`gerar_notas.py`, pasta `arquivos`).', '']
    if sem_pdf:
        linhas.extend(['> PDFs não gerados (`--sem-pdf` ou navegador não encontrado).', ''])

    linhas.extend([
        '## Antes de enviar', '',
        '1. Na tela **Empresas**, as duas empresas que recebem as notas precisam estar cadastradas e ativas:', '',
        '   | Empresa | CNPJ |', '   |---|---|',
        '   | Autopeças piloto matriz | `' + base.br_doc(args.empresas[0]) + '` |',
        '   | Autopeças piloto filial 2 | `' + base.br_doc(args.empresas[1]) + '` |', '',
        '   **Não** cadastre `' + base.br_doc(args.nao_cadastrada) + '`: a nota 33 testa justamente isso.',
        '2. Conecte o Bling nas duas, com o aplicativo tendo permissão de **incluir** em Contatos e Contas a pagar.',
        '3. Envie cada pasta como **um e-mail**, com todos os arquivos dela anexados, para a caixa lida pelo Previsio',
        '   (a da rede ou a própria de uma das empresas; tanto faz: a nota vai para a empresa pelo CNPJ do destinatário).',
        '4. **Mande o email-01 antes do email-02** (o 02 testa o fornecedor que o 01 cadastra no Bling)',
        '   e o email-13 por último (é o reenvio da nota 21).', '',
        '## E-mails', '',
        '| Pasta | Assunto sugerido | O que testa | Resultado esperado |', '|---|---|---|---|',
    ])

    descricoes = {
        'email-01-sucesso-fornecedor-novo': (
            'Fornecedor que ainda não existe no Bling (o Previsio cadastra o contato) e 2 parcelas', resultado(g['21'])
            + '. Fornecedor Zeta criado em Contatos.'),
        'email-02-sucesso-fornecedor-ja-existente': (
            'Mesmo fornecedor da 21: o Previsio encontra o contato e não cria outro', resultado(g['22'])
            + '. Contatos continua com **um** Zeta.'),
        'email-03-seis-parcelas': (
            '6 duplicatas, com centavos sobrando na última; 3 vencem depois dos 30 dias', resultado(g['23'])
            + '. No painel de 30 dias entram só as 3 primeiras.'),
        'email-04-vencimentos-fora-dos-30-dias': (
            'Vencimentos em 45 e 75 dias', resultado(g['24'])
            + '. Não aparece no painel de 30 dias; aparece com o período de 90 dias.'),
        'email-05-uma-parcela-vencida': (
            'Nota de 20 dias atrás com uma parcela já vencida (há 5 dias)', resultado(g['25'])
            + '. A vencida não entra no painel (que mostra de hoje em diante).'),
        'email-06-falha-no-bling': (
            'Fornecedor com CNPJ de dígito verificador errado', resultado(g['26'])),
        'email-07-lancar-manualmente-no-bling': (
            'Nota à vista (sem duplicatas) para lançar à mão no Bling e marcar "Já lancei no Bling"',
            resultado(g['27']) + ' Siga o roteiro mais abaixo.'),
        'email-08-zip-misturado': (
            'Zip com uma NF-e, o DANFE em PDF, um CT-e e um evento de cancelamento',
            '28: ' + resultado(g['28']) + '.<br>CT-e e cancelamento: em Falhas como "XML não reconhecido". O PDF é ignorado.'),
        'email-09-arquivos-com-problema': (
            'XML cortado no meio, CT-e solto e zip corrompido',
            'Nenhuma nota registrada.<br>XML cortado e CT-e: em Falhas como "XML não reconhecido".'
            '<br>Zip corrompido: ignorado sem registro.'),
        'email-10-caracteres-especiais': (
            'Fornecedor com acentos e "&" no nome e no endereço', resultado(g['29'])
            + '. Nome no Bling: "AÇO & CIA FERRAGENS AUTOMOTIVAS LTDA - ME".'),
        'email-11-duplicatas-invalidas': (
            '1ª duplicata sem data de vencimento e 2ª com valor zero', resultado(g['30'])),
        'email-12-tres-empresas-no-mesmo-email': (
            'Três notas do mesmo fornecedor para matriz, filial 2 e um CNPJ não cadastrado (só XML, sem PDF)',
            '31: ' + resultado(g['31']) + '.<br>32: ' + resultado(g['32']) + '.<br>33: ' + resultado(g['33'])),
        'email-13-reenvio-dentro-de-zip': (
            'A nota 21 de novo, agora dentro de um zip', 'Nada novo: a nota é reconhecida pela chave de acesso.'),
    }
    for email in EMAILS:
        oque, esperado = descricoes[email['pasta']]
        linhas.append('| `' + email['pasta'] + '` | ' + email['assunto'] + ' | ' + oque + ' | ' + esperado + ' |')

    linhas.extend([
        '', '## Depois de enviar', '',
        'Em **Notas pendentes**, como admin, clique em **Buscar e-mails agora**. Ou espere o agendamento:',
        'a leitura roda a cada 10 minutos e o lançamento logo depois.',
        'Em **Empresas**, **Sincronizar contas agora** traz as contas do Bling para o painel.', '',
        '## Roteiro: nota 27, lançada manualmente no Bling', '',
        '1. Ela aparece em **Notas pendentes** como "Sem vencimentos" (filial 2, R$ '
        + base.br_dinheiro(g['27']['total']) + ').',
        '2. No **Bling da filial 2**, crie a conta a pagar à mão:', '',
        '   | Campo | Valor |', '   |---|---|',
        '   | Fornecedor | ' + g['27']['emitente']['nome'] + ' (CNPJ ' + base.br_doc(g['27']['emitente']['doc']) + ') |',
        '   | Valor | R$ ' + base.br_dinheiro(g['27']['total']) + ' |',
        '   | Vencimento | ' + base.br_data(hoje + timedelta(days=12)) + ' |',
        '   | Nº do documento | ' + str(g['27']['nota']['numero']) + ' |', '',
        '3. No Previsio, na nota 3390, clique em **Já lancei no Bling** (ícone ✓) e escreva uma observação,',
        '   por exemplo "lançada à mão, sem boleto".',
        '4. A nota sai das pendentes. Em **Notas recebidas** ela fica como "Lançada manualmente", com quem e quando.',
        '5. Em **Empresas**, clique em **Sincronizar contas agora**: a conta criada à mão entra no painel da filial 2',
        '   no dia ' + base.br_data(hoje + timedelta(days=12)) + '.', '',
        'A nota 26 (falha no Bling) também serve para esse roteiro: corrija o fornecedor à mão no Bling,',
        'lance a conta lá e marque **Já lancei no Bling**.', '',
        '## Conferência', '',
    ])

    lancadas = [g[i] for i in REGISTRADAS if lancada(g[i])]
    pendentes = [g[i] for i in REGISTRADAS if not lancada(g[i])]
    total_contas = sum(len(d['parcelas']) for d in lancadas)
    linhas.extend([
        '- **Notas registradas:** ' + str(len(REGISTRADAS)) + ' (' + ', '.join(REGISTRADAS) + ').',
        '- **Lançadas pelo Previsio:** ' + str(len(lancadas)) + ' notas, ' + str(total_contas) + ' contas a pagar.',
        '- **Pendentes ou com falha:** ' + ', '.join(d['nota']['id'] for d in pendentes) + '.',
        '- **Em Falhas (XML não reconhecido):** CT-e e cancelamento do zip (email-08), XML cortado e CT-e (email-09).',
        '',
        '| Nota | Fornecedor | Empresa | Nº do documento | Vencimento | Valor (R$) | No painel de 30 dias? |',
        '|---|---|---|---|---|---|---|',
    ])
    por_empresa = {'A': Decimal('0.00'), 'B': Decimal('0.00')}
    for dados in lancadas:
        quantidade = len(dados['parcelas'])
        for parcela in dados['parcelas']:
            documento = str(dados['nota']['numero'])
            if quantidade > 1:
                documento = documento + '/' + parcela['numero'].lstrip('0')
            entra = no_painel_30_dias(parcela, hoje)
            if entra:
                por_empresa[dados['nota']['dest']] += parcela['valor']
            marcacao = 'sim'
            if not entra:
                marcacao = 'não'
            linhas.append('| ' + dados['nota']['id'] + ' | ' + dados['emitente']['nome'] + ' | '
                          + EMPRESAS[dados['nota']['dest']] + ' | ' + documento + ' | '
                          + base.br_data(parcela['vencimento']) + ' | ' + base.br_dinheiro(parcela['valor'])
                          + ' | ' + marcacao + ' |')

    linhas.extend(['', '| Nota pendente ou com falha | Fornecedor | Empresa | Valor (R$) | Situação |', '|---|---|---|---|---|'])
    for dados in pendentes:
        empresa = EMPRESAS.get(dados['nota']['dest'], 'não cadastrada')
        linhas.append('| ' + dados['nota']['id'] + ' | ' + dados['emitente']['nome'] + ' | ' + empresa + ' | '
                      + base.br_dinheiro(dados['total']) + ' | ' + resultado(dados).replace('|', '/') + ' |')

    manual = g['27']['total']
    linhas.extend([
        '',
        '**Painel (30 dias), depois da sincronização, só com este lote:** matriz R$ '
        + base.br_dinheiro(por_empresa['A']) + ', filial 2 R$ ' + base.br_dinheiro(por_empresa['B'])
        + ' (+ R$ ' + base.br_dinheiro(manual) + ' da nota 27, se você lançá-la à mão).',
        'Somam-se a isso as contas do primeiro lote e as que já existirem no Bling dessas empresas.',
        '', '## Testar de novo', '',
        'As chaves de acesso não mudam dentro do mesmo mês: reenviar os mesmos arquivos dá "já existentes".',
        'Para um teste limpo, gere chaves novas com `python gerar_notas_2.py --serie ' + str(args.serie + 1) + '`.',
        '',
    ])
    return '\n'.join(linhas)


# ---------------------------------------------------------------- principal

def ler_argumentos():
    parser = argparse.ArgumentParser(description='Gera o segundo lote de NF-e de teste para o Previsio.')
    parser.add_argument('--empresas', nargs=2, metavar=('CNPJ_MATRIZ', 'CNPJ_FILIAL'),
                        default=[base.cnpj('11222333', '0001'), base.cnpj('11222333', '0002')],
                        help='CNPJs das duas empresas cadastradas no Previsio que recebem as notas')
    parser.add_argument('--nao-cadastrada', default=base.cnpj('11222333', '0003'),
                        help='CNPJ que NÃO está cadastrado (nota 33)')
    parser.add_argument('--serie', type=int, default=2, help='série das notas; mude para gerar chaves novas')
    parser.add_argument('--saida', default=str(Path(__file__).resolve().parent / 'arquivos 2'),
                        help='pasta de saída (é recriada a cada execução)')
    parser.add_argument('--navegador', help='caminho do msedge.exe ou chrome.exe para gerar os PDFs')
    parser.add_argument('--sem-pdf', action='store_true', help='não gera os DANFEs em PDF')
    args = parser.parse_args()

    args.empresas = [base.somente_digitos(args.empresas[0]), base.somente_digitos(args.empresas[1])]
    args.nao_cadastrada = base.somente_digitos(args.nao_cadastrada)
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


def main():
    args = ler_argumentos()
    hoje = date.today()
    destinatarios = base.montar_destinatarios(args)
    saida = Path(args.saida)

    navegador = None
    if not args.sem_pdf:
        navegador = base.achar_navegador(args.navegador)
        if navegador is None:
            print('Aviso: Edge/Chrome não encontrado; os PDFs não serão gerados (use --navegador).')
    sem_pdf = navegador is None

    preparar_saida(saida)
    trabalho = Path(tempfile.mkdtemp(prefix='previsio-notas-2-'))
    try:
        arquivos = {}
        geradas = {}
        for nota in NOTAS:
            dados = base.calcular_nota(nota, FORNECEDORES[nota['emit']], destinatarios[nota['dest']], hoje, args.serie)
            geradas[nota['id']] = dados

            texto = base.xml_nfe(dados)
            if nota['id'] == '30':
                texto = xml_duplicatas_invalidas(texto)
            if nota['id'] == '34':
                texto = xml_corrompido(texto)
            caminho_xml = trabalho / (nota['arquivo'] + '.xml')
            base.gravar(caminho_xml, texto)
            arquivos[(nota['id'], 'xml')] = caminho_xml

            if not sem_pdf and nota['id'] != '34':
                caminho_html = trabalho / (nota['arquivo'] + '-danfe.html')
                base.gravar(caminho_html, base.danfe_html(dados))
                caminho_pdf = trabalho / (nota['arquivo'] + '-danfe.pdf')
                base.imprimir_pdf(navegador, caminho_html, caminho_pdf, trabalho / 'perfis-navegador')
                arquivos[(nota['id'], 'pdf')] = caminho_pdf
                print('  ' + caminho_pdf.name)

        caminho_cte = trabalho / ARQUIVO_CTE
        base.gravar(caminho_cte, xml_cte(hoje, args.empresas[1]))
        arquivos[('arquivo', ARQUIVO_CTE)] = caminho_cte

        caminho_cancelamento = trabalho / '28b-cancelamento-de-outra-nota.xml'
        base.gravar(caminho_cancelamento, base.xml_cancelamento(geradas['24'], datetime.combine(hoje, time(11, 0))))

        caminho_zip = trabalho / ARQUIVO_ZIP
        with zipfile.ZipFile(caminho_zip, 'w', zipfile.ZIP_DEFLATED) as pacote:
            pacote.write(arquivos[('28', 'xml')], arquivos[('28', 'xml')].name)
            if ('28', 'pdf') in arquivos:
                pacote.write(arquivos[('28', 'pdf')], arquivos[('28', 'pdf')].name)
            pacote.write(caminho_cte, 'documentos/' + ARQUIVO_CTE)
            pacote.write(caminho_cancelamento, 'documentos/' + caminho_cancelamento.name)
        arquivos[('arquivo', ARQUIVO_ZIP)] = caminho_zip

        caminho_zip_corrompido = trabalho / ARQUIVO_ZIP_CORROMPIDO
        caminho_zip_corrompido.write_bytes(zip_corrompido())
        arquivos[('arquivo', ARQUIVO_ZIP_CORROMPIDO)] = caminho_zip_corrompido

        caminho_repetida = trabalho / ARQUIVO_REPETIDA_ZIP
        with zipfile.ZipFile(caminho_repetida, 'w', zipfile.ZIP_DEFLATED) as pacote:
            pacote.write(arquivos[('21', 'xml')], 'reenvio-' + arquivos[('21', 'xml')].name)
        arquivos[('arquivo', ARQUIVO_REPETIDA_ZIP)] = caminho_repetida

        for email in EMAILS:
            pasta = saida / email['pasta']
            pasta.mkdir()
            for anexo in email['anexos']:
                origem = arquivos.get(anexo)
                if origem is not None:
                    shutil.copy2(origem, pasta / origem.name)

        base.gravar(saida / 'LEIAME.md', gerar_leiame(geradas, args, hoje, sem_pdf))
    finally:
        shutil.rmtree(trabalho, ignore_errors=True)

    print('Notas geradas em ' + str(saida))
    for email in EMAILS:
        nomes = sorted(arquivo.name for arquivo in (saida / email['pasta']).iterdir())
        print('  ' + email['pasta'] + ': ' + ', '.join(nomes))


if __name__ == '__main__':
    main()
