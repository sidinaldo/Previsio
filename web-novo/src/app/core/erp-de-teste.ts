import { Provider, signal } from '@angular/core';

import { ErpService, definirNomeErp } from './erp.service';

/**
 * Para os testes: o ErpService com um nome fixo, sem sessão nem lista de clientes.
 * Também acerta o nome dos textos fixos (comErp).
 */
export function erpDeTeste(nome = 'Bling'): Provider {
  definirNomeErp(nome);
  return { provide: ErpService, useValue: { nome: signal(nome).asReadonly() } };
}
