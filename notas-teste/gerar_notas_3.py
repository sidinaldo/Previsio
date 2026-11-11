"""Terceiro lote de notas fiscais (NF-e) de TESTE para o Previsio, separado por empresa.

Cada pasta de primeiro nível é uma empresa (matriz, filial 2, CNPJ não cadastrado) e mais
uma pasta "caixa-errada" com notas enviadas de propósito para a caixa de outra empresa.
Dentro delas, cada pasta é um e-mail; o LEIAME.md diz para qual caixa mandar e o
resultado esperado de cada um.

Usa números e série novos (série 3), para não cair como duplicadas das notas dos lotes
anteriores, que já foram importadas. Reaproveita o XML, o DANFE e o PDF do gerar_notas.py.
Tudo é fictício e em ambiente de homologação ("SEM VALOR FISCAL").

Uso:
    python gerar_notas_3.py
    python gerar_notas_3.py --empresas 11222333000181 11222333000262
    python gerar_notas_3.py --serie 4      (chaves novas, para testar de novo sem limpar o banco)

Os PDFs são impressos pelo Microsoft Edge (ou Chrome) sem janela. Sem navegador, use --sem-pdf.
"""

import argparse
import shutil
import sys
import tempfile
import zipfile
from datetime import date, datetime, time
from pathlib import Path

import gerar_notas as base
import gerar_notas_2 as lote2

MARCA_LEIAME = '<!-- gerado por gerar_notas_3.py -->'

FORNECEDORES = dict(lote2.FORNECEDORES)
# Fornecedores que ainda não existem no Bling de nenhuma das empresas (testam o cadastro do contato)
FORNECEDORES['iota'] = {
    'doc': base.cnpj('64830512', '0001'), 'nome': 'IOTA RADIADORES E ARREFECIMENTO LTDA', 'fantasia': 'IOTA RADIADORES',
    'ie': '117845236110', 'lgr': 'RUA DOS RADIADORES', 'nro': '520', 'bairro': 'BRAS',
    'cmun': '3550308', 'mun': 'SAO PAULO', 'uf': 'SP', 'cuf': '35', 'cep': '03003000', 'fone': '1132321212',
}
FORNECEDORES['kappa'] = {
    'doc': base.cnpj('71295348', '0001'), 'nome': 'KAPPA SUSPENSOES AUTOMOTIVAS LTDA', 'fantasia': 'KAPPA SUSPENSOES',
    'ie': '0623781450093', 'lgr': 'AVENIDA DO CONTORNO', 'nro': '9100', 'bairro': 'BARRO PRETO',
    'cmun': '3106200', 'mun': 'BELO HORIZONTE', 'uf': 'MG', 'cuf': '31', 'cep': '30110110', 'fone': '3133331313',
}

# Destinatários: A = matriz, B = filial 2, X = CNPJ não cadastrado (mesmos códigos do gerar_notas.py)
EMPRESA_DA_LETRA = {'A': 'matriz', 'B': 'filial 2', 'X': 'CNPJ não cadastrado'}

