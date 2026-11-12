import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { DatePipe } from '@angular/common';

import { mensagemDaApi } from '../../core/mensagem-erro';
import { Empresa, UFS } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { CarregandoDirective } from '../../shared/carregando.directive';

/**
 * Certificado digital A1 da empresa (arquivo .pfx, senha e UF), usado para buscar na SEFAZ o XML
 * das notas que chegam só em PDF. Envia na hora, separado do "Salvar alterações" da empresa.
 * Sem campos de formulário Angular: fica dentro do formulário da empresa sem entrar nele.
 */
@Component({
  selector: 'app-certificado-empresa',
  imports: [DatePipe, CarregandoDirective],
  template: `
    @if (empresa().certificadoCadastrado) {
      <p class="pv-certificado__atual">
        <i class="material-icons" aria-hidden="true">verified_user</i>
        <span>
          <strong>{{ empresa().certificadoTitular }}</strong>
          <span class="pv-tabela__auxiliar">
            Válido até {{ empresa().certificadoValidoAte | date:'dd/MM/yyyy' }} · UF {{ empresa().uf }}
          </span>
        </span>
        <button type="button" class="btn btn-link btn-sm" [pvCarregando]="acao() === 'remover'" [disabled]="acao() !== null"
                (click)="remover()">Remover</button>
      </p>
    }
    <div class="pv-formulario__linha">
      <div class="pv-certificado__campo">
        <label class="pv-rotulo" [for]="'certificado-arquivo-' + empresa().id">
          @if (empresa().certificadoCadastrado) { Novo arquivo .pfx (substitui o atual) } @else { Arquivo .pfx }
        </label>
        <input [id]="'certificado-arquivo-' + empresa().id" type="file" accept=".pfx,.p12,application/x-pkcs12"
               (change)="escolher($event)">
      </div>
      <div class="pv-certificado__campo">
        <label class="pv-rotulo" [for]="'certificado-senha-' + empresa().id">Senha do certificado</label>
        <input [id]="'certificado-senha-' + empresa().id" type="password" class="pv-campo pv-campo--texto" autocomplete="new-password"
               [value]="senha()" (input)="mudarSenha($event)">
      </div>
      <div class="pv-certificado__campo">
        <label class="pv-rotulo" [for]="'certificado-uf-' + empresa().id">UF da empresa</label>
        <span class="pv-campo-grupo bmd-form-group">
          <select [id]="'certificado-uf-' + empresa().id" class="pv-campo pv-certificado__uf" (change)="mudarUf($event)">
            <option value="" [selected]="uf() === ''">Escolha</option>
            @for (sigla of ufs; track sigla) {
              <option [value]="sigla" [selected]="uf() === sigla">{{ sigla }}</option>
            }
          </select>
        </span>
      </div>
      <button type="button" class="btn btn-secundario pv-certificado__enviar" [pvCarregando]="acao() === 'enviar'"
              [disabled]="acao() !== null || !arquivo()" (click)="enviar()">
        <i class="material-icons" aria-hidden="true">upload</i> Enviar certificado
      </button>
    </div>
    @if (mensagem()) {
      <p class="pv-certificado__ok" role="status">{{ mensagem() }}</p>
    }
    @if (erro()) {
      <p class="pv-certificado__erro" role="alert">{{ erro() }}</p>
    }
  `
})
export class CertificadoEmpresaComponent implements OnInit {
  private readonly api = inject(PrevisioService);

  readonly empresa = input.required<Empresa>();
  readonly alterada = output<Empresa>();

  readonly ufs = UFS;
  readonly arquivo = signal<File | null>(null);
  readonly senha = signal('');
  readonly uf = signal('');
  readonly acao = signal<'enviar' | 'remover' | null>(null);
  readonly mensagem = signal('');
  readonly erro = signal('');

  ngOnInit(): void {
    this.uf.set(this.empresa().uf || '');
  }

  escolher(evento: Event): void {
    const campo = evento.target as HTMLInputElement;
    if (campo.files && campo.files.length > 0) {
      this.arquivo.set(campo.files[0]);
    } else {
      this.arquivo.set(null);
    }
  }

  mudarSenha(evento: Event): void {
    this.senha.set((evento.target as HTMLInputElement).value);
  }

  mudarUf(evento: Event): void {
    this.uf.set((evento.target as HTMLSelectElement).value);
  }

  enviar(): void {
    const arquivo = this.arquivo();
    if (!arquivo) {
      return;
    }
    if (this.uf() === '') {
      this.erro.set('Escolha a UF da empresa.');
      return;
    }
    this.mensagem.set('');
    this.erro.set('');
    this.acao.set('enviar');
    this.api.salvarCertificado(this.empresa().id, arquivo, this.senha(), this.uf()).subscribe({
      next: empresa => {
        this.acao.set(null);
        this.senha.set('');
        this.mensagem.set('Certificado cadastrado. Das notas que chegarem só em PDF, o XML será buscado na SEFAZ.');
        this.alterada.emit(empresa);
      },
      error: erro => {
        this.acao.set(null);
        this.erro.set(mensagemDaApi(erro, 'Não foi possível cadastrar o certificado.'));
      }
    });
  }

  remover(): void {
    this.mensagem.set('');
    this.erro.set('');
    this.acao.set('remover');
    this.api.removerCertificado(this.empresa().id).subscribe({
      next: empresa => {
        this.acao.set(null);
        this.mensagem.set('Certificado removido.');
        this.alterada.emit(empresa);
      },
      error: erro => {
        this.acao.set(null);
        this.erro.set(mensagemDaApi(erro, 'Não foi possível remover o certificado.'));
      }
    });
  }
}
