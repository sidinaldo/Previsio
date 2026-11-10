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
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<FalhaProcessamento> Falhas => Set<FalhaProcessamento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cliente>(entidade =>
        {
            entidade.ToTable("clientes");
            entidade.Property(c => c.Nome).HasMaxLength(200).IsRequired();
            entidade.Property(c => c.ImapHost).HasMaxLength(200);
            entidade.Property(c => c.ImapUsuario).HasMaxLength(200);
        });

        modelBuilder.Entity<Empresa>(entidade =>
        {
            entidade.ToTable("empresas");
            entidade.Property(e => e.Nome).HasMaxLength(200).IsRequired();
            entidade.Property(e => e.Cnpj).HasMaxLength(14).IsRequired();
            entidade.Property(e => e.BlingClientId).HasMaxLength(200);
            entidade.HasIndex(e => new { e.ClienteId, e.Cnpj }).IsUnique();
            entidade.Ignore(e => e.BlingConectado);

            entidade.HasOne(e => e.Cliente)
                .WithMany(c => c.Empresas)
                .HasForeignKey(e => e.ClienteId);
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

            // A mesma nota nunca é processada duas vezes para o mesmo cliente
            entidade.HasIndex(n => new { n.ClienteId, n.ChaveAcesso }).IsUnique();
            entidade.HasIndex(n => n.Status);

            entidade.HasOne(n => n.Empresa)
                .WithMany()
                .HasForeignKey(n => n.EmpresaId);
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

            entidade.HasOne(c => c.Empresa)
                .WithMany()
                .HasForeignKey(c => c.EmpresaId);
        });

        modelBuilder.Entity<Usuario>(entidade =>
        {
            entidade.ToTable("usuarios");
            entidade.Property(u => u.Nome).HasMaxLength(200).IsRequired();
            entidade.Property(u => u.Email).HasMaxLength(200).IsRequired();
            entidade.Property(u => u.SenhaHash).HasMaxLength(500).IsRequired();
            entidade.Property(u => u.CarimboSeguranca).HasMaxLength(64).IsRequired();
            entidade.Property(u => u.Perfil).HasConversion<int>();

            // O e-mail é o login: gravado sempre em minúsculas
            entidade.HasIndex(u => u.Email).IsUnique();

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
    }
}
