"""Notas fiscais (NF-e) de TESTE para gravar o vídeo de demonstração do Previsio.

Só notas que dão certo (viram conta a pagar no Bling), separadas pelas duas lojas da rede
de demonstração. Cada pasta de loja tem os e-mails dela; o LEIAME.md diz para qual caixa mandar.
Números e série próprios (série 4), para não colidir com os lotes de teste já importados.
Tudo fictício e em ambiente de homologação ("SEM VALOR FISCAL").

Uso:
    python gerar_notas_video.py
    python gerar_notas_video.py --serie 5      (chaves novas, para gravar de novo sem limpar o banco)
"""

import argparse
import shutil
import sys
import tempfile
import unicodedata
from datetime import date
from pathlib import Path

import gerar_notas as base
import gerar_notas_3 as lote3

MARCA_LEIAME = '<!-- gerado por gerar_notas_video.py -->'

# A = CNPJ 0001-81, B = CNPJ 0002-62 (mesmos códigos do gerar_notas.py)
LOJAS = {
    'A': {'pasta': 'auto-center-zona-norte', 'nome': 'Auto Center Zona Norte',
          'caixa': 'a caixa da **rede**'},
    'B': {'pasta': 'auto-center-centro', 'nome': 'Auto Center Centro',
          'caixa': 'a caixa **própria do Auto Center Centro** (sidinaldobarbosa@gmail.com)'},
}

# Vencimentos espalhados pelas próximas semanas, para o gráfico do painel ficar cheio.
# 'ao_vivo': a nota para mandar durante a gravação, mostrando o caminho e-mail → Bling → painel.
NOTAS = [
    # ---- Auto Center Zona Norte
    {'id': 'N01', 'numero': 91001, 'emit': 'alfa', 'dest': 'A', 'emitida_ha': 3,
     'itens': [('PAST', 12), ('FILT', 20)], 'vencimentos': [4, 18]},
    {'id': 'N02', 'numero': 91002, 'emit': 'beta', 'dest': 'A', 'emitida_ha': 2,
     'itens': [('OLEO', 48)], 'vencimentos': [9, 23]},
    {'id': 'N03', 'numero': 91003, 'emit': 'gama', 'dest': 'A', 'emitida_ha': 2,
     'itens': [('PNEU', 8)], 'vencimentos': [12, 26, 40]},
    {'id': 'N04', 'numero': 91004, 'emit': 'epsilon', 'dest': 'A', 'emitida_ha': 1,
     'itens': [('BAT', 4)], 'vencimentos': [15]},
    {'id': 'N05', 'numero': 91005, 'emit': 'iota', 'dest': 'A', 'emitida_ha': 0, 'ao_vivo': True,
     'itens': [('AMORT', 4), ('CORREIA', 6)], 'vencimentos': [7, 21]},
    # ---- Auto Center Centro
    {'id': 'C01', 'numero': 92001, 'emit': 'kappa', 'dest': 'B', 'emitida_ha': 3,
     'itens': [('AMORT', 6)], 'vencimentos': [5, 19]},
    {'id': 'C02', 'numero': 92002, 'emit': 'eta', 'dest': 'B', 'emitida_ha': 2,
     'itens': [('PNEU', 4), ('VELA', 24)], 'vencimentos': [8, 22]},
    {'id': 'C03', 'numero': 92003, 'emit': 'delta', 'dest': 'B', 'emitida_ha': 2,
     'itens': [('CHAVE', 6)], 'vencimentos': [11]},
    {'id': 'C04', 'numero': 92004, 'emit': 'beta', 'dest': 'B', 'emitida_ha': 1,
     'itens': [('OLEO', 36), ('FILT', 12)], 'vencimentos': [14, 28]},
    {'id': 'C05', 'numero': 92005, 'emit': 'aco', 'dest': 'B', 'emitida_ha': 0, 'ao_vivo': True,
     'itens': [('CHAVE', 2), ('AMORT', 2)], 'vencimentos': [10]},
]


def descricao_parcelas(dados):
    quantidade = len(dados['parcelas'])
    if quantidade == 1:
        return '1 conta a pagar'
    return str(quantidade) + ' contas a pagar'


def sem_acentos(texto):
    return unicodedata.normalize('NFKD', texto).encode('ascii', 'ignore').decode('ascii')


def nome_pasta(numero_email, dados):
    fantasia = dados['emitente']['fantasia'] or dados['emitente']['nome']
    slug = ''.join(c if c.isalnum() else '-' for c in sem_acentos(fantasia).lower()).strip('-')
    while '--' in slug:
        slug = slug.replace('--', '-')
    prefixo = 'email-' + str(numero_email).zfill(2)
    if dados['nota'].get('ao_vivo'):
        prefixo = prefixo + '-AO-VIVO'
    return prefixo + '-nfe-' + str(dados['nota']['numero']) + '-' + slug


