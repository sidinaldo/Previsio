import { Injectable, computed, effect, inject, signal } from '@angular/core';

import { ClientesService } from './clientes.service';
import { SessaoService } from './sessao.service';

/** Sem rede aberta (ou antes de a lista de clientes chegar): o nome genérico. */
export const NOME_ERP_GENERICO = 'ERP';

/** Marca, nos textos fixos (situações, etapas, opções de filtro), onde entra o nome do ERP. */
export const MARCA_ERP = '{erp}';

// Cópia do nome para os textos fixos, que não passam por injeção: o ErpService escreve
const nomeDoErp = signal(NOME_ERP_GENERICO);

/**
 * Troca a marca {erp} pelo nome do ERP da rede. Chamada na hora de mostrar (template ou método
 * usado nele), para o texto acompanhar a troca de rede.
 */
export function comErp(texto: string): string {
  return texto.split(MARCA_ERP).join(nomeDoErp());
}

/** Troca o nome que os textos fixos usam. Só o ErpService (e o ErpService dos testes) chama. */
export function definirNomeErp(nome: string): void {
  nomeDoErp.set(nome);
}

/**
 * Nome do ERP da rede aberta no painel ("lançada no Bling", "Conectar Bling").
 * Quem decide é a API: o usuário da rede recebe o nome no login (usuario.erpNome) e o admin,
 * na lista de clientes (cliente.erpNome). Nenhuma chamada a mais.
 */
@Injectable({ providedIn: 'root' })
export class ErpService {
  private readonly sessao = inject(SessaoService);
  private readonly clientes = inject(ClientesService);

  readonly nome = computed(() => {
    const usuario = this.sessao.usuario();
    if (!usuario) {
      return NOME_ERP_GENERICO;
    }
    if (this.sessao.ehAdmin()) {
      const clienteId = this.sessao.clienteId();
      const cliente = this.clientes.lista().find(item => item.id === clienteId);
      if (cliente && cliente.erpNome) {
        return cliente.erpNome;
      }
      return NOME_ERP_GENERICO;
    }
    if (usuario.erpNome) {
      return usuario.erpNome;
    }
    return NOME_ERP_GENERICO;
  });

  constructor() {
    effect(() => definirNomeErp(this.nome()));
  }
}