# vencimentos = dias a partir de hoje (negativo = já venceu); lista vazia = nota à vista, sem <cobr>
# tipo: 'ok' lança; 'falha-bling' o Bling recusa o fornecedor; 'sem-vencimentos'; 'duplicatas-invalidas';
#       'nao-encontrada'; 'corrompido' (o Previsio não consegue ler)
NOTAS = [
    # ---- matriz
    {'id': 'M01', 'numero': 81001, 'emit': 'iota', 'dest': 'A', 'emitida_ha': 1, 'tipo': 'ok',
     'itens': [('AMORT', 2), ('CORREIA', 4)], 'vencimentos': [7, 21]},
    {'id': 'M02', 'numero': 81002, 'emit': 'iota', 'dest': 'A', 'emitida_ha': 0, 'tipo': 'ok',
     'itens': [('CORREIA', 6)], 'vencimentos': [14]},
    {'id': 'M03', 'numero': 81003, 'emit': 'alfa', 'dest': 'A', 'emitida_ha': 2, 'tipo': 'ok',
     'itens': [('PAST', 8), ('FILT', 10)], 'vencimentos': [10]},
    {'id': 'M04', 'numero': 81004, 'emit': 'eta', 'dest': 'A', 'emitida_ha': 1, 'tipo': 'ok',
     'itens': [('BAT', 3)], 'vencimentos': [40, 70]},
    {'id': 'M05', 'numero': 81005, 'emit': 'invalido', 'dest': 'A', 'emitida_ha': 1, 'tipo': 'falha-bling',
     'itens': [('CHAVE', 2)], 'vencimentos': [12]},
    {'id': 'M06', 'numero': 81006, 'emit': 'delta', 'dest': 'A', 'emitida_ha': 0, 'tipo': 'sem-vencimentos',
     'itens': [('CHAVE', 3), ('VELA', 12)], 'vencimentos': []},
    {'id': 'M07', 'numero': 81007, 'emit': 'gama', 'dest': 'A', 'emitida_ha': 1, 'tipo': 'duplicatas-invalidas',
     'itens': [('PNEU', 4)], 'vencimentos': [10, 20]},
    {'id': 'M08', 'numero': 81008, 'emit': 'beta', 'dest': 'A', 'emitida_ha': 1, 'tipo': 'ok',
     'itens': [('OLEO', 24)], 'vencimentos': [9, 23]},
    {'id': 'M09', 'numero': 81009, 'emit': 'aco', 'dest': 'A', 'emitida_ha': 2, 'tipo': 'ok',
     'itens': [('CHAVE', 1), ('AMORT', 1)], 'vencimentos': [18]},
    # ---- filial 2
    {'id': 'F01', 'numero': 82001, 'emit': 'kappa', 'dest': 'B', 'emitida_ha': 1, 'tipo': 'ok',
     'itens': [('AMORT', 4)], 'vencimentos': [6, 20]},
    {'id': 'F02', 'numero': 82002, 'emit': 'eta', 'dest': 'B', 'emitida_ha': 2, 'tipo': 'ok',
     'itens': [('PNEU', 5), ('VELA', 16)], 'vencimentos': [5, 15, 25, 35, 45, 55]},
    {'id': 'F03', 'numero': 82003, 'emit': 'alfa', 'dest': 'B', 'emitida_ha': 20, 'tipo': 'ok',
     'itens': [('PAST', 4), ('FILT', 8)], 'vencimentos': [-4, 11]},
    {'id': 'F04', 'numero': 82004, 'emit': 'delta', 'dest': 'B', 'emitida_ha': 0, 'tipo': 'sem-vencimentos',
     'itens': [('CHAVE', 5)], 'vencimentos': []},
    {'id': 'F05', 'numero': 82005, 'emit': 'invalido', 'dest': 'B', 'emitida_ha': 1, 'tipo': 'falha-bling',
     'itens': [('VELA', 30)], 'vencimentos': [16]},
    {'id': 'F06', 'numero': 82006, 'emit': 'beta', 'dest': 'B', 'emitida_ha': 1, 'tipo': 'ok',
     'itens': [('OLEO', 30)], 'vencimentos': [12, 27]},
    {'id': 'F07', 'numero': 82007, 'emit': 'epsilon', 'dest': 'B', 'emitida_ha': 1, 'tipo': 'ok',
     'itens': [('BAT', 2)], 'vencimentos': [17]},
    {'id': 'F08', 'numero': 82008, 'emit': 'gama', 'dest': 'B', 'emitida_ha': 0, 'tipo': 'corrompido',
     'itens': [('PNEU', 1)], 'vencimentos': [9]},
    # ---- CNPJ não cadastrado
    {'id': 'X01', 'numero': 83001, 'emit': 'epsilon', 'dest': 'X', 'emitida_ha': 0, 'tipo': 'nao-encontrada',
     'itens': [('BAT', 1)], 'vencimentos': [20]},
    # ---- caixa errada (o destino no XML é um, a caixa em que chega é outra)
    {'id': 'C01', 'numero': 84001, 'emit': 'beta', 'dest': 'A', 'emitida_ha': 0, 'tipo': 'ok',
     'itens': [('OLEO', 12), ('FILT', 6)], 'vencimentos': [8]},
    {'id': 'C02', 'numero': 84002, 'emit': 'gama', 'dest': 'X', 'emitida_ha': 0, 'tipo': 'nao-encontrada',
     'itens': [('PNEU', 2)], 'vencimentos': [15]},
]