def gerar_leiame(geradas, pastas, args, hoje, sem_pdf):
    linhas = [MARCA_LEIAME, '# Notas para o vídeo de demonstração', '',
              'Geradas em ' + base.br_data(hoje) + ' por `gerar_notas_video.py`, série ' + str(args.serie) + '.',
              'Tudo fictício, em ambiente de homologação e **sem valor fiscal**. Só notas que viram conta a pagar.', '']
    if sem_pdf:
        linhas.extend(['> PDFs não gerados (`--sem-pdf` ou navegador não encontrado).', ''])

    linhas.extend([
        '## Antes de gravar', '',
        '1. Mande os e-mails **sem** "AO-VIVO" no nome, cada um para a caixa da loja (tabela abaixo).',
        '2. No Previsio, em **Notas pendentes**, clique em **Buscar e-mails agora**.',
        '3. Em **Empresas**, clique em **Sincronizar contas agora**: o painel fica com o gráfico das próximas semanas.',
        '4. Deixe abertos: a caixa de e-mail, o Bling da loja e o Previsio.', '',
        '## Durante a gravação', '',
        'Mande o e-mail **AO-VIVO** de uma das lojas, clique em **Buscar e-mails agora** e mostre:',
        'a nota em **Notas recebidas**, a conta a pagar no **Bling** da loja e o valor entrando no **painel**.', '',
    ])

    for letra in ('A', 'B'):
        loja = LOJAS[letra]
        linhas.extend(['## `' + loja['pasta'] + '/`: ' + loja['nome'], '',
                       'CNPJ `' + base.br_doc(args.empresas[0 if letra == 'A' else 1]) + '`. Mandar para ' + loja['caixa'] + '.', '',
                       '| E-mail | Assunto sugerido | Valor | Vencimentos | Resultado |', '|---|---|---|---|---|'])
        for nota in NOTAS:
            if nota['dest'] != letra:
                continue
            dados = geradas[nota['id']]
            fantasia = dados['emitente']['fantasia'] or dados['emitente']['nome']
            vencimentos = ', '.join(base.br_data(p['vencimento'])[:5] for p in dados['parcelas'])
            linhas.append('| `' + pastas[nota['id']] + '` | NF-e ' + str(nota['numero']) + ' - ' + fantasia.title()
                          + ' | ' + base.br_dinheiro(dados['total']) + ' | ' + vencimentos + ' | '
                          + descricao_parcelas(dados) + ' no Bling da loja |')
        total = sum((geradas[n['id']]['total'] for n in NOTAS if n['dest'] == letra), base.Decimal('0.00'))
        linhas.extend(['', 'Total da loja: **' + base.br_dinheiro(total) + '**.', ''])
    return '\n'.join(linhas)


def ler_argumentos():
    parser = argparse.ArgumentParser(description='Gera as notas de teste para o vídeo de demonstração do Previsio.')
    parser.add_argument('--empresas', nargs=2, metavar=('CNPJ_ZONA_NORTE', 'CNPJ_CENTRO'),
                        default=[base.cnpj('11222333', '0001'), base.cnpj('11222333', '0002')],
                        help='CNPJs do Auto Center Zona Norte e do Auto Center Centro')
    parser.add_argument('--serie', type=int, default=4, help='série das notas; mude para gerar chaves novas')
    parser.add_argument('--saida', default=str(Path(__file__).resolve().parent / 'arquivos video'),
                        help='pasta de saída (é recriada a cada execução)')
    parser.add_argument('--navegador', help='caminho do msedge.exe ou chrome.exe para gerar os PDFs')
    parser.add_argument('--sem-pdf', action='store_true', help='não gera os DANFEs em PDF')
    args = parser.parse_args()
    args.empresas = [base.somente_digitos(args.empresas[0]), base.somente_digitos(args.empresas[1])]
    for documento in args.empresas:
        if len(documento) != 14:
            parser.error('CNPJ precisa ter 14 dígitos: ' + documento)
    if args.serie < 1 or args.serie > 999:
        parser.error('--serie vai de 1 a 999')
    # montar_destinatarios também monta o CNPJ não cadastrado (não usado aqui)
    args.nao_cadastrada = base.cnpj('11222333', '0003')
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
    trabalho = Path(tempfile.mkdtemp(prefix='previsio-notas-video-'))
    try:
        geradas = {}
        pastas = {}
        contador = {'A': 0, 'B': 0}
        for nota in NOTAS:
            dados = base.calcular_nota(nota, lote3.FORNECEDORES[nota['emit']], destinatarios[nota['dest']], hoje, args.serie)
            geradas[nota['id']] = dados
            contador[nota['dest']] += 1
            pastas[nota['id']] = nome_pasta(contador[nota['dest']], dados)

            pasta = saida / LOJAS[nota['dest']]['pasta'] / pastas[nota['id']]
            pasta.mkdir(parents=True)
            nome = 'nfe-' + str(nota['numero'])
            base.gravar(pasta / (nome + '.xml'), base.xml_nfe(dados))

            if not sem_pdf:
                caminho_html = trabalho / (nome + '-danfe.html')
                base.gravar(caminho_html, base.danfe_html(dados))
                base.imprimir_pdf(navegador, caminho_html, pasta / (nome + '-danfe.pdf'), trabalho / 'perfis-navegador')

        base.gravar(saida / 'LEIAME.md', gerar_leiame(geradas, pastas, args, hoje, sem_pdf))
    finally:
        shutil.rmtree(trabalho, ignore_errors=True)

    print('Notas geradas em ' + str(saida))
    for nota in NOTAS:
        print('  ' + LOJAS[nota['dest']]['pasta'] + '/' + pastas[nota['id']])


if __name__ == '__main__':
    main()
