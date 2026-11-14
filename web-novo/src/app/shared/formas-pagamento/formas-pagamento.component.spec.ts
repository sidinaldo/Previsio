import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { ConfiguracaoFormasPagamento } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { FormasPagamentoComponent } from './formas-pagamento.component';

function configuracao(parcial: Partial<ConfiguracaoFormasPagamento> = {}): ConfiguracaoFormasPagamento {
  return {
    formaPadrao: null,
    padraoAutomatico: 'Boleto',
    padraoAusenteEm: [],
    opcoes: [
      { nome: 'Boleto', grupo: 'boleto', grupoNome: 'Boleto', saiSozinho: false },
      { nome: 'Débito em conta', grupo: 'debito-automatico', grupoNome: 'Débito automático', saiSozinho: true },
      { nome: 'Pix', grupo: 'pix', grupoNome: 'Pix', saiSozinho: false }
    ],
    regras: [{ id: 3, cnpjFornecedor: '12345678000199', fornecedorNome: 'ENERGIA SA', forma: 'Débito em conta', ausenteEm: ['Norte'] }],
    fornecedores: [{ cnpj: '12345678000199', nome: 'ENERGIA SA' }, { cnpj: '98765432000155', nome: 'AUTOPEÇAS ABC' }],
    empresasSemFormas: [],
    gruposSaiSozinho: ['Débito automático'],
    ...parcial
  };
}

describe('FormasPagamentoComponent', () => {
  async function montar(dados: ConfiguracaoFormasPagamento) {
    const api = {
      obterFormasPagamento: vi.fn(() => of(dados)),
      alterarFormaPagamentoPadrao: vi.fn((_: number, forma: string) => of({ ...dados, formaPadrao: forma || null })),
      salvarRegraFormaPagamento: vi.fn(() => of(dados)),
      removerRegraFormaPagamento: vi.fn(() => of({ ...dados, regras: [] }))
    };
    TestBed.configureTestingModule({ imports: [FormasPagamentoComponent], providers: [{ provide: PrevisioService, useValue: api }] });
    const fixture = TestBed.createComponent(FormasPagamentoComponent);
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

  it('mostra a automática, o grupo de cada forma e as regras com o que sai sozinho', async () => {
    const { tela, api } = await montar(configuracao());
    expect(api.obterFormasPagamento).toHaveBeenCalledWith(7);
    const padrao = tela.querySelector('#forma-padrao') as HTMLSelectElement;
    expect(padrao.value).toBe('');
    expect(padrao.options[0].textContent).toContain('Automática (Boleto)');
    expect(padrao.textContent).toContain('Débito em conta (Débito automático)');
    const regras = tela.querySelector('.pv-categorias__regras')?.textContent ?? '';
    expect(regras).toContain('ENERGIA SA');
    expect(regras).toContain('sai sozinho');
    expect(regras).toContain('Não existe no Bling de Norte');
    expect(tela.textContent).toContain('Débito automático sai sozinho da conta');
  });

  it('salva a forma padrão e adiciona regra só com fornecedor e forma', async () => {
    const { fixture, tela, api } = await montar(configuracao());
    escolher(tela, '#forma-padrao', 'Pix');
    (tela.querySelector('.pv-config__acoes .btn-info') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(api.alterarFormaPagamentoPadrao).toHaveBeenCalledWith(7, 'Pix');
    expect(tela.querySelector('.alert-success')?.textContent).toContain('Forma padrão salva');

    const adicionar = Array.from(tela.querySelectorAll('button')).find(b => b.textContent?.includes('Adicionar regra')) as HTMLButtonElement;
    expect(adicionar.disabled).toBe(true);
    escolher(tela, '#regra-forma-fornecedor', '98765432000155');
    escolher(tela, '#regra-forma', 'Boleto');
    fixture.detectChanges();
    adicionar.click();
    await fixture.whenStable();
    expect(api.salvarRegraFormaPagamento).toHaveBeenCalledWith(7, { cnpjFornecedor: '98765432000155', forma: 'Boleto' });
  });

  it('remove a regra e avisa a empresa sem formas', async () => {
    const { fixture, tela, api } = await montar(configuracao({ empresasSemFormas: ['Filial Sul'] }));
    expect(tela.querySelector('.alert-warning')?.textContent).toContain('Filial Sul');
    (tela.querySelector('.pv-categorias__regras button') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(api.removerRegraFormaPagamento).toHaveBeenCalledWith(7, 3);
    expect(tela.textContent).toContain('Regra removida');
  });
});