ARQUIVO_CTE = 'cte-conhecimento-de-transporte.xml'
ARQUIVO_CANCELAMENTO = 'cancelamento-de-outra-nota.xml'
ARQUIVO_ZIP_MISTURADO = 'lote-misturado.zip'
ARQUIVO_ZIP_DUAS = 'lote-duas-notas.zip'
ARQUIVO_ZIP_CORROMPIDO = 'zip-corrompido.zip'
ARQUIVO_REENVIO_ZIP = 'reenvio-81001.zip'

# Para onde mandar cada grupo (o Previsio lê a caixa da rede e a caixa própria de cada empresa)
CAIXA_REDE = 'a caixa da **rede**'
CAIXA_FILIAL = 'a caixa **própria da filial 2** (ex.: sidinaldobarbosa@gmail.com); se ela não tiver uma, a da rede'

# Cada grupo é uma pasta; cada e-mail, uma subpasta. ('M01', 'xml') = XML da nota M01;
# ('arquivo', nome) = arquivo gerado à parte.
GRUPOS = [
    {'pasta': 'matriz', 'titulo': 'Autopeças piloto matriz', 'letra': 'A', 'caixa': CAIXA_REDE, 'emails': [
        {'pasta': 'email-01-fornecedor-novo', 'assunto': 'NF-e 81001 - Iota Radiadores',
         'anexos': [('M01', 'xml'), ('M01', 'pdf')],
         'testa': 'Fornecedor que não existe no Bling da matriz; 2 parcelas',
         'esperado': 'NOTA:M01. Iota criado em Contatos do Bling da matriz.'},
        {'pasta': 'email-02-fornecedor-ja-existente', 'assunto': 'NF-e 81002 - Iota Radiadores',
         'anexos': [('M02', 'xml'), ('M02', 'pdf')],
         'testa': 'Mesmo fornecedor do email-01: o Previsio encontra o contato e não cria outro',
         'esperado': 'NOTA:M02. Contatos continua com **um** Iota.'},
        {'pasta': 'email-03-parcela-unica', 'assunto': 'NF-e 81003 - Alfa Autopecas',
         'anexos': [('M03', 'xml'), ('M03', 'pdf')],
         'testa': 'Nota simples, uma duplicata',
         'esperado': 'NOTA:M03.'},
        {'pasta': 'email-04-vencimentos-fora-dos-30-dias', 'assunto': 'NF-e 81004 - Eta Escapamentos',
         'anexos': [('M04', 'xml'), ('M04', 'pdf')],
         'testa': 'Vencimentos em 40 e 70 dias',
         'esperado': 'NOTA:M04. Não aparece no painel de 30 dias; aparece no de 90.'},
        {'pasta': 'email-05-falha-no-bling', 'assunto': 'NF-e 81005 - Omega Pecas',
         'anexos': [('M05', 'xml'), ('M05', 'pdf')],
         'testa': 'Fornecedor com CNPJ de dígito verificador errado',
         'esperado': 'NOTA:M05'},
        {'pasta': 'email-06-lancar-manualmente-no-bling', 'assunto': 'NF-e 81006 - Delta Ferramentas',
         'anexos': [('M06', 'xml'), ('M06', 'pdf')],
         'testa': 'Nota à vista (sem duplicatas): lançar à mão no Bling e marcar "Já lancei no Bling"',
         'esperado': 'NOTA:M06'},
        {'pasta': 'email-07-duplicatas-invalidas', 'assunto': 'NF-e 81007 - Gama Pneus',
         'anexos': [('M07', 'xml'), ('M07', 'pdf')],
         'testa': '1ª duplicata sem vencimento e 2ª com valor zero',
         'esperado': 'NOTA:M07'},
        {'pasta': 'email-08-zip-misturado', 'assunto': 'Lote de documentos (zip)',
         'anexos': [('arquivo', ARQUIVO_ZIP_MISTURADO)],
         'testa': 'Zip com uma NF-e, o DANFE em PDF, um CT-e e um evento de cancelamento',
         'esperado': 'NOTA:M08.<br>CT-e e cancelamento: em Falhas como "XML não reconhecido". O PDF é ignorado.'},
        {'pasta': 'email-09-caracteres-especiais', 'assunto': 'NF-e 81009 - Aço & Cia',
         'anexos': [('M09', 'xml'), ('M09', 'pdf')],
         'testa': 'Fornecedor com acentos e "&" no nome e no endereço',
         'esperado': 'NOTA:M09. Nome no Bling: "AÇO & CIA FERRAGENS AUTOMOTIVAS LTDA - ME".'},
        {'pasta': 'email-10-reenvio-dentro-de-zip', 'assunto': 'Reenvio NF-e 81001 (zip)',
         'anexos': [('arquivo', ARQUIVO_REENVIO_ZIP)],
         'testa': 'A nota do email-01 de novo, agora dentro de um zip. **Mande por último.**',
         'esperado': 'Nada novo: a nota é reconhecida pela chave de acesso.'},
    ]},
    {'pasta': 'filial-2', 'titulo': 'Autopeças piloto filial 2', 'letra': 'B', 'caixa': CAIXA_FILIAL, 'emails': [
        {'pasta': 'email-01-fornecedor-novo', 'assunto': 'NF-e 82001 - Kappa Suspensoes',
         'anexos': [('F01', 'xml'), ('F01', 'pdf')],
         'testa': 'Fornecedor que não existe no Bling da filial; 2 parcelas',
         'esperado': 'NOTA:F01. Kappa criado em Contatos do Bling da filial 2.'},
        {'pasta': 'email-02-seis-parcelas', 'assunto': 'NF-e 82002 - Eta Escapamentos',
         'anexos': [('F02', 'xml'), ('F02', 'pdf')],
         'testa': '6 duplicatas, com centavos sobrando na última; 3 vencem depois dos 30 dias',
         'esperado': 'NOTA:F02. No painel de 30 dias entram só as 3 primeiras.'},
        {'pasta': 'email-03-uma-parcela-vencida', 'assunto': 'NF-e 82003 - Alfa Autopecas',
         'anexos': [('F03', 'xml'), ('F03', 'pdf')],
         'testa': 'Nota de 20 dias atrás com uma parcela já vencida (há 4 dias)',
         'esperado': 'NOTA:F03. A vencida não entra no painel (que mostra de hoje em diante).'},
        {'pasta': 'email-04-lancar-manualmente-no-bling', 'assunto': 'NF-e 82004 - Delta Ferramentas',
         'anexos': [('F04', 'xml'), ('F04', 'pdf')],
         'testa': 'Nota à vista (sem duplicatas): lançar à mão no Bling e marcar "Já lancei no Bling"',
         'esperado': 'NOTA:F04'},
        {'pasta': 'email-05-falha-no-bling', 'assunto': 'NF-e 82005 - Omega Pecas',
         'anexos': [('F05', 'xml'), ('F05', 'pdf')],
         'testa': 'Fornecedor com CNPJ de dígito verificador errado',
         'esperado': 'NOTA:F05'},
        {'pasta': 'email-06-zip-com-duas-notas', 'assunto': 'Notas 82006 e 82007 (zip)',
         'anexos': [('arquivo', ARQUIVO_ZIP_DUAS)],
         'testa': 'Zip com duas NF-e de fornecedores diferentes',
         'esperado': 'NOTA:F06.<br>NOTA:F07.'},
        {'pasta': 'email-07-nota-repetida', 'assunto': 'Reenvio NF-e 82001',
         'anexos': [('F01', 'xml')],
         'testa': 'A nota do email-01 de novo, XML solto. **Mande depois do email-01.**',
         'esperado': 'Nada novo: a nota é reconhecida pela chave de acesso.'},
        {'pasta': 'email-08-arquivos-com-problema', 'assunto': 'Arquivos com problema',
         'anexos': [('F08', 'xml'), ('arquivo', ARQUIVO_CTE), ('arquivo', ARQUIVO_ZIP_CORROMPIDO)],
         'testa': 'XML cortado no meio, CT-e solto e zip corrompido',
         'esperado': 'Nenhuma nota registrada.<br>XML cortado e CT-e: em Falhas como "XML não reconhecido".'
                     '<br>Zip corrompido: ignorado sem registro.'},
    ]},
    {'pasta': 'cnpj-nao-cadastrado', 'titulo': 'CNPJ não cadastrado', 'letra': 'X', 'caixa': CAIXA_REDE, 'emails': [
        {'pasta': 'email-01-empresa-nao-encontrada', 'assunto': 'NF-e 83001 - Epsilon Baterias',
         'anexos': [('X01', 'xml'), ('X01', 'pdf')],
         'testa': 'Nota para um CNPJ que não está cadastrado no Previsio',
         'esperado': 'NOTA:X01'},
    ]},
    {'pasta': 'caixa-errada', 'titulo': 'Caixa errada', 'letra': None, 'caixa': CAIXA_FILIAL, 'emails': [
        {'pasta': 'email-01-nota-da-matriz-na-caixa-da-filial', 'assunto': 'NF-e 84001 - Beta Lubrificantes',
         'anexos': [('C01', 'xml'), ('C01', 'pdf')],
         'testa': 'Nota emitida para a **matriz**, enviada para a caixa da **filial 2**',
         'esperado': 'NOTA:C01. Em Notas recebidas aparece o aviso "Chegou na caixa de Autopeças piloto filial 2".'},
        {'pasta': 'email-02-cnpj-nao-cadastrado-na-caixa-da-filial', 'assunto': 'NF-e 84002 - Gama Pneus',
         'anexos': [('C02', 'xml'), ('C02', 'pdf')],
         'testa': 'Nota para CNPJ não cadastrado, enviada para a caixa da filial 2',
         'esperado': 'NOTA:C02. Também com o aviso "Chegou na caixa de Autopeças piloto filial 2".'},
    ]},
]


