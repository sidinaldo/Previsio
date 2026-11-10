import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { App } from './app';
import { tituloDaRota } from './shared/sidebar/menu';

describe('App', () => {
  beforeEach(async () => {
    // O app chama o plugin do Bootstrap Material Design via jQuery global
    (globalThis as any).$ = () => ({ bootstrapMaterialDesign: () => undefined });

    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([])]
    }).compileComponents();
  });

  it('cria o app', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });
});

describe('tituloDaRota', () => {
  it('usa o título do item do menu', () => {
    expect(tituloDaRota('/pages/painel')).toBe('Painel');
    expect(tituloDaRota('/pages/notas-pendentes')).toBe('Notas pendentes');
  });

  it('reconhece telas filhas e ignora parâmetros', () => {
    expect(tituloDaRota('/pages/empresas/3')).toBe('Empresas');
    expect(tituloDaRota('/pages/empresas?bling=conectado')).toBe('Empresas');
  });

  it('usa "Previsio" quando a rota não está no menu', () => {
    expect(tituloDaRota('/pages/login')).toBe('Previsio');
  });
});
