import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { formatarCnpj } from '../../core/implantacao';
import { mensagemDaApi } from '../../core/mensagem-erro';
import { Cliente, DESCRICAO_ORIGEM_APLICATIVO, Empresa, NovaEmpresa, ResultadoTesteEmail } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { CarregandoDirective } from '../../shared/carregando.directive';
import { TAMANHO_PAGINA_PADRAO, paginar } from '../../core/paginacao';
import { PaginacaoComponent } from '../../shared/paginacao/paginacao.component';

/**
 * Etapa 2: as empresas (CNPJs) da rede. Cada uma pode ter o próprio aplicativo do Bling
 * e a própria caixa de e-mail; sem eles, usa os da rede (mesmas regras da tela Empresas).
 */
@Component({
  selector: 'app-etapa-empresas',
  imports: [FormsModule, CarregandoDirective, PaginacaoComponent],
  templateUrl: './etapa-empresas.component.html'
})
export class EtapaEmpresasComponent {
  private readonly previsio = inject(PrevisioService);

  readonly cliente = input.required<Cliente>();
  readonly empresas = input.required<Empresa[]>();
  readonly alterou = output<void>();
  readonly continuar = output<void>();

  readonly origens = DESCRICAO_ORIGEM_APLICATIVO;
  readonly formatarCnpj = formatarCnpj;

  readonly nome = signal('');
  readonly cnpj = signal('');
  readonly clientId = signal('');
  readonly clientSecret = signal('');
  readonly imapHost = signal('');
  readonly imapPorta = signal<number | null>(993);
  readonly imapSsl = signal(true);
  readonly imapUsuario = signal('');
  readonly imapSenha = signal('');

  readonly cadastrando = signal(false);
  readonly testando = signal(false);
  readonly erro = signal('');
  readonly mensagem = signal('');
  readonly teste = signal<ResultadoTesteEmail | null>(null);

  readonly ativas = computed(() => this.empresas().filter(empresa => empresa.ativo));

  // Paginação da tabela (a lista inteira vem da API)
  readonly pagina = signal(1);
  readonly tamanhoPagina = signal(TAMANHO_PAGINA_PADRAO);
  readonly empresasDaPagina = computed(() => paginar(this.empresas(), this.pagina(), this.tamanhoPagina()));

  cadastrar(): void {
    const nome = this.nome().trim();
    const cnpj = this.cnpj().replace(/\D/g, '');
    if (nome === '' || cnpj.length !== 14) {
      this.erro.set('Informe o nome e um CNPJ com 14 dígitos.');
      return;
    }

    const clientId = this.clientId().trim();
    const clientSecret = this.clientSecret().trim();
    if ((clientId === '') !== (clientSecret === '')) {
      this.erro.set('Informe o Client ID e o Client Secret do aplicativo do Bling, ou deixe os dois vazios.');
      return;
    }
    if (!this.emailProprioValido()) {
      return;
    }

    const dados: NovaEmpresa = { nome: nome, cnpj: cnpj };
    if (clientId !== '') {
      dados.blingClientId = clientId;
      dados.blingClientSecret = clientSecret;
    }
    if (this.imapHost().trim() !== '') {
      dados.imapHost = this.imapHost().trim();
      dados.imapPorta = Number(this.imapPorta());
      dados.imapSsl = this.imapSsl();
      dados.imapUsuario = this.imapUsuario().trim();
      dados.imapSenha = this.imapSenha();
    }

    this.erro.set('');
    this.mensagem.set('');
    this.cadastrando.set(true);
    this.previsio.criarEmpresaDoCliente(this.cliente().id, dados).subscribe({
      next: empresa => {
        this.cadastrando.set(false);
        this.limpar();
        this.mensagem.set(empresa.nome + ' cadastrada. Cadastre a próxima loja ou continue.');
        this.alterou.emit();
      },
      error: resposta => {
        this.cadastrando.set(false);
        this.erro.set(mensagemDaApi(resposta, 'Não foi possível cadastrar a empresa.'));
      }
    });
  }

  /** Testa a caixa própria da empresa (opcional) com os dados digitados. */
  testarEmailProprio(): void {
    if (this.imapHost().trim() === '') {
      this.erro.set('Informe o servidor da caixa própria para testar.');
      return;
    }
    if (!this.emailProprioValido()) {
      return;
    }
    this.erro.set('');
    this.teste.set(null);
    this.testando.set(true);
    this.previsio.testarEmail({
      imapHost: this.imapHost().trim(),
      imapPorta: Number(this.imapPorta()),
      imapSsl: this.imapSsl(),
      imapUsuario: this.imapUsuario().trim(),
      imapSenha: this.imapSenha()
    }).subscribe({
      next: resultado => {
        this.testando.set(false);
        this.teste.set(resultado);
      },
      error: resposta => {
        this.testando.set(false);
        this.erro.set(mensagemDaApi(resposta, 'Não foi possível testar a caixa de e-mail.'));
      }
    });
  }

  // Caixa própria é opcional; preenchido o servidor, o resto passa a ser obrigatório
  private emailProprioValido(): boolean {
    if (this.imapHost().trim() === '') {
      return true;
    }
    const porta = Number(this.imapPorta());
    if (this.imapUsuario().trim() === '' || this.imapSenha().trim() === '') {
      this.erro.set('Informe o e-mail e a senha de app da caixa própria da empresa (ou deixe o servidor vazio).');
      return false;
    }
    if (!Number.isInteger(porta) || porta < 1 || porta > 65535) {
      this.erro.set('Informe uma porta entre 1 e 65535 (no Gmail, 993).');
      return false;
    }
    return true;
  }

  private limpar(): void {
    this.nome.set('');
    this.cnpj.set('');
    this.clientId.set('');
    this.clientSecret.set('');
    this.imapHost.set('');
    this.imapPorta.set(993);
    this.imapSsl.set(true);
    this.imapUsuario.set('');
    this.imapSenha.set('');
    this.teste.set(null);
  }
}
