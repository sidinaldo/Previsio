import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';

import { LoginComponent } from './login.component';
import { PREVISIO_CONFIG } from '../../core/previsio.config';

function montar(parametros: { [nome: string]: string }) {
  TestBed.configureTestingModule({
    imports: [LoginComponent],
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(parametros) } } }
    ]
  });
  const fixture = TestBed.createComponent(LoginComponent);
  fixture.autoDetectChanges();
  return { fixture, tela: fixture.nativeElement as HTMLElement };
}

describe('LoginComponent (visual)', () => {
  beforeEach(() => localStorage.clear());

  it('rótulos visíveis e autocomplete de usuário e senha', () => {
    const { tela } = montar({});
    const rotulos = Array.from(tela.querySelectorAll('label')).map(r => r.textContent?.trim());
    expect(rotulos).toEqual(['E-mail', 'Senha']);
    expect(tela.querySelector('#login-email')!.getAttribute('autocomplete')).toBe('username');
    expect(tela.querySelector('#login-senha')!.getAttribute('autocomplete')).toBe('current-password');
  });

  it('o olho mostra e oculta a senha', async () => {
    const { fixture, tela } = montar({});
    const senha = tela.querySelector('#login-senha') as HTMLInputElement;
    const olho = tela.querySelector('.campo__olho') as HTMLButtonElement;
    expect(senha.type).toBe('password');
    expect(olho.getAttribute('aria-label')).toBe('Mostrar senha');

    olho.click();
    await fixture.whenStable();
    expect(senha.type).toBe('text');
    expect(olho.getAttribute('aria-label')).toBe('Ocultar senha');
  });

  it('avisa quando o Caps Lock está ligado', async () => {
    const { fixture, tela } = montar({});
    const senha = tela.querySelector('#login-senha') as HTMLInputElement;
    const tecla = new KeyboardEvent('keyup', { key: 'A' });
    Object.defineProperty(tecla, 'getModifierState', { value: (nome: string) => nome === 'CapsLock' });
    senha.dispatchEvent(tecla);
    await fixture.whenStable();
    expect(tela.textContent).toContain('Caps Lock ativado');

    senha.dispatchEvent(new Event('blur'));
    await fixture.whenStable();
    expect(tela.textContent).not.toContain('Caps Lock ativado');
  });

  it('sessão terminada mostra o aviso acima do botão', () => {
    const { tela } = montar({ motivo: 'expirou' });
    const aviso = tela.querySelector('.alert-info') as HTMLElement;
    expect(aviso.getAttribute('role')).toBe('alert');
    expect(aviso.textContent).toContain('Sua sessão terminou');
  });

  it('enquanto envia, o botão fica desabilitado com "Entrando..."; senha errada mostra o erro', async () => {
    const { fixture, tela } = montar({});
    const api = TestBed.inject(HttpTestingController);
    fixture.componentInstance.email.set('fulano@exemplo.com');
    fixture.componentInstance.senha.set('errada');
    fixture.componentInstance.entrar();
    await fixture.whenStable();

    const botao = tela.querySelector('button[type=submit]') as HTMLButtonElement;
    expect(botao.disabled).toBe(true);
    expect(botao.textContent).toContain('Entrando...');

    api.expectOne(`${PREVISIO_CONFIG.apiUrl}/auth/login`).flush('', { status: 401, statusText: 'Unauthorized' });
    await fixture.whenStable();
    expect(botao.disabled).toBe(false);
    expect(tela.querySelector('.alert-danger')!.textContent).toContain('E-mail ou senha inválidos.');
  });
});
