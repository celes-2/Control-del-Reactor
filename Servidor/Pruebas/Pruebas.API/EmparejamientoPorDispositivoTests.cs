using API.TiempoReal;
using Aplicacion.Puertos;
using Dominio.Juego;
using Dominio.Usuarios;
using Infraestructura.Partidas;
using Xunit;

namespace PruebasAPI;

[Trait("CA", "18.2")]
public class EmparejamientoPorDispositivoTests
{
    /// <summary>dispositivos.json falso, en memoria.</summary>
    private sealed class DispositivosFalsos : IDispositivoRepo
    {
        private readonly Dictionary<string, Guid> _asignados = new(StringComparer.OrdinalIgnoreCase);

        public Task AsignarAsync(string deviceId, Guid userId, CancellationToken ct = default)
        {
            _asignados[deviceId] = userId;
            return Task.CompletedTask;
        }

        public Task<DispositivoAsignado?> ObtenerPorDeviceIdAsync(string deviceId, CancellationToken ct = default) =>
            Task.FromResult<DispositivoAsignado?>(
                _asignados.TryGetValue(deviceId, out var id) ? new DispositivoAsignado(deviceId, id, DateTime.UtcNow) : null);
    }

    private static async Task<(EmparejamientoPorDispositivo emparejamiento, DispositivosFalsos dispositivos, Jugador ana, Jugador beto)>
        Preparar(bool iniciar = true)
    {
        var repo = new PartidaEnMemoriaRepositorio();
        var partida = Partida.Crear(new ConfigPartida());
        var ana = partida.Unirse(Guid.NewGuid(), "Ana", Guid.NewGuid());
        var beto = partida.Unirse(Guid.NewGuid(), "Beto", Guid.NewGuid());
        if (iniciar)
            partida.Iniciar();
        await repo.AgregarAsync(partida);

        var dispositivos = new DispositivosFalsos();
        var emparejamiento = new EmparejamientoPorDispositivo(dispositivos, repo, new EmparejamientoPorOrden(repo));
        return (emparejamiento, dispositivos, ana, beto);
    }

    [Fact]
    public async Task ControlEmparejado_DevuelveSuJugador_AunqueElOrdenDigaOtro()
    {
        var (emparejamiento, dispositivos, _, beto) = await Preparar();
        await dispositivos.AsignarAsync("pico-01", beto.Id); // por orden pico-01 seria Ana

        Assert.Equal(beto.Id, await emparejamiento.BuscarJugadorAsync("pico-01"));
    }

    [Fact]
    public async Task ControlEmparejado_JugadorSinPartidaEnCurso_DevuelveNull()
    {
        var (emparejamiento, dispositivos, _, _) = await Preparar();
        await dispositivos.AsignarAsync("pico-01", Guid.NewGuid()); // jugador que no esta jugando

        Assert.Null(await emparejamiento.BuscarJugadorAsync("pico-01"));
    }

    [Fact]
    public async Task ControlEmparejado_PartidaEnEspera_DevuelveNull()
    {
        var (emparejamiento, dispositivos, ana, _) = await Preparar(iniciar: false);
        await dispositivos.AsignarAsync("pico-01", ana.Id);

        Assert.Null(await emparejamiento.BuscarJugadorAsync("pico-01"));
    }

    [Fact]
    public async Task ControlSinEmparejar_UsaElOrdenComoRespaldo()
    {
        var (emparejamiento, _, ana, beto) = await Preparar();

        Assert.Equal(ana.Id, await emparejamiento.BuscarJugadorAsync("pico-01"));
        Assert.Equal(beto.Id, await emparejamiento.BuscarJugadorAsync("pico-02"));
    }

    [Fact]
    public async Task ControlDesconocidoSinEmparejar_DevuelveNull()
    {
        var (emparejamiento, _, _, _) = await Preparar();

        Assert.Null(await emparejamiento.BuscarJugadorAsync("control-x"));
    }
}