import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { Saude, SaudeRede } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { SaudeComponent } from './saude.component';

function rede(clienteId: number, nome: string, atencao: boolean): SaudeRede {
  return {
    clienteId: clienteId, nome: nome, atencao: atencao, avisosAbertos: atencao ? 2 : 0,
    caixa: {
      usuario: 'notas@' + clienteId + '.com.br', ultimaLeituraEm: '2026-11-11T13:00:00Z',
      ultimaFalhaEm: atencao ? '2026-11-11T14:00:00Z' : null,
      ultimoErro: atencao ? 'Usuário ou senha recusados' : null, falhando: atencao
    },
    notasAguardando: 1, notasComProblema: atencao ? 3 : 0, ultimaNotaEm: '2026-11-11T12:00:00Z',
    empresas: [{
      id: clienteId * 10, nome: 'Loja ' + nome, cnpj: '11222333000181', blingConectado: !atencao,
      tokenValidoAte: atencao ? null : '2026-11-11T18:00:00Z', ultimaSincronizacaoEm: null, caixa: null,
      notasAguardando: 1, notasComProblema: atencao ? 3 : 0, avisosAbertos: atencao ? 2 : 0
    }]
  };
}

const SAUDE: Saude = {
  geradoEm: '2026-11-11T15:00:00Z',
  redesComAtencao: 1,
  jobs: [
    { id: 'leitura-email', nome: 'Leitura dos e-mails', situacao: 'Falhou', ultimaExecucaoEm: '2026-11-11T14:50:00Z',
      proximaExecucaoEm: '2026-11-11T15:00:00Z', erro: 'Banco fora do ar' },
    { id: 'lancamento', nome: 'Lançamento no Bling', situacao: 'Ok', ultimaExecucaoEm: '2026-11-11T14:50:00Z',
      proximaExecucaoEm: '2026-11-11T15:00:00Z', erro: null }
  ],
  redes: [rede(2, 'Rede Auto Center', true), rede(1, 'Autopeças Piloto', false)]
};

async function montar() {
  const api = { obterSaude: vi.fn(() => of(SAUDE)), obterEmailsIgnorados: vi.fn(() => of([])) };
  TestBed.configureTestingModule({
    imports: [SaudeComponent],
    providers: [provideRouter([]), { provide: PrevisioService, useValue: api }]
  });
  const fixture = TestBed.createComponent(SaudeComponent);
  fixture.autoDetectChanges();
  await fixture.whenStable();
  return { fixture, api, tela: fixture.nativeElement as HTMLElement };
}

describe('SaudeComponent', () => {
  it('mostra os jobs com a situação em português e o erro da falha', async () => {
    const { tela, api } = await montar();
    expect(api.obterSaude).toHaveBeenCalledTimes(1);
    const linhas = tela.querySelectorAll('.card')[0].querySelectorAll('tbody tr');
    expect(linhas.length).toBe(2);
    expect(linhas[0].textContent).toContain('Falhou');
    expect(linhas[0].textContent).toContain('Banco fora do ar');
    expect(linhas[0].querySelector('.badge')?.classList.contains('badge-danger')).toBe(true);
    expect(linhas[1].textContent).toContain('Em dia');
    expect(tela.querySelector('.pv-saude__resumo')?.textContent).toContain('1 de 2');
  });

  it('cada rede mostra a caixa, o Bling das empresas e se pede atenção', async () => {
    const { tela } = await montar();
    const redes = tela.querySelectorAll('.pv-saude__rede');
    expect(redes.length).toBe(2);
    expect(redes[0].textContent).toContain('Rede Auto Center');
    expect(redes[0].textContent).toContain('Atenção · 2 avisos');
    expect(redes[0].textContent).toContain('Usuário ou senha recusados');
    expect(redes[0].textContent).toContain('Desconectado');
    expect(redes[1].textContent).toContain('Em dia');
    expect(redes[1].textContent).toContain('Conectado');
    expect(redes[1].textContent).toContain('11.222.333/0001-81');
  });

  it('filtra por nome e só as que pedem atenção', async () => {
    const { fixture, tela } = await montar();
    fixture.componentInstance.mudarBusca('piloto');
    await fixture.whenStable();
    expect(tela.querySelectorAll('.pv-saude__rede').length).toBe(1);
    expect(tela.querySelector('.pv-saude__rede')?.textContent).toContain('Autopeças Piloto');

    fixture.componentInstance.mudarBusca('');
    fixture.componentInstance.mudarSoAtencao(true);
    await fixture.whenStable();
    expect(tela.querySelectorAll('.pv-saude__rede').length).toBe(1);
    expect(tela.querySelector('.pv-saude__rede')?.textContent).toContain('Rede Auto Center');
  });

  it('Atualizar consulta a API de novo', async () => {
    const { fixture, api, tela } = await montar();
    (tela.querySelector('.pv-pagina__acoes button') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(api.obterSaude).toHaveBeenCalledTimes(2);
  });
});
