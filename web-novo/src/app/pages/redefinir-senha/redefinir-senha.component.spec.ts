import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';

import { RedefinirSenhaComponent } from './redefinir-senha.component';
import { PREVISIO_CONFIG } from '../../core/previsio.config';

const API = PREVISIO_CONFIG.apiUrl;

function montar(token: string | null) {
  localStorage.clear();
  TestBed.configureTestingModule({
    imports: [RedefinirSenhaComponent],
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(token === null ? {} : { token: token }) } } }
    ]
  });
  const fixture = TestBed.createComponent(RedefinirSenhaComponent);
  fixture.autoDetectChanges();
  return { fixture, api: TestBed.inject(HttpTestingController), tela: fixture.nativeElement as HTMLElement };
}

describe('RedefinirSenhaComponent', () => {
  it('sem token no link mostra o atalho para pedir outro', async () => {
    const { fixture, tela } = montar(null);
    await fixture.whenStable();
    expect(tela.textContent).toContain('Link incompleto');
    expect(tela.querySelector('form')).toBeNull();
  });

  it('senhas diferentes não chamam a API', async () => {
    const { fixture, api, tela } = montar('abc');
    fixture.componentInstance.senha.set('senha-nova-1');
    fixture.componentInstance.confirmacao.set('senha-nova-2');
    fixture.componentInstance.salvar();
    await fixture.whenStable();
    api.expectNone(`${API}/auth/redefinir-senha`);
    expect(tela.textContent).toContain('As duas senhas não são iguais.');
  });

  it('salva e volta para o login com o aviso de senha alterada', async () => {
    const { fixture, api } = montar('abc');
    const navegar = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    fixture.componentInstance.senha.set('senha-nova-1');
    fixture.componentInstance.confirmacao.set('senha-nova-1');
    fixture.componentInstance.salvar();

    const chamada = api.expectOne(`${API}/auth/redefinir-senha`);
    expect(chamada.request.body).toEqual({ token: 'abc', novaSenha: 'senha-nova-1' });
    chamada.flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();

    expect(navegar).toHaveBeenCalledWith(['/pages/login'], { queryParams: { motivo: 'senha-redefinida' } });
  });

  it('link expirado mostra a mensagem da API e o atalho para pedir outro', async () => {
    const { fixture, api, tela } = montar('velho');
    fixture.componentInstance.senha.set('senha-nova-1');
    fixture.componentInstance.confirmacao.set('senha-nova-1');
    fixture.componentInstance.salvar();
    api.expectOne(`${API}/auth/redefinir-senha`).flush(
      'Este link de redefinição é inválido ou expirou. Peça um novo em "Esqueci minha senha".',
      { status: 400, statusText: 'Bad Request' });
    await fixture.whenStable();

    expect(tela.textContent).toContain('inválido ou expirou');
    expect(tela.querySelector('.alert a')!.textContent).toContain('Pedir novo link');
  });
});