# ---------------------------------------------------------------- resultado esperado

def lancada(dados):
    return dados['nota']['tipo'] == 'ok'


def contas(quantidade):
    if quantidade == 1:
        return '1 conta a pagar'
    return str(quantidade) + ' contas a pagar'


def resultado(dados):
    nota = dados['nota']
    tipo = nota['tipo']
    if tipo == 'nao-encontrada':
        return 'Pendente: "CNPJ não cadastrado" (' + base.br_doc(dados['destinatario']['doc']) + ')'
    if tipo == 'duplicatas-invalidas':
        return 'Pendente: "Sem vencimentos no XML". As duas duplicatas são inválidas e ignoradas'
    if tipo == 'sem-vencimentos':
        return 'Pendente: "Sem vencimentos no XML". Não vai para o Bling; siga o roteiro no fim deste arquivo'
    if tipo == 'falha-bling':
        return ('**Falha:** "Erro ao lançar". O Bling recusa o fornecedor (CNPJ ' + base.br_doc(lote2.CNPJ_INVALIDO)
                + ' com dígito errado). Aparece em Falhas, etapa "Cadastro do fornecedor", com a resposta do Bling')
    return contas(len(dados['parcelas'])) + ' na ' + EMPRESA_DA_LETRA[nota['dest']]


