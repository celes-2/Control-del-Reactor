using System.Text.Json;
using API.TiempoReal;
using Contratos.TiempoReal;
using Dominio.Juego;
using Infraestructura.Partidas;
using Xunit;

namespace PruebasAPI;

public class TiempoRealTests
{
    // Arena por defecto 11x11: Ana aparece en (0,0) y Beto en (10,0).
    private static (Partida partida, Jugador ana, Jugador beto) CrearPartida(
        ConfigPartida? config = null, bool iniciar = true)
    {
        var partida = Partida.Crear(config ?? new ConfigPartida());
        var ana = partida.Unirse(Guid.NewGuid(), "Ana", Guid.NewGuid());
        var beto = partida.Unirse(Guid.NewGuid(), "Beto", Guid.NewGuid());
        if (iniciar)
            partida.Iniciar();
        return (partida, ana, beto);
    }

    // ---------- Emparejamiento de controles (plan B: pico-01 = primer jugador) ----------

    [Theory]
    [InlineData("pico-01", 0)]
    [InlineData("pico-02", 1)]
    [InlineData("PICO-03", 2)]
    public void IndiceDelControl_NombreValido_DevuelveSuPosicion(string deviceId, int esperado)
    {
        Assert.Equal(esperado, EmparejamientoPorOrden.IndiceDelControl(deviceId));
    }

    [Theory]
    [InlineData("pico-00")]
    [InlineData("pico-x")]
    [InlineData("control-01")]
    [InlineData("")]
    public void IndiceDelControl_NombreInvalido_DevuelveNull(string deviceId)
    {
        Assert.Null(EmparejamientoPorOrden.IndiceDelControl(deviceId));
    }

    [Fact]
    public async Task BuscarJugador_ConPartidaEnCurso_AsignaPorOrdenDeLlegada()
    {
        var repo = new PartidaEnMemoriaRepositorio();
        var (partida, ana, beto) = CrearPartida();
        await repo.AgregarAsync(partida);
        var emparejamiento = new EmparejamientoPorOrden(repo);

        Assert.Equal(ana.Id, await emparejamiento.BuscarJugadorAsync("pico-01"));
        Assert.Equal(beto.Id, await emparejamiento.BuscarJugadorAsync("pico-02"));
        Assert.Null(await emparejamiento.BuscarJugadorAsync("pico-03")); // no hay tercer jugador
    }

    [Fact]
    public async Task BuscarJugador_SinPartidaEnCurso_DevuelveNull()
    {
        var repo = new PartidaEnMemoriaRepositorio();
        var (partida, _, _) = CrearPartida(iniciar: false); // sigue en Espera
        await repo.AgregarAsync(partida);

        Assert.Null(await new EmparejamientoPorOrden(repo).BuscarJugadorAsync("pico-01"));
    }

    // ---------- Reloj ----------

    [Fact]
    public void Reloj_PartidaEnCurso_DescuentaUnSegundo()
    {
        var (partida, _, _) = CrearPartida(new ConfigPartida { DuracionSegundos = 30 });

        var cambio = ServicioReloj.Avanzar(partida);

        Assert.NotNull(cambio);
        Assert.Equal(29, cambio.TimeLeft);
        Assert.Equal(1, cambio.TimeElapsed);
        Assert.False(cambio.HuboProrroga);
        Assert.False(cambio.Termino);
    }

    [Fact]
    public void Reloj_SeAcabaElTiempoEnEmpate_AvisaProrroga()
    {
        var (partida, _, _) = CrearPartida(new ConfigPartida { DuracionSegundos = 1 }); // 0 a 0

        var cambio = ServicioReloj.Avanzar(partida);

        Assert.NotNull(cambio);
        Assert.True(cambio.HuboProrroga);
        Assert.False(cambio.Termino);
        Assert.Equal(Partida.SegundosPorEmpate, cambio.SegundosRestantes);
        Assert.Equal(EstadoPartida.EnCurso, partida.Estado);
    }

    [Fact]
    public void Reloj_SeAcabaElTiempoConGanador_AvisaFin()
    {
        var (partida, ana, _) = CrearPartida(new ConfigPartida { DuracionSegundos = 1 });
        partida.Ejecutar(ana.Id, Accion.Abajo); // Ana 1, Beto 0

        var cambio = ServicioReloj.Avanzar(partida);

        Assert.NotNull(cambio);
        Assert.True(cambio.Termino);
        Assert.Equal(ana.Id, partida.GanadorId);
    }

    [Fact]
    public void Reloj_ModoPorPuntos_NoTieneTiempoLimite()
    {
        var (partida, _, _) = CrearPartida(new ConfigPartida { ModoFin = ModoFin.Puntos, PuntosObjetivo = 5 });

        var cambio = ServicioReloj.Avanzar(partida);

        Assert.NotNull(cambio);
        Assert.Null(cambio.TimeLeft);
        Assert.Equal(1, cambio.TimeElapsed);
    }

    [Fact]
    public void Reloj_PartidaSinIniciar_NoHaceNada()
    {
        var (partida, _, _) = CrearPartida(iniciar: false);

        Assert.Null(ServicioReloj.Avanzar(partida));
        Assert.Equal(0, partida.SegundosTranscurridos);
    }

    // ---------- Mensajes WebSocket ----------

    [Fact]
    public void Leer_MensajeValido_DevuelveTipoYData()
    {
        var id = Guid.NewGuid();
        var json = $"{{\"type\":\"match:join\",\"data\":{{\"matchId\":\"{id}\"}}}}";

        var mensaje = SerializadorMensajes.Leer(json);

        Assert.NotNull(mensaje);
        Assert.Equal(TiposMensaje.MatchJoin, mensaje.Value.Tipo);
        Assert.Equal(id, mensaje.Value.Data.GetProperty("matchId").GetGuid());
    }

    [Theory]
    [InlineData("hola")]               // no es JSON
    [InlineData("[]")]                 // no es un objeto
    [InlineData("{\"data\":{}}")]      // falta type
    [InlineData("{\"type\":5}")]       // type no es texto
    public void Leer_MensajeInvalido_DevuelveNull(string json)
    {
        Assert.Null(SerializadorMensajes.Leer(json));
    }

    [Fact]
    public void Serializar_UsaElFormatoDelContrato()
    {
        var id = Guid.NewGuid();

        var json = SerializadorMensajes.Serializar(TiposMensaje.PlayerMoved, new JugadorMovidoData(id, 1, 2, 3));

        using var documento = JsonDocument.Parse(json);
        var raiz = documento.RootElement;
        Assert.Equal("player:moved", raiz.GetProperty("type").GetString());
        var data = raiz.GetProperty("data");
        Assert.Equal(id, data.GetProperty("userId").GetGuid()); // camelCase, como espera Unity
        Assert.Equal(2, data.GetProperty("y").GetInt32());
        Assert.Equal(3, data.GetProperty("score").GetInt32());
    }
}
