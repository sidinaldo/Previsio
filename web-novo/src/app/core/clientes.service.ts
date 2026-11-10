import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';

import { Cliente, NovoCliente } from './previsio.models';
import { PrevisioService } from './previsio.service';
import { SessaoService } from './sessao.service';

/**
 * Lista de clientes do admin, usada pela escolha na barra do topo e pela tela Clientes.
 * Fica num serviço para o cliente recém-cadastrado aparecer na hora nos dois lugares.
 */
@Injectable({ providedIn: 'root' })
export class ClientesService {
  private readonly previsio = inject(PrevisioService);
  private readonly sessao = inject(SessaoService);

  readonly lista = signal<Cliente[]>([]);
  readonly carregada = signal(false);
  readonly erro = signal('');

  carregar(): void {
    this.previsio.listarClientes().subscribe({
      next: lista => {
        this.lista.set(lista);
        this.erro.set('');
        this.garantirClienteEscolhido();
        this.carregada.set(true);
      },
      error: () => {
        this.erro.set('Não foi possível carregar a lista de clientes. Verifique se a API está no ar.');
        this.carregada.set(true);
      }
    });
  }

  /** Cadastra o cliente e já passa a exibi-lo. */
  criar(dados: NovoCliente): Observable<Cliente> {
    return this.previsio.criarCliente(dados).pipe(
      tap(cliente => {
        const lista = [...this.lista(), cliente];
        lista.sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR'));
        this.lista.set(lista);
        this.sessao.escolherCliente(cliente.id);
      })
    );
  }

  /** Troca na lista o cliente alterado (ex.: nome novo aparece na barra do topo). */
  atualizar(cliente: Cliente): void {
    const lista = this.lista().map(item => {
      if (item.id === cliente.id) {
        return cliente;
      }
      return item;
    });
    lista.sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR'));
    this.lista.set(lista);
  }

  // Sem escolha anterior (ou com um cliente que não existe mais): abre o primeiro
  private garantirClienteEscolhido(): void {
    const lista = this.lista();
    const escolhido = this.sessao.clienteId();
    const existe = lista.some(cliente => cliente.id === escolhido);
    if (!existe && lista.length > 0) {
      this.sessao.escolherCliente(lista[0].id);
    }
  }
}