def texto_esperado(esperado, geradas):
    """Troca NOTA:M01 pelo resultado calculado da nota M01."""
    for nota_id, dados in geradas.items():
        esperado = esperado.replace('NOTA:' + nota_id, nota_id_numero(dados) + ': ' + resultado(dados))
    return esperado


def nota_id_numero(dados):
    return str(dados['nota']['numero'])


def no_painel_30_dias(parcela, hoje):
    dias = (parcela['vencimento'] - hoje).days
    return 0 <= dias <= 30


# ---------------------------------------------------------------- LEIAME

def gerar_leiame(geradas, args, hoje, sem_pdf):
    linhas = [MARCA_LEIAME, '# Notas fiscais de teste: terceiro lote, por empresa', '',
              'Geradas em ' + base.br_data(hoje) + ' por `gerar_notas_3.py`, série ' + str(args.serie) + '.',
              'Tudo fictício, em ambiente de homologação e **sem valor fiscal**.',
              'Números e chaves novos: não colidem com os lotes `arquivos` e `arquivos 2`, que já foram importados.', '']
    if sem_pdf:
        linhas.extend(['> PDFs não gerados (`--sem-pdf` ou navegador não encontrado).', ''])

    linhas.extend([
        '## Como está organizado', '',
        '| Pasta | Notas para | Mandar para |', '|---|---|---|',
    ])
    for grupo in GRUPOS:
        if grupo['letra'] is None:
            para = 'matriz e CNPJ não cadastrado (de propósito na caixa errada)'
        elif grupo['letra'] == 'X':
            para = 'CNPJ `' + base.br_doc(args.nao_cadastrada) + '` (não cadastre)'
        else:
            para = grupo['titulo'] + ' (`' + base.br_doc(args.empresas[0 if grupo['letra'] == 'A' else 1]) + '`)'
        linhas.append('| `' + grupo['pasta'] + '/` | ' + para + ' | ' + grupo['caixa'] + ' |')

    linhas.extend([
        '', 'Cada subpasta `email-NN-...` é **um e-mail**: anexe todos os arquivos dela e mande para a caixa da linha acima.',
        'A nota vai para a empresa pelo **CNPJ do destinatário no XML**, não pela caixa. A caixa só decide se aparece',
        'o aviso "Chegou na caixa de ..." (pasta `caixa-errada`).', '',
        '## Antes de enviar', '',
        '1. Em **Empresas**, a matriz e a filial 2 cadastradas e ativas, com o Bling conectado e o aplicativo',
        '   com permissão de **incluir** em Contatos e Contas a pagar.',
        '2. **Não** cadastre `' + base.br_doc(args.nao_cadastrada) + '`.',
        '3. Rode a migration nova (`dotnet ef database update`) para o aviso de caixa errada funcionar.',
        '4. Em cada pasta, mande os e-mails **na ordem dos números**: o email-02 da matriz depende do fornecedor que o',
        '   email-01 cadastra, e os reenvios precisam vir depois da nota original.', '',
    ])

    for grupo in GRUPOS:
        linhas.extend(['## `' + grupo['pasta'] + '/`: ' + grupo['titulo'], '',
                       'Mandar para ' + grupo['caixa'] + '.', '',
                       '| E-mail | Assunto sugerido | O que testa | Resultado esperado |', '|---|---|---|---|'])
        for email in grupo['emails']:
            linhas.append('| `' + email['pasta'] + '` | ' + email['assunto'] + ' | ' + email['testa'] + ' | '
                          + texto_esperado(email['esperado'], geradas) + ' |')
        linhas.append('')

    linhas.extend(['## Depois de enviar', '',
                   'Em **Notas pendentes**, como admin, clique em **Buscar e-mails agora** (ou espere o agendamento).',
                   'Em **Empresas**, **Sincronizar contas agora** traz as contas do Bling para o painel.', '',
                   '## Roteiro: notas à vista, lançadas manualmente no Bling', ''])
    for nota_id in ('M06', 'F04'):
        dados = geradas[nota_id]
        empresa = EMPRESA_DA_LETRA[dados['nota']['dest']]
        linhas.extend([
            '**' + nota_id_numero(dados) + ' (' + empresa + ', ' + base.br_dinheiro(dados['total']) + ')**', '',
            '1. Aparece em **Notas pendentes** como "Sem vencimentos no XML".',
            '2. No **Bling da ' + empresa + '**, crie a conta a pagar à mão: fornecedor ' + dados['emitente']['nome']
            + ' (CNPJ ' + base.br_doc(dados['emitente']['doc']) + '), valor ' + base.br_dinheiro(dados['total'])
            + ', nº do documento ' + nota_id_numero(dados) + '.',
            '3. No Previsio, clique em **Já lancei no Bling** e escreva uma observação.',
            '4. Em **Notas recebidas** ela fica como "Lançada manualmente", com quem e quando.', ''])

    registradas = [d for d in geradas.values() if d['nota']['tipo'] != 'corrompido']
    lancadas = [d for d in registradas if lancada(d)]
    total_contas = sum(len(d['parcelas']) for d in lancadas)
    linhas.extend([
        '## Conferência', '',
        '- **Notas registradas:** ' + str(len(registradas)) + '.',
        '- **Lançadas pelo Previsio:** ' + str(len(lancadas)) + ' notas, ' + str(total_contas) + ' contas a pagar.',
        '- **Pendentes ou com falha:** ' + ', '.join(nota_id_numero(d) for d in registradas if not lancada(d)) + '.',
        '- **Com aviso de caixa errada:** ' + nota_id_numero(geradas['C01']) + ' e ' + nota_id_numero(geradas['C02']) + '.',
        '', '| Nota | Fornecedor | Empresa | Nº do documento | Vencimento | Valor (R$) | No painel de 30 dias? |',
        '|---|---|---|---|---|---|---|'])
    for dados in lancadas:
        empresa = EMPRESA_DA_LETRA[dados['nota']['dest']]
        quantidade = len(dados['parcelas'])
        for parcela in dados['parcelas']:
            documento = nota_id_numero(dados)
            if quantidade > 1:
                documento = documento + '/' + str(int(parcela['numero']))
            painel = 'sim' if no_painel_30_dias(parcela, hoje) else 'não'
            linhas.append('| ' + nota_id_numero(dados) + ' | ' + dados['emitente']['nome'] + ' | ' + empresa + ' | '
                          + documento + ' | ' + base.br_data(parcela['vencimento']) + ' | '
                          + base.br_dinheiro(parcela['valor']).replace('R$ ', '') + ' | ' + painel + ' |')
    linhas.append('')
    return '\n'.join(linhas)


