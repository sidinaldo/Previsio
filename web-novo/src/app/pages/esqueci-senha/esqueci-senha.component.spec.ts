import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { EsqueciSenhaComponent } from './esqueci-senha.component';
import { PREVISIO_CONFIG } from '../../core/previsio.config';

const API = PREVISIO_CONFIG.apiUrl;

describe('EsqueciSenhaComponent', () => {
  it('envia o e-mail e mostra a mesma confirmação, sem dizer se ele existe', async () => {
    TestBed.configureTestingModule({
      imports: [EsqueciSenhaComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    });
    const fixture = TestBed.createComponent(EsqueciSenhaComponent);
    fixture.autoDetectChanges();
    const api = TestBed.inject(HttpTestingController);
    const tela = fixture.nativeElement as HTMLElement;

    fixture.componentInstance.email.set('  ana@autocenter.com.br ');
    fixture.componentInstance.enviar();
    const chamada = api.expectOne(`${API}/auth/esqueci-senha`);
    expect(chamada.request.body).toEqual({ email: 'ana@autocenter.com.br' });
    chamada.flush('Se o e-mail estiver cadastrado, enviamos um link.');
    await fixture.whenStable();

    expect(tela.textContent).toContain('Verifique seu e-mail');
    expect(tela.textContent).toContain('ana@autocenter.com.br');
  });
});
