import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { ConfiguracaoCategorias } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { CategoriasComponent } from './categorias.component';

function configuracao(parcial: Partial<ConfiguracaoCategorias> = {}): ConfiguracaoCategorias {
  return {
    categoriaPadrao: 'Compras',
    padraoAusenteEm: ['Norte'],
    opcoes: ['Compras', 'Fretes'],
    regras: [{ id: 3, cnpjFornecedor: '12345678000199', fornecedorNome: 'ROTA SUL', categoria: 'Fretes', ausenteEm: [] }],
    fornecedores: [{ cnpj: '12345678000199', nome: 'ROTA SUL' }, { cnpj: '98765432000155', nome: 'AUTOPEÇAS ABC' }],
    empresasSemCategorias: [],
    ...parcial
  };
}

describe('CategoriasComponent', () => {
  async function montar(dados: ConfiguracaoCategorias) {
    const api = {
      obterCategorias: vi.fn(() => of(dados)),
      alterarCategoriaPadrao: vi.fn((_: number, categoria: string) => of({ ...dados, categoriaPadrao: categoria || null, padraoAusenteEm: [] })),
      salvarRegraCategoria: vi.fn(() => of(dados)),
      removerRegraCategoria: vi.fn(() => of({ ...dados, regras: [] }))
    };
    TestBed.configureTestingModule({ imports: [CategoriasComponent], providers: [{ provide: PrevisioService, useValue: api }] });
    const fixture = TestBed.createComponent(CategoriasComponent);
    fixture.componentRef.setInput('clienteId', 7);
    fixture.autoDetectChanges();
    await fixture.whenStable();
    return { fixture, api, tela: fixture.nativeElement as HTMLElement };
  }

  function escolher(tela: HTMLElement, seletor: string, valor: string): void {
    const campo = tela.querySelector(seletor) as HTMLSelectElement;
    campo.value = valor;
    campo.dispatchEvent(new Event('change'));
  }

  it('mostra a padrão, as regras e o aviso de empresa sem a categoria', async () => {
    const { tela, api } = await montar(configuracao());
    expect(api.obterCategorias).toHaveBeenCalledWith(7);
    expect((tela.querySelector('#categoria-padrao') as HTMLSelectElement).value).toBe('Compras');
    expect(tela.textContent).toContain('Não existe no Bling de Norte');
    expect(tela.querySelector('.pv-categorias__regras')?.textContent).toContain('ROTA SUL');
    expect(tela.querySelector('.pv-categorias__regras')?.textContent).toContain('12.345.678/0001-99');
  });

  it('salva a categoria padrão', async () => {
    const { fixture, tela, api } = await montar(configuracao());
    escolher(tela, '#categoria-padrao', 'Fretes');
    (tela.querySelector('.pv-resumo__linha .btn-info') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(api.alterarCategoriaPadrao).toHaveBeenCalledWith(7, 'Fretes');
    expect(tela.querySelector('.alert-success')?.textContent).toContain('Categoria padrão salva');
  });

  it('adiciona regra só com fornecedor e categoria escolhidos', async () => {
    const { fixture, tela, api } = await montar(configuracao());
    const botao = Array.from(tela.querySelectorAll('button')).find(b => b.textContent?.includes('Adicionar regra')) as HTMLButtonElement;
    expect(botao.disabled).toBe(true);
    escolher(tela, '#regra-fornecedor', '98765432000155');
    escolher(tela, '#regra-categoria', 'Compras');
    await fixture.whenStable();
    expect(botao.disabled).toBe(false);
    botao.click();
    await fixture.whenStable();
    expect(api.salvarRegraCategoria).toHaveBeenCalledWith(7, { cnpjFornecedor: '98765432000155', categoria: 'Compras' });
  });

  it('remove a regra', async () => {
    const { fixture, tela, api } = await montar(configuracao());
    (tela.querySelector('.pv-categorias__regras button') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(api.removerRegraCategoria).toHaveBeenCalledWith(7, 3);
    expect(tela.querySelector('.pv-categorias__regras')).toBeNull();
  });

  it('avisa as empresas ainda sem categorias e sem opções não mostra o formulário', async () => {
    const { tela } = await montar(configuracao({ opcoes: [], regras: [], empresasSemCategorias: ['Centro'], categoriaPadrao: null, padraoAusenteEm: [] }));
    expect(tela.querySelector('.alert-warning')?.textContent).toContain('Centro');
    expect(tela.querySelector('#categoria-padrao')).toBeNull();
  });

  it('sem padrão escolhida mostra a automática na primeira opção', async () => {
    const { fixture, tela, api } = await montar(configuracao({ categoriaPadrao: null, padraoAutomatico: 'Compras de Mercadorias' }));
    const select = tela.querySelector('#categoria-padrao') as HTMLSelectElement;
    expect(select.value).toBe('');
    expect(select.options[0].textContent).toContain('Automática (Compras de Mercadorias)');
    (tela.querySelector('.pv-resumo__linha .btn-info') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(api.alterarCategoriaPadrao).toHaveBeenCalledWith(7, '');
    expect(tela.querySelector('.alert-success')?.textContent).toContain('vão para Compras de Mercadorias');
  });
});
