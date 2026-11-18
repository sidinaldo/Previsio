import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { NotaRecebida, PaginaNotas } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { SessaoService } from '../../core/sessao.service';
import { NotasRecebidasComponent } from './notas-recebidas.component';
import { erpDeTeste } from '../../core/erp-de-teste';

function nota(id: number, categoria: string | null, categoriaTipo: string | null): NotaRecebida {
  return {
    id, empresaId: 1, empresaNome: 'Loja Centro', numero: String(1000 + id), serie: '1', chaveAcesso: '',
    fornecedor: 'ALFA', cnpjFornecedor: '12345678000199', dataEmissao: '2026-11-10', valorTotal: 100,
    primeiroVencimento: '2026-11-20', status: categoriaTipo ? 'Lancada' : 'Erro', mensagemErro: null,
    recebidaEm: '2026-11-10T10:00:00', lancadaEm: null, categoria, categoriaTipo, parcelas: [],
    resolvidaManualmenteEm: null, resolvidaManualmentePor: null, observacaoResolucao: null
  } as NotaRecebida;
}

const PAGINA: PaginaNotas = {
  itens: [
    nota(1, 'Compras de Mercadorias', 'categoria'),
    nota(2, 'Sem categoria', 'sem'),
    nota(3, 'A conferir', 'conferir'),
    nota(4, 'Várias categorias', 'varias'),
    nota(5, null, null)
  ],
  total: 5, pagina: 1, tamanhoPagina: 25
};

describe('NotasRecebidasComponent', () => {
  beforeEach(() => localStorage.clear());

  it('mostra a categoria das contas no Bling, com link para Contas a pagar', async () => {
    const api = { obterNotas: vi.fn(() => of(PAGINA)), listarEmpresas: vi.fn(() => of([])), exportarNotas: vi.fn() };
    TestBed.configureTestingModule({
      imports: [NotasRecebidasComponent],
      providers: [erpDeTeste(), 
        provideRouter([]),
        { provide: PrevisioService, useValue: api },
        { provide: SessaoService, useValue: { clienteId: signal(2), ehAdmin: signal(false) } },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } }
      ]
    });
    const fixture = TestBed.createComponent(NotasRecebidasComponent);
    fixture.autoDetectChanges();
    await fixture.whenStable();
    const tela = fixture.nativeElement as HTMLElement;

    const cabecalho = Array.from(tela.querySelectorAll('thead th')).map(th => th.textContent?.trim());
    expect(cabecalho).toContain('Categoria');
    const linhas = tela.querySelectorAll('tbody > tr');
    const link = linhas[0].querySelector('a[href*="contas-pagar"]') as HTMLAnchorElement;
    expect(link.textContent).toContain('Compras de Mercadorias');
    expect(link.getAttribute('href')).toContain('categoria=Compras%20de%20Mercadorias');
    expect(linhas[1].textContent).toContain('Sem categoria');
    expect(linhas[1].querySelector('a[href*="contas-pagar"]')).toBeNull();
    expect(linhas[2].textContent).toContain('A conferir');
    expect(linhas[3].textContent).toContain('Várias categorias');
    expect(linhas[4].textContent).toContain('—');
  });
});
