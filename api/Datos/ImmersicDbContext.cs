using Immersic.Api.Modelos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Immersic.Api.Datos;

public class ImmersicDbContext : DbContext
{
    public ImmersicDbContext(DbContextOptions<ImmersicDbContext> opciones)
        : base(opciones)
    {
    }

    public DbSet<Libro> Libros => Set<Libro>();

    public DbSet<Capitulo> Capitulos => Set<Capitulo>();

    public DbSet<Escena> Escenas => Set<Escena>();

    public DbSet<Etiqueta> Etiquetas => Set<Etiqueta>();

    public DbSet<Pista> Pistas => Set<Pista>();

    public DbSet<EscenaEtiqueta> EscenaEtiquetas => Set<EscenaEtiqueta>();

    public DbSet<PistaEtiqueta> PistaEtiquetas => Set<PistaEtiqueta>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var fechaUtc = new ValueConverter<DateTime, DateTime>(
            valor => valor,
            valor => DateTime.SpecifyKind(valor, DateTimeKind.Utc));

        modelBuilder.Entity<Libro>()
            .Property(libro => libro.CreadoEn)
            .HasConversion(fechaUtc);

        modelBuilder.Entity<Capitulo>()
            .Property(capitulo => capitulo.CreadoEn)
            .HasConversion(fechaUtc);

        modelBuilder.Entity<Capitulo>()
            .HasOne(capitulo => capitulo.Libro)
            .WithMany(libro => libro.Capitulos)
            .HasForeignKey(capitulo => capitulo.LibroId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Escena>()
            .HasOne(escena => escena.Capitulo)
            .WithMany(capitulo => capitulo.Escenas)
            .HasForeignKey(escena => escena.CapituloId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<EscenaEtiqueta>()
            .HasKey(relacion => new { relacion.EscenaId, relacion.EtiquetaId });

        modelBuilder.Entity<EscenaEtiqueta>()
            .HasOne(relacion => relacion.Escena)
            .WithMany(escena => escena.Etiquetas)
            .HasForeignKey(relacion => relacion.EscenaId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<EscenaEtiqueta>()
            .HasOne(relacion => relacion.Etiqueta)
            .WithMany()
            .HasForeignKey(relacion => relacion.EtiquetaId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PistaEtiqueta>()
            .HasKey(relacion => new { relacion.PistaId, relacion.EtiquetaId });

        modelBuilder.Entity<PistaEtiqueta>()
            .HasOne(relacion => relacion.Pista)
            .WithMany(pista => pista.Etiquetas)
            .HasForeignKey(relacion => relacion.PistaId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PistaEtiqueta>()
            .HasOne(relacion => relacion.Etiqueta)
            .WithMany()
            .HasForeignKey(relacion => relacion.EtiquetaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
