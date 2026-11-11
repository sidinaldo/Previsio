import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { Cliente } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { ContratoClienteComponent } from './contrato-cliente.component';

const EM_TESTE: Cliente = {
  id: 7, nome: 'Rede', imapHost: '', imapPorta: 993, imapSsl: true, imapUsuario: '', blingClientId: null,
  blingClientSecretCadastrado: false, ativo: true, contrato: 'EmTeste', diasRestantesTeste: 3, testeUltimoDia: '2026-11-13T00:00:00'
};

describe('ContratoClienteComponent', () => {
  it('contrata e prorroga avisando quem usa', async () => {
    const contratado: Cliente = { ...EM_TESTE, contrato: 'Contratado', diasRestantesTeste: null, testeUltimoDia: null };
    const prorrogado: Cliente = { ...EM_TESTE, diasRestantesTeste: 10, testeUltimoDia: '2026-11-20T00:00:00' };
    const api = { contratarCliente: vi.fn(() => of(contratado)), prorrogarTeste: vi.fn(() => of(prorrogado)) };
    TestBed.configureTestingModule({ imports: [ContratoClienteComponent], providers: [{ provide: PrevisioService, useValue: api }] });
    const fixture = TestBed.createComponent(ContratoClienteComponent);
    fixture.componentRef.setInput('cliente', EM_TESTE);
    const alterado = vi.fn();
    fixture.componentInstance.alterado.subscribe(alterado);
    fixture.autoDetectChanges();
    await fixture.whenStable();
    const tela = fixture.nativeElement as HTMLElement;
    expect(tela.textContent).toContain('Teste grátis até 13/11/2026');
    expect(tela.querySelector('.badge')?.textContent).toContain('Teste · faltam 3 dias');

    const botoes = tela.querySelectorAll('.pv-formulario__acoes button');
    (botoes[1] as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(api.prorrogarTeste).toHaveBeenCalledWith(7, 7);
    expect(tela.querySelector('.alert-success')?.textContent).toContain('vai até 20/11/2026');

    (botoes[0] as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(api.contratarCliente).toHaveBeenCalledWith(7);
    expect(alterado).toHaveBeenLastCalledWith(contratado);
  });
});
