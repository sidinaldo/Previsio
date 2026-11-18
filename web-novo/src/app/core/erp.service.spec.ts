import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';

import { ClientesService } from './clientes.service';
import { ErpService, NOME_ERP_GENERICO, comErp } from './erp.service';
import { Cliente, Usuario } from './previsio.models';
import { SessaoService } from './sessao.service';

function montar(usuario: Usuario | null, admin: boolean, clienteId: number | null, lista: Partial<Cliente>[]) {
  TestBed.configureTestingModule({
    providers: [
      { provide: SessaoService, useValue: { usuario: signal(usuario), ehAdmin: signal(admin), clienteId: signal(clienteId) } },
      { provide: ClientesService, useValue: { lista: signal(lista) } }
    ]
  });
  return TestBed.inject(ErpService);
}

const usuarioDaRede: Usuario = {
  id: 1, clienteId: 7, nome: 'Ana', email: 'ana@rede.com', perfil: 'Cliente', ativo: true, ultimoAcessoEm: null, erpNome: 'Bling'
};

describe('ErpService', () => {
  it('usuário da rede: o nome que veio no login, sem chamar a API', () => {
    const erp = montar(usuarioDaRede, false, 7, []);
    expect(erp.nome()).toBe('Bling');
  });

  it('admin: o nome do cliente escolhido na lista', () => {
    const admin = { ...usuarioDaRede, clienteId: null, perfil: 'Admin', erpNome: null };
    const erp = montar(admin, true, 8, [{ id: 7, erpNome: 'Outro' }, { id: 8, erpNome: 'Bling' }]);
    expect(erp.nome()).toBe('Bling');
  });

  it('sem login, o nome genérico; os textos fixos acompanham', () => {
    const erp = montar(null, false, null, []);
    expect(erp.nome()).toBe(NOME_ERP_GENERICO);
    TestBed.tick();
    expect(comErp('Lançada no {erp}')).toBe('Lançada no ERP');
  });
});
