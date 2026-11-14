using Microsoft.EntityFrameworkCore;
using Previsio.Core.Entidades;

namespace Previsio.Data;

public class PrevisioDbContext : DbContext
{
    public PrevisioDbContext(DbContextOptions<PrevisioDbContext> options) : base(options)
    {
    }

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<NotaFiscal> NotasFiscais => Set<NotaFiscal>();
    public DbSet<ParcelaNota> ParcelasNota => Set<ParcelaNota>();
    public DbSet<ContaPagarSincronizada> ContasPagar => Set<ContaPagarSincronizada>();
    public DbSet<ContaReceberSincronizada> ContasReceber => Set<ContaReceberSincronizada>();
    public DbSet<CategoriaFinanceira> Categorias => Set<CategoriaFinanceira>();
    public DbSet<RegraCategoria> RegrasCategoria => Set<RegraCategoria>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<FalhaProcessamento> Falhas => Set<FalhaProcessamento>();
    public DbSet<Aviso> Avisos => Set<Aviso>();
    public DbSet<NotaSoPdf> NotasSoPdf => Set<NotaSoPdf>();
    public DbSet<FornecedorBling> FornecedoresBling => Set<FornecedorBling>();
    public DbSet<EmailIgnorado> EmailsIgnorados => Set<EmailIgnorado>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cliente>(entidade =>
        {
            entidade.ToTable("clientes");
            entidade.Property(c => c.ConferenciaToleranciaValor).HasPrecision(18, 2);
            entidade.Property(c => c.Nome).HasMaxLength(200).IsRequired();
            entidade.Property(c => c.ImapHost).HasMaxLength(200);
            entidade.Property(c => c.ImapUsuario).HasMaxLength(200);
            entidade.Property(c => c.ImapUltimoErroConexao).HasMaxLength(500);
            entidade.Property(c => c.BlingClientId).HasMaxLength(200);
            entidade.Property(c => c.CategoriaPadrao).HasMaxLength(200);
        });

        modelBuilder.Entity<Empresa>(entidade =>
        {
            entidade.ToTable("empresas");
            entidade.Property(e => e.Nome).HasMaxLength(200).IsRequired();
            entidade.Property(e => e.Cnpj).HasMaxLength(14).IsRequired();
            entidade.Property(e => e.ImapUltimoErroConexao).HasMaxLength(500);
            entidade.Property(e => e.BlingClientId).HasMaxLength(200);
            entidade.Property(e => e.ImapHost).HasMaxLength(200);
            entidade.Property(e => e.ImapUsuario).HasMaxLength(200);
            entidade.HasIndex(e => new { e.ClienteId, e.Cnpj }).IsUnique();
            entidade.Ignore(e => e.BlingConectado);
            entidade.Ignore(e => e.TemEmailProprio);
            entidade.Ignore(e => e.TemCertificado);
            entidade.Property(e => e.CertificadoTitular).HasMaxLength(300);
            entidade.Property(e => e.Uf).HasMaxLength(2);
            entidade.Property(e => e.SefazBuscaAtiva).HasDefaultValue(true);
            entidade.Property(e => e.SefazUltimoNsu).HasMaxLength(15);
            entidade.Property(e => e.SefazSituacao).HasMaxLength(500);

            entidade.HasOne(e => e.Cliente)
                .WithMany(c => c.Empresas)
                .HasForeignKey(e => e.ClienteId);
        });

