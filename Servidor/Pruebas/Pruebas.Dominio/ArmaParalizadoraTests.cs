using Dominio.Comun;
using Dominio.Juego;
using Xunit;

namespace PruebasDominio;

/// <summary>
/// Arma paralizadora (Paquete B; pedido del profesor en clase, sin CA en el enunciado). Reglas en Docs/arena-viva.md:
/// ACCIÓN lejos del reactor dispara; paraliza 5 s a los guardianes a 2 pasos o menos; recarga de 15 s aunque no le
/// pegue a nadie; nunca afecta a jugadores ni puntajes.
/// Mapa (AyudaArenaViva.MapaGuardianes): Ana (0,0) junto al reactor (1,0); Beto (10,0) lejos;
/// guardianes en (0,3) y (10,3), que dan un paso cada 2 s.
/// </summary>
public class ArmaParalizadoraTests
{
    private const string Requisito = "Arma paralizadora";

    /// <summary>Partida en la que el guardián de Beto ya quedó a 2 pasos: (10,3) -> (10,2).</summary>
    private static (Partida partida, Jugador ana, Jugador beto, Guardian guardian) GuardianADosPasosDeBeto(
        ConfigPartida? config = null)
    {
        var (partida, ana, beto) = AyudaArenaViva.Crear(AyudaArenaViva.MapaGuardianes, config: config);
        partida.Tick(2);
        var guardian = partida.Guardianes.Single(g => g.Posicion.X == 10);
        Assert.Equal(new Posicion(10, 2), guardian.Posicion);
        return (partida, ana, beto, guardian);
    }

    [Fact]
    [Trait("Requisito", Requisito)]
    public void Accion_LejosDelReactor_DisparaYParalizaAlGuardianCercano()
    {
        var (partida, _, beto, guardian) = GuardianADosPasosDeBeto();

        var resultado = partida.Ejecutar(beto.Id, Accion.Extraer);

        Assert.Equal(ResultadoAccion.ArmaDisparada, resultado);
        Assert.Equal(5, guardian.SegundosParalizado);
        Assert.False(guardian.Activo);
        var disparo = Assert.Single(partida.TomarNovedades().OfType<ArmaDisparadaNovedad>());
        Assert.Equal(beto.Id, disparo.JugadorId);
        Assert.Equal(new[] { guardian.Id }, disparo.GuardianesIds.ToArray());
        Assert.Equal((5, 15), (disparo.SegundosParalisis, disparo.SegundosRecarga));
    }

    [Fact]
    [Trait("Requisito", Requisito)]
    public void GuardianParalizado_NoSeMueveNiCongelaYDespuesVuelveAPerseguir()
    {
        var (partida, _, beto, guardian) = GuardianADosPasosDeBeto();
        partida.Ejecutar(beto.Id, Accion.Extraer);

        partida.Tick(5);

        Assert.Equal(new Posicion(10, 2), guardian.Posicion);
        Assert.False(beto.Congelado);
        Assert.Equal(0, guardian.SegundosParalizado);

        // Ya recuperado, en el siguiente paso llega junto a Beto y lo congela.
        Assert.True(AyudaArenaViva.AvanzarHasta(partida, () => beto.Congelado, maximo: 4));
    }

    [Fact]
    [Trait("Requisito", Requisito)]
    public void JugadorJuntoAUnGuardianParalizado_NoQuedaCongelado()
    {
        var (partida, _, beto, guardian) = GuardianADosPasosDeBeto();
        partida.Ejecutar(beto.Id, Accion.Extraer);

        // Beto se acerca y queda pegado al guardián paralizado.
        partida.Ejecutar(beto.Id, Accion.Abajo);

        Assert.Equal(1, beto.Posicion.DistanciaManhattan(guardian.Posicion));
        Assert.False(beto.Congelado);
    }

    [Fact]
    [Trait("Requisito", Requisito)]
    public void DisparoVacio_SeGastaIgualYEntraEnRecarga()
    {
        var (partida, _, beto) = AyudaArenaViva.Crear(AyudaArenaViva.MapaGuardianes);   // guardián a 3 pasos

        var resultado = partida.Ejecutar(beto.Id, Accion.Extraer);

        Assert.Equal(ResultadoAccion.ArmaDisparada, resultado);
        Assert.Empty(Assert.Single(partida.TomarNovedades().OfType<ArmaDisparadaNovedad>()).GuardianesIds);
        Assert.All(partida.Guardianes, g => Assert.Equal(0, g.SegundosParalizado));
        Assert.Equal(15, partida.SegundosRecargaArma(beto.Id));
    }

