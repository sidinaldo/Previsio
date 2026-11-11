import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, signal } from '@angular/core';

import { carregamentoInterceptor } from './carregamento.interceptor';
import { CarregamentoService } from './carregamento.service';
import { PREVISIO_CONFIG } from './previsio.config';
import { BarraCarregamentoComponent } from '../shared/barra-carregamento/barra-carregamento.component';
import { CarregandoDirective } from '../shared/carregando.directive';

const API = PREVISIO_CONFIG.apiUrl;

function configurar() {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(withInterceptors([carregamentoInterceptor])), provideHttpClientTesting()]
  });
  return {
    http: TestBed.inject(HttpClient),
    api: TestBed.inject(HttpTestingController),
    carregamento: TestBed.inject(CarregamentoService)
  };
}

describe('carregamento', () => {
  it('fica carregando até a última chamada à API terminar (com sucesso ou erro)', () => {
    const { http, api, carregamento } = configurar();
    expect(carregamento.carregando()).toBe(false);

    http.get(`${API}/empresas`).subscribe();
    http.get(`${API}/notas`).subscribe({ error: () => undefined });
    expect(carregamento.carregando()).toBe(true);

    api.expectOne(`${API}/empresas`).flush([]);
    expect(carregamento.carregando()).toBe(true);

    api.expectOne(`${API}/notas`).flush('erro', { status: 500, statusText: 'Erro' });
    expect(carregamento.carregando()).toBe(false);
  });

  it('chamadas a outros endereços não acendem a barra', () => {
    const { http, api, carregamento } = configurar();
    http.get('https://fonts.googleapis.com/css').subscribe();
    expect(carregamento.carregando()).toBe(false);
    api.expectOne('https://fonts.googleapis.com/css').flush('');
  });

  it('a barra aparece só enquanto há chamada em andamento', async () => {
    const { carregamento } = configurar();
    const fixture = TestBed.createComponent(BarraCarregamentoComponent);
    fixture.autoDetectChanges();
    const tela = fixture.nativeElement as HTMLElement;
    expect(tela.querySelector('.pv-barra-carregamento')).toBeNull();

    carregamento.iniciar();
    await fixture.whenStable();
    expect(tela.querySelector('.pv-barra-carregamento')).not.toBeNull();

    carregamento.terminar();
    await fixture.whenStable();
    expect(tela.querySelector('.pv-barra-carregamento')).toBeNull();
  });
});

@Component({
  imports: [CarregandoDirective],
  template: `<button type="button" [pvCarregando]="ocupado()">Salvar</button>`
})
class BotaoDeTeste {
  readonly ocupado = signal(false);
}

describe('pvCarregando', () => {
  it('marca o botão com o spinner e aria-busy enquanto a ação está em andamento', async () => {
    TestBed.configureTestingModule({ imports: [BotaoDeTeste] });
    const fixture = TestBed.createComponent(BotaoDeTeste);
    fixture.autoDetectChanges();
    const botao = (fixture.nativeElement as HTMLElement).querySelector('button')!;
    expect(botao.classList.contains('pv-botao--carregando')).toBe(false);
    expect(botao.getAttribute('aria-busy')).toBeNull();

    fixture.componentInstance.ocupado.set(true);
    await fixture.whenStable();
    expect(botao.classList.contains('pv-botao--carregando')).toBe(true);
    expect(botao.getAttribute('aria-busy')).toBe('true');
  });
});
