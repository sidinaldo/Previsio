import { HttpResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';

import { Dossie } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { RelatoriosComponent } from './relatorios.component';

function dossie(): Dossie {
  return {
    redeNome: 'Rede Auto Center', empresas: [{ nome: 'Matriz', cnpj: '11222333000181' }],
    geradoEm: '2026-11-14T14:00:00', dias: 30, de: '2026-11-01', ate: '2026-11-30',
    projecao: {
      inicio: '2026-11-14', fim: '2026-12-13', fluxoCaixaAtivo: true, totalEntradas: 3000, totalSaidas: 5000, saldo: -2000,
      saidasVencidas: 120, entradasVencidas: 0, mediaSaidasSemana: 1250, fatorSemanaCritica: 1.5, quantidadeCriticas: 1,
      menorSaldoAcumulado: -2000, semanaMenorSaldo: '2026-12-05', saldoNegativoEm: '2026-11-21',
      semanas: [
        { inicio: '2026-11-14', fim: '2026-11-20', entradas: 1500, saidas: 1000, saldo: 500, saldoAcumulado: 500, critica: false, motivos: [], saldoFicaNegativo: false },
        { inicio: '2026-11-21', fim: '2026-11-27', entradas: 500, saidas: 2500, saldo: -2000, saldoAcumulado: -1500, critica: true, motivos: ['Pico de saídas'], saldoFicaNegativo: true },
        { inicio: '2026-11-28', fim: '2026-12-04', entradas: 1000, saidas: 1500, saldo: -500, saldoAcumulado: -2000, critica: false, motivos: [], saldoFicaNegativo: false }
      ]
    },
    auditoria: {
      indice: 79, faixa: 'amarelo', faixaVerde: 90, faixaAmarela: 70, quantidadeReceberVencido: 0, valorReceberVencido: 0,
      indicadores: [
        { chave: 'categorias', nome: 'Contas com categoria', percentual: 90, faixa: 'verde', peso: 1, quantidade: 1, valor: 100, descricao: '1 conta sem categoria no Bling', quantidadeAConferir: 2 },
        { chave: 'notas', nome: 'Notas lançadas no Bling', percentual: 66, faixa: 'vermelho', peso: 1, quantidade: 3, valor: 300, descricao: '3 notas ainda não lançadas', quantidadeAConferir: 0 },
        { chave: 'em-dia', nome: 'Contas a pagar em dia', percentual: null, faixa: '', peso: 1, quantidade: 0, valor: 0, descricao: 'Nenhuma conta vencida em aberto', quantidadeAConferir: 0 }
      ]
    },
    comparativo: {
      totalSaidas: 1000, totalEntradas: 400, quantidadeContas: 3,
      empresas: [{ empresaId: 1, nome: 'Matriz', cnpj: '11222333000181', saidas: 1000, saidasPagas: 600, saidasEmAberto: 400, entradas: 400, quantidadeContas: 3, percentual: 100 }],
      fornecedores: [{ nome: 'ALFA', valor: 750, quantidade: 2, percentual: 75, tipo: 'fornecedor' }, { nome: 'Outros fornecedores', valor: 250, quantidade: 1, percentual: 25, tipo: 'outros' }],
      categorias: [{ nome: 'Compras de Mercadorias', valor: 900, quantidade: 2, percentual: 90, tipo: 'categoria' }, { nome: 'Sem categoria', valor: 100, quantidade: 1, percentual: 10, tipo: 'sem' }]
    }
  };
}

async function montar(falha = false) {
  const api = {
    obterDossie: vi.fn(() => {
      if (falha) {
        return throwError(() => new HttpErrorResponse({ status: 400, error: 'Escolha a projeção de 30, 60 ou 90 dias.' }));
      }
      return of(dossie());
    }),
    exportarDossie: vi.fn(() => of(new HttpResponse<Blob>({ body: new Blob() })))
  };
  TestBed.configureTestingModule({ imports: [RelatoriosComponent], providers: [provideRouter([]), { provide: PrevisioService, useValue: api }] });
  const fixture = TestBed.createComponent(RelatoriosComponent);
  fixture.autoDetectChanges();
  await fixture.whenStable();
  return { fixture, api, tela: fixture.nativeElement as HTMLElement };
}

describe('RelatoriosComponent', () => {
  it('abre com 30 dias e o mês atual e monta os três blocos do dossiê', async () => {
    const { api, tela } = await montar();
    const [dias, de, ate] = api.obterDossie.mock.calls[0] as unknown as [number, string, string];
    expect(dias).toBe(30);
    expect(de).toMatch(/^\d{4}-\d{2}-01$/);
    expect(ate >= de).toBe(true);

    expect(tela.querySelector('.pv-dossie__identificacao')?.textContent).toContain('Rede Auto Center');
    expect(tela.querySelector('.pv-dossie__identificacao')?.textContent).toContain('11.222.333/0001-81');
    expect(tela.querySelectorAll('.pv-dossie__titulo').length).toBe(3);

    // Projeção: pico em vermelho no gráfico e na tabela; semana em que o saldo fica negativo
    expect(tela.querySelectorAll('.pv-dossie__barra-saida--critica').length).toBe(1);
    expect(tela.querySelector('polyline.pv-dossie__linha-saldo')).not.toBeNull();
    const critica = tela.querySelector('tr.pv-dossie__critica') as HTMLElement;
    expect(critica.textContent).toContain('Pico de saídas');
    expect(critica.textContent).toContain('Saldo fica negativo');
    expect(tela.textContent).toContain('1 de 3');

    // Auditoria: índice, faixa e o link para arrumar
    expect(tela.querySelector('.pv-dossie__indice')?.className).toContain('pv-dossie__faixa--amarelo');
    expect(tela.querySelector('.pv-dossie__indice strong')?.textContent).toContain('79');
    const arrumar = Array.from(tela.querySelectorAll('a.pv-dossie__arrumar')).map(a => a.getAttribute('href'));
    expect(arrumar.length).toBe(2);
    expect(arrumar[0]).toContain('categoria=Sem%20categoria');
    expect(arrumar[1]).toContain('notas-pendentes');
    expect(tela.textContent).toContain('sem dados');

    // Comparativo e rankings
    expect(tela.textContent).toContain('ALFA');
    expect(tela.querySelector('.pv-dossie__ranking-outros')?.textContent).toContain('Outros fornecedores');
  });

  it('trocar o horizonte recarrega; Exportar PDF abre a impressão e o Excel usa os mesmos filtros', async () => {
    const { fixture, api, tela } = await montar();
    const dias = tela.querySelector('#dossie-dias') as HTMLSelectElement;
    dias.value = '90';
    dias.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect((api.obterDossie.mock.calls[1] as unknown as [number])[0]).toBe(90);

    const imprimir = vi.spyOn(window, 'print').mockImplementation(() => undefined);
    const pdf = Array.from(tela.querySelectorAll('button')).find(b => b.textContent?.includes('Exportar PDF')) as HTMLButtonElement;
    pdf.click();
    expect(imprimir).toHaveBeenCalled();

    fixture.componentInstance.exportar();
    expect((api.exportarDossie.mock.calls[0] as unknown as [number])[0]).toBe(90);
  });

  it('mostra o motivo quando a API recusa', async () => {
    const { tela } = await montar(true);
    expect(tela.querySelector('.alert-danger')?.textContent).toContain('30, 60 ou 90');
  });
});