    [Fact]
    [Trait("Requisito", Requisito)]
    public void Recargando_NoDisparaHastaQuePasenQuinceSegundos()
    {
        var (partida, _, beto) = AyudaArenaViva.Crear(AyudaArenaViva.MapaGuardianes,
            config: new ConfigPartida { SegundosCongelamiento = 0 });   // que los guardianes no lo inmovilicen
        partida.Ejecutar(beto.Id, Accion.Extraer);
        partida.TomarNovedades();

        Assert.Equal(ResultadoAccion.ArmaRecargando, partida.Ejecutar(beto.Id, Accion.Extraer));
        Assert.Empty(partida.TomarNovedades().OfType<ArmaDisparadaNovedad>());

        partida.Tick(14);
        Assert.Equal(1, partida.SegundosRecargaArma(beto.Id));
        Assert.Equal(ResultadoAccion.ArmaRecargando, partida.Ejecutar(beto.Id, Accion.Extraer));

        partida.Tick(1);
        Assert.Equal(0, partida.SegundosRecargaArma(beto.Id));
        Assert.Equal(ResultadoAccion.ArmaDisparada, partida.Ejecutar(beto.Id, Accion.Extraer));
    }

    [Fact]
    [Trait("Requisito", Requisito)]
    public void Recarga_EsDeCadaJugador()
    {
        var (partida, ana, beto) = AyudaArenaViva.Crear(AyudaArenaViva.MapaGuardianes);
        partida.Ejecutar(beto.Id, Accion.Extraer);

        Assert.Equal(0, partida.SegundosRecargaArma(ana.Id));
        Assert.Equal(15, partida.SegundosRecargaArma(beto.Id));
    }

    [Fact]
    [Trait("Requisito", Requisito)]
    public void DosDisparos_LaParalisisNoSeAcumula()
    {
        var reglas = new ReglasArenaViva { SegundosRecargaArma = 1 };
        var (partida, _, beto, guardian) = GuardianADosPasosDeBeto(new ConfigPartida { ArenaViva = reglas });
        partida.Ejecutar(beto.Id, Accion.Extraer);
        partida.Tick(1);

        partida.Ejecutar(beto.Id, Accion.Extraer);

        Assert.Equal(5, guardian.SegundosParalizado);
    }

    [Fact]
    [Trait("Requisito", Requisito)]
    public void JuntoAlReactor_AccionSigueExtrayendo()
    {
        var (partida, ana, _) = AyudaArenaViva.Crear(AyudaArenaViva.MapaGuardianes);

        Assert.Equal(ResultadoAccion.EnergiaExtraida, partida.Ejecutar(ana.Id, Accion.Extraer));
        Assert.Empty(partida.TomarNovedades().OfType<ArmaDisparadaNovedad>());
        Assert.Equal(0, partida.SegundosRecargaArma(ana.Id));
    }

    [Fact]
    [Trait("Requisito", Requisito)]
    public void SinArenaViva_LejosDelReactorSigueRechazandoComoAntes()
    {
        var (partida, _, beto) = AyudaArenaViva.Crear(AyudaArenaViva.MapaGuardianes, arenaViva: false);

        Assert.Equal(ResultadoAccion.LejosDelReactor, partida.Ejecutar(beto.Id, Accion.Extraer));
    }

    [Fact]
    [Trait("Requisito", Requisito)]
    public void Disparo_NoAfectaJugadoresNiPuntajes()
    {
        var (partida, ana, beto, _) = GuardianADosPasosDeBeto();
        partida.Ejecutar(ana.Id, Accion.Extraer);
        var (puntosAna, puntosBeto) = (ana.Puntaje, beto.Puntaje);

        partida.Ejecutar(beto.Id, Accion.Extraer);

        Assert.Equal((puntosAna, puntosBeto), (ana.Puntaje, beto.Puntaje));
        Assert.False(ana.Congelado);
        Assert.False(beto.Congelado);
    }

    [Fact]
    [Trait("Requisito", Requisito)]
    [Trait("CA", "21.5")]
    public void Congelado_NoPuedeDisparar()
    {
        var (partida, _, beto, guardian) = GuardianADosPasosDeBeto();
        partida.Congelar(beto.Id);

        Assert.Equal(ResultadoAccion.Congelado, partida.Ejecutar(beto.Id, Accion.Extraer));
        Assert.Equal(0, guardian.SegundosParalizado);
        Assert.Equal(0, partida.SegundosRecargaArma(beto.Id));
    }

    [Fact]
    [Trait("Requisito", Requisito)]
    public void Reglas_ValoresPorDefectoYValidacion()
    {
        var reglas = new ReglasArenaViva();
        Assert.Equal((2, 5, 15), (reglas.AlcanceArma, reglas.SegundosParalisisArma, reglas.SegundosRecargaArma));

        Assert.Throws<DominioException>(() => new ReglasArenaViva { AlcanceArma = 0 }.Validar());
        Assert.Throws<DominioException>(() => new ReglasArenaViva { SegundosParalisisArma = 0 }.Validar());
        Assert.Throws<DominioException>(() => new ReglasArenaViva { SegundosRecargaArma = 0 }.Validar());
    }
}