# ---------------------------------------------------------------- execução

def ler_argumentos():
    parser = argparse.ArgumentParser(description='Gera o terceiro lote de NF-e de teste para o Previsio, por empresa.')
    parser.add_argument('--empresas', nargs=2, metavar=('CNPJ_MATRIZ', 'CNPJ_FILIAL'),
                        default=[base.cnpj('11222333', '0001'), base.cnpj('11222333', '0002')],
                        help='CNPJs das duas empresas cadastradas no Previsio que recebem as notas')
    parser.add_argument('--nao-cadastrada', default=base.cnpj('11222333', '0003'),
                        help='CNPJ que NÃO está cadastrado')
    parser.add_argument('--serie', type=int, default=3, help='série das notas; mude para gerar chaves novas')
    parser.add_argument('--saida', default=str(Path(__file__).resolve().parent / 'arquivos 3'),
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


def nome_arquivo(nota):
    return nota['id'].lower() + '-nfe-' + str(nota['numero'])


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
    trabalho = Path(tempfile.mkdtemp(prefix='previsio-notas-3-'))
    try:
        arquivos = {}
        geradas = {}
        for nota in NOTAS:
            dados = base.calcular_nota(nota, FORNECEDORES[nota['emit']], destinatarios[nota['dest']], hoje, args.serie)
            geradas[nota['id']] = dados

            texto = base.xml_nfe(dados)
            if nota['tipo'] == 'duplicatas-invalidas':
                texto = lote2.xml_duplicatas_invalidas(texto)
            if nota['tipo'] == 'corrompido':
                texto = lote2.xml_corrompido(texto)
            caminho_xml = trabalho / (nome_arquivo(nota) + '.xml')
            base.gravar(caminho_xml, texto)
            arquivos[(nota['id'], 'xml')] = caminho_xml

            if not sem_pdf and nota['tipo'] != 'corrompido':
                caminho_html = trabalho / (nome_arquivo(nota) + '-danfe.html')
                base.gravar(caminho_html, base.danfe_html(dados))
                caminho_pdf = trabalho / (nome_arquivo(nota) + '-danfe.pdf')
                base.imprimir_pdf(navegador, caminho_html, caminho_pdf, trabalho / 'perfis-navegador')
                arquivos[(nota['id'], 'pdf')] = caminho_pdf
                print('  ' + caminho_pdf.name)

        caminho_cte = trabalho / ARQUIVO_CTE
        base.gravar(caminho_cte, lote2.xml_cte(hoje, args.empresas[1]))
        arquivos[('arquivo', ARQUIVO_CTE)] = caminho_cte

        # Cancelamento de uma nota que não está no zip (a M04): o Previsio não registra eventos
        caminho_cancelamento = trabalho / ARQUIVO_CANCELAMENTO
        base.gravar(caminho_cancelamento, base.xml_cancelamento(geradas['M04'], datetime.combine(hoje, time(11, 0))))

        caminho_misturado = trabalho / ARQUIVO_ZIP_MISTURADO
        with zipfile.ZipFile(caminho_misturado, 'w', zipfile.ZIP_DEFLATED) as pacote:
            pacote.write(arquivos[('M08', 'xml')], arquivos[('M08', 'xml')].name)
            if ('M08', 'pdf') in arquivos:
                pacote.write(arquivos[('M08', 'pdf')], arquivos[('M08', 'pdf')].name)
            pacote.write(caminho_cte, 'documentos/' + ARQUIVO_CTE)
            pacote.write(caminho_cancelamento, 'documentos/' + ARQUIVO_CANCELAMENTO)
        arquivos[('arquivo', ARQUIVO_ZIP_MISTURADO)] = caminho_misturado

        caminho_duas = trabalho / ARQUIVO_ZIP_DUAS
        with zipfile.ZipFile(caminho_duas, 'w', zipfile.ZIP_DEFLATED) as pacote:
            for nota_id in ('F06', 'F07'):
                pacote.write(arquivos[(nota_id, 'xml')], arquivos[(nota_id, 'xml')].name)
                if (nota_id, 'pdf') in arquivos:
                    pacote.write(arquivos[(nota_id, 'pdf')], arquivos[(nota_id, 'pdf')].name)
        arquivos[('arquivo', ARQUIVO_ZIP_DUAS)] = caminho_duas

        caminho_corrompido = trabalho / ARQUIVO_ZIP_CORROMPIDO
        caminho_corrompido.write_bytes(lote2.zip_corrompido())
        arquivos[('arquivo', ARQUIVO_ZIP_CORROMPIDO)] = caminho_corrompido

        caminho_reenvio = trabalho / ARQUIVO_REENVIO_ZIP
        with zipfile.ZipFile(caminho_reenvio, 'w', zipfile.ZIP_DEFLATED) as pacote:
            pacote.write(arquivos[('M01', 'xml')], 'reenvio-' + arquivos[('M01', 'xml')].name)
        arquivos[('arquivo', ARQUIVO_REENVIO_ZIP)] = caminho_reenvio

        for grupo in GRUPOS:
            for email in grupo['emails']:
                pasta = saida / grupo['pasta'] / email['pasta']
                pasta.mkdir(parents=True)
                for anexo in email['anexos']:
                    origem = arquivos.get(anexo)
                    if origem is not None:
                        shutil.copy2(origem, pasta / origem.name)

        base.gravar(saida / 'LEIAME.md', gerar_leiame(geradas, args, hoje, sem_pdf))
    finally:
        shutil.rmtree(trabalho, ignore_errors=True)

    print('Notas geradas em ' + str(saida))
    for grupo in GRUPOS:
        for email in grupo['emails']:
            pasta = saida / grupo['pasta'] / email['pasta']
            nomes = sorted(arquivo.name for arquivo in pasta.iterdir())
            print('  ' + grupo['pasta'] + '/' + email['pasta'] + ': ' + ', '.join(nomes))


if __name__ == '__main__':
    main()
