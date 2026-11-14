import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { SessaoService } from '../../core/sessao.service';
import { ConfiguracoesComponent } from './configuracoes.component';

const API = 'http://localhost:5080/api';

describe('ConfiguracoesComponent', () => {
  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem('previsio.sessao', JSON.stringify({
      token: 't', expiraEm: '2099-01-01T00:00:00Z',
      usuario: { id: 1, clienteId: null, nome: 'Admin', email: 'a@b.c', perfil: 'Admin', ativo: true, ultimoAcessoEm: null }
    }));
  });

  it('mostra as 5 seções da rede escolhida, cada uma com título fora do card', async () => {
    TestBed.configureTestingModule({
      imports: [ConfiguracoesComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    });
    TestBed.inject(SessaoService).escolherCliente(7);
    const fixture = TestBed.createComponent(ConfiguracoesComponent);
    fixture.autoDetectChanges();
    const api = TestBed.inject(HttpTestingController);
    api.expectOne(`${API}/clientes/7/conferencia-bling`).flush({ conferirAntesDeLancar: true, exigeNumeroNota: false, toleranciaValor: 0.1, toleranciaDias: 0, fecharPendentes: true });
    api.expectOne(`${API}/clientes/7/fluxo-caixa`).flush({ ativo: true });
    api.expectOne(`${API}/clientes/7/categorias`).flush({ categoriaPadrao: null, padraoAusenteEm: [], opcoes: ['Compras'], regras: [], fornecedores: [], empresasSemCategorias: [] });
    api.expectOne(`${API}/clientes/7/alertas-vencimento`).flush({ avisoVencidasDias: 1, alertaDiarioAtivo: true, alertaDiarioHora: 7 });
    api.expectOne(`${API}/clientes/7/resumo-semanal`).flush({ ativo: true, dia: 1, hora: 7, ultimoEnvioEm: null, destinatarios: 1 });
    await fixture.whenStable();
    const tela = fixture.nativeElement as HTMLElement;

    const titulos = Array.from(tela.querySelectorAll('.pv-secao__cabecalho h2')).map(h => h.textContent!.trim());
    expect(titulos).toEqual(['Conferência com o Bling', 'Fluxo de caixa', 'Categorias', 'Avisos de vencimento', 'Resumo por e-mail']);
    // Título e descrição ficam fora do card
    expect(tela.querySelector('.pv-secao__card .pv-card__titulo')).toBeNull();
    expect(tela.querySelector('#categorias .pv-secao')).not.toBeNull();
    // Conferência: os 3 campos dependentes no sub-bloco
    const dependentes = tela.querySelector('.pv-config__dependentes') as HTMLElement;
    expect(dependentes.querySelector('#conferencia-criterio')).not.toBeNull();
    expect(dependentes.querySelector('#conferencia-valor')).not.toBeNull();
    expect(dependentes.querySelector('#conferencia-dias')).not.toBeNull();
    // Avisos: dias e e-mail diário lado a lado
    const colunas = tela.querySelector('.pv-config__duas-colunas') as HTMLElement;
    expect(colunas.querySelector('#alerta-dias')).not.toBeNull();
    expect(colunas.querySelector('#alerta-hora')).not.toBeNull();
    api.verify();
  });
});