        modelBuilder.Entity<NotaSoPdf>(entidade =>
        {
            entidade.ToTable("notas_so_pdf");
            entidade.Property(n => n.MensagemId).HasMaxLength(300);
            entidade.Property(n => n.Remetente).HasMaxLength(200);
            entidade.Property(n => n.NomeRemetente).HasMaxLength(200);
            entidade.Property(n => n.Assunto).HasMaxLength(300);
            entidade.Property(n => n.OrigemEmail).HasMaxLength(300);
            entidade.Property(n => n.PdfNome).HasMaxLength(200);
            entidade.Property(n => n.ChaveAcesso).HasMaxLength(44);
            entidade.Property(n => n.OrigemChave).HasMaxLength(30);
            entidade.Property(n => n.Motivo).HasMaxLength(500);
            entidade.Property(n => n.ResolvidaPor).HasMaxLength(200);
            entidade.Property(n => n.Observacao).HasMaxLength(500);
            entidade.HasIndex(n => new { n.ClienteId, n.Situacao });
            entidade.HasIndex(n => new { n.ClienteId, n.MensagemId });

            entidade.HasOne(n => n.Cliente).WithMany().HasForeignKey(n => n.ClienteId);
            entidade.HasOne(n => n.EmpresaCaixa).WithMany().HasForeignKey(n => n.EmpresaCaixaId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<NotaFiscal>(entidade =>
        {
            entidade.ToTable("notas_fiscais");
            entidade.Property(n => n.ChaveAcesso).HasMaxLength(44).IsRequired();
            entidade.Property(n => n.Numero).HasMaxLength(20);
            entidade.Property(n => n.Serie).HasMaxLength(5);
            entidade.Property(n => n.CnpjDestinatario).HasMaxLength(14);
            entidade.Property(n => n.CnpjFornecedor).HasMaxLength(14);
            entidade.Property(n => n.NomeFornecedor).HasMaxLength(200);
            entidade.Property(n => n.OrigemEmail).HasMaxLength(300);
            entidade.Property(n => n.ValorTotal).HasPrecision(14, 2);
            entidade.Property(n => n.DataEmissao).HasColumnType("date");
            entidade.Property(n => n.Status).HasConversion<int>();
            entidade.Property(n => n.ResolvidaManualmentePor).HasMaxLength(200);
            entidade.Property(n => n.ObservacaoResolucao).HasMaxLength(500);

            // A mesma nota nunca é processada duas vezes para o mesmo cliente
            entidade.HasIndex(n => new { n.ClienteId, n.ChaveAcesso }).IsUnique();
            entidade.HasIndex(n => n.Status);

            entidade.HasOne(n => n.Empresa)
                .WithMany()
                .HasForeignKey(n => n.EmpresaId);

            entidade.HasOne(n => n.EmpresaCaixa)
                .WithMany()
                .HasForeignKey(n => n.EmpresaCaixaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ParcelaNota>(entidade =>
        {
            entidade.ToTable("parcelas_nota");
            entidade.Property(p => p.NumeroParcela).HasMaxLength(20);
            entidade.Property(p => p.Valor).HasPrecision(14, 2);
            entidade.Property(p => p.Vencimento).HasColumnType("date");

            entidade.HasOne(p => p.NotaFiscal)
                .WithMany(n => n.Parcelas)
                .HasForeignKey(p => p.NotaFiscalId);
        });

        modelBuilder.Entity<ContaPagarSincronizada>(entidade =>
        {
            entidade.ToTable("contas_pagar");
            entidade.Property(c => c.Valor).HasPrecision(14, 2);
            entidade.Property(c => c.Vencimento).HasColumnType("date");
            entidade.HasIndex(c => new { c.EmpresaId, c.BlingId }).IsUnique();
            entidade.HasIndex(c => c.Vencimento);
            entidade.Property(c => c.FornecedorNome).HasMaxLength(200);

            entidade.HasOne(c => c.Empresa)
                .WithMany()
                .HasForeignKey(c => c.EmpresaId);
        });

        modelBuilder.Entity<CategoriaFinanceira>(entidade =>
        {
            entidade.ToTable("categorias_financeiras");
            entidade.Property(c => c.Descricao).HasMaxLength(200);
            entidade.HasIndex(c => new { c.EmpresaId, c.BlingId }).IsUnique();

            entidade.HasOne(c => c.Empresa)
                .WithMany()
                .HasForeignKey(c => c.EmpresaId);
        });

        modelBuilder.Entity<RegraCategoria>(entidade =>
        {
            entidade.ToTable("regras_categoria");
            entidade.Property(r => r.CnpjFornecedor).HasMaxLength(14);
            entidade.Property(r => r.Categoria).HasMaxLength(200);
            entidade.HasIndex(r => new { r.ClienteId, r.CnpjFornecedor }).IsUnique();

            entidade.HasOne(r => r.Cliente)
                .WithMany()
                .HasForeignKey(r => r.ClienteId);
        });

        modelBuilder.Entity<ContaReceberSincronizada>(entidade =>
        {
            entidade.ToTable("contas_receber");
            entidade.Property(c => c.Valor).HasPrecision(14, 2);
            entidade.Property(c => c.Vencimento).HasColumnType("date");
            entidade.HasIndex(c => new { c.EmpresaId, c.BlingId }).IsUnique();
            entidade.HasIndex(c => c.Vencimento);
            entidade.Property(c => c.ClienteNome).HasMaxLength(200);

            entidade.HasOne(c => c.Empresa)
                .WithMany()
                .HasForeignKey(c => c.EmpresaId);
        });

        modelBuilder.Entity<EmailIgnorado>(entidade =>
        {
            entidade.ToTable("emails_ignorados");
            entidade.Property(e => e.MensagemId).HasMaxLength(300);
            entidade.Property(e => e.Remetente).HasMaxLength(200);
            entidade.Property(e => e.NomeRemetente).HasMaxLength(200);
            entidade.Property(e => e.Assunto).HasMaxLength(300);
            entidade.Property(e => e.Corpo).HasMaxLength(4000);
            entidade.Property(e => e.Anexos).HasMaxLength(500);
            entidade.Property(e => e.Motivo).HasMaxLength(300);
            entidade.Property(e => e.PdfNome).HasMaxLength(200);
            entidade.Property(e => e.TratadoPor).HasMaxLength(200);
            entidade.Property(e => e.ResultadoTratamento).HasMaxLength(300);
            entidade.Property(e => e.NumeroNota).HasMaxLength(20);
            entidade.HasIndex(e => new { e.ClienteId, e.LidoEm });
            entidade.HasIndex(e => new { e.ClienteId, e.MensagemId });

            entidade.HasOne(e => e.Cliente)
                .WithMany()
                .HasForeignKey(e => e.ClienteId);
            entidade.HasOne(e => e.EmpresaCaixa)
                .WithMany()
                .HasForeignKey(e => e.EmpresaCaixaId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<FornecedorBling>(entidade =>
        {
            entidade.ToTable("fornecedores_bling");
            entidade.Property(f => f.Nome).HasMaxLength(200).IsRequired();
            entidade.Property(f => f.Documento).HasMaxLength(20);
            entidade.HasIndex(f => new { f.EmpresaId, f.BlingContatoId }).IsUnique();

            entidade.HasOne(f => f.Empresa)
                .WithMany()
                .HasForeignKey(f => f.EmpresaId);
        });

        modelBuilder.Entity<Usuario>(entidade =>
        {
            entidade.ToTable("usuarios");
            entidade.Property(u => u.Nome).HasMaxLength(200).IsRequired();
            entidade.Property(u => u.Email).HasMaxLength(200).IsRequired();
            entidade.Property(u => u.SenhaHash).HasMaxLength(500).IsRequired();
            entidade.Property(u => u.CarimboSeguranca).HasMaxLength(64).IsRequired();
            entidade.Property(u => u.Perfil).HasConversion<int>();
            entidade.Property(u => u.RedefinicaoSenhaHash).HasMaxLength(64);

            // O e-mail é o login: gravado sempre em minúsculas
            entidade.HasIndex(u => u.Email).IsUnique();
            entidade.HasIndex(u => u.RedefinicaoSenhaHash);

            entidade.HasOne(u => u.Cliente)
                .WithMany()
                .HasForeignKey(u => u.ClienteId);
        });

        modelBuilder.Entity<FalhaProcessamento>(entidade =>
        {
            entidade.ToTable("falhas_processamento");
            entidade.Property(f => f.Etapa).HasConversion<int>();
            entidade.Property(f => f.Mensagem).HasMaxLength(2000).IsRequired();
            entidade.Property(f => f.Metodo).HasMaxLength(10).IsRequired();
            entidade.Property(f => f.Endereco).HasMaxLength(500).IsRequired();
            entidade.Property(f => f.Resposta).HasMaxLength(4000);
            entidade.Property(f => f.NumeroNota).HasMaxLength(20);
            entidade.Property(f => f.ChaveAcesso).HasMaxLength(44);
            entidade.Property(f => f.Fornecedor).HasMaxLength(200);
            entidade.Property(f => f.CnpjFornecedor).HasMaxLength(14);
            entidade.Property(f => f.ValorNota).HasPrecision(14, 2);
            entidade.Property(f => f.NumeroParcela).HasMaxLength(20);
            entidade.Property(f => f.ValorParcela).HasPrecision(14, 2);
            entidade.Property(f => f.VencimentoParcela).HasColumnType("date");
            entidade.Property(f => f.Origem).HasMaxLength(300);

            // A tela lista as falhas do cliente da mais recente para a mais antiga
            entidade.HasIndex(f => new { f.ClienteId, f.OcorridaEm });
            entidade.HasIndex(f => f.NotaFiscalId);
            entidade.HasIndex(f => new { f.ClienteId, f.UidEmail });

            entidade.HasOne<Cliente>()
                .WithMany()
                .HasForeignKey(f => f.ClienteId)
                .OnDelete(DeleteBehavior.Cascade);

            entidade.HasOne(f => f.Empresa)
                .WithMany()
                .HasForeignKey(f => f.EmpresaId)
                .OnDelete(DeleteBehavior.SetNull);

            entidade.HasOne<NotaFiscal>()
                .WithMany()
                .HasForeignKey(f => f.NotaFiscalId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Aviso>(entidade =>
        {
            entidade.ToTable("avisos");
            entidade.Property(a => a.Tipo).HasConversion<int>();
            entidade.Property(a => a.Chave).HasMaxLength(100).IsRequired();
            entidade.Property(a => a.Titulo).HasMaxLength(200).IsRequired();
            entidade.Property(a => a.Mensagem).HasMaxLength(500);
            entidade.Property(a => a.Link).HasMaxLength(200);

            // Um aviso aberto por problema: o mesmo problema só gera aviso novo depois de resolvido
            entidade.HasIndex(a => new { a.ClienteId, a.Chave })
                .IsUnique()
                .HasFilter("\"ResolvidoEm\" IS NULL");

            entidade.HasOne(a => a.Cliente)
                .WithMany()
                .HasForeignKey(a => a.ClienteId);
        });
    }
}
