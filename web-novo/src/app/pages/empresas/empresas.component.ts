import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { PrevisioService } from '../../core/previsio.service';
import { Empresa } from '../../core/previsio.models';

@Component({
  selector: 'app-empresas',
  imports: [FormsModule, RouterLink, DatePipe],
  templateUrl: './empresas.component.html'
})
export class EmpresasComponent {
  private readonly previsio = inject(PrevisioService);
  private readonly rota = inject(ActivatedRoute);

  readonly empresas = signal<Empresa[]>([]);
  readonly novoNome = signal('');
  readonly novoCnpj = signal('');
  readonly mensagem = signal('');
  readonly erro = signal('');

  constructor() {
    // Retorno do OAuth do Bling: /pages/empresas?bling=conectado
    const retornoBling = this.rota.snapshot.queryParamMap.get('bling');
    if (retornoBling === 'conectado') {
      this.mensagem.set('Conta Bling conectada. As contas a pagar serão sincronizadas em instantes.');
    } else if (retornoBling === 'expirado') {
      this.erro.set('O link de conexão expirou. Clique em "Conectar Bling" novamente.');
    } else if (retornoBling === 'erro') {
      this.erro.set('Não foi possível conectar ao Bling. Tente novamente.');
    }

    this.carregar();
  }

  carregar(): void {
    this.previsio.listarEmpresas().subscribe({
      next: empresas => this.empresas.set(empresas),
      error: () => this.erro.set('Não foi possível carregar as empresas.')
    });
  }

  adicionar(): void {
    const nome = this.novoNome().trim();
    const cnpj = this.novoCnpj().replace(/\D/g, '');
    if (nome === '' || cnpj.length !== 14) {
      this.erro.set('Informe o nome e um CNPJ com 14 dígitos.');
      return;
    }

    this.erro.set('');
    this.previsio.criarEmpresa(nome, cnpj).subscribe({
      next: () => {
        this.novoNome.set('');
        this.novoCnpj.set('');
        this.mensagem.set('Empresa cadastrada. Agora conecte a conta Bling dela.');
        this.carregar();
      },
      error: resposta => {
        if (typeof resposta.error === 'string') {
          this.erro.set(resposta.error);
        } else {
          this.erro.set('Não foi possível cadastrar a empresa.');
        }
      }
    });
  }

  conectarBling(empresa: Empresa): void {
    this.previsio.obterUrlConexaoBling(empresa.id).subscribe({
      next: resposta => {
        // O cliente faz login no Bling da empresa e autoriza o Previsio
        window.location.href = resposta.url;
      },
      error: () => this.erro.set('Não foi possível gerar o link de conexão.')
    });
  }

  sincronizarAgora(): void {
    this.previsio.executarJob('sincronizacao').subscribe({
      next: () => this.mensagem.set('Sincronização iniciada.'),
      error: () => this.erro.set('Não foi possível iniciar a sincronização.')
    });
  }
}
