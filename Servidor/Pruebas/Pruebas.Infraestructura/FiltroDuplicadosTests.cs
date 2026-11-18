using Dominio.Juego;
using Infraestructura.Hardware;
using Xunit;

namespace PruebasInfraestructura;

[Trait("CA", "18.8")]
public class FiltroDuplicadosTests
{
    private static EventoBoton Evento(string deviceId, int seq) => new(deviceId, Accion.Extraer, seq);

    [Fact]
    public void PrimerEvento_EsNuevo()
    {
        var filtro = new FiltroDuplicados();

        Assert.True(filtro.EsNuevo(Evento("pico-01", 1)));
    }

    [Fact]
    public void MismoSeqDosVeces_CuentaUna()
    {
        var filtro = new FiltroDuplicados();

        Assert.True(filtro.EsNuevo(Evento("pico-01", 5)));
        Assert.False(filtro.EsNuevo(Evento("pico-01", 5)));
    }

    [Fact]
    public void SeqMenor_SeDescarta()
    {
        var filtro = new FiltroDuplicados();
        filtro.EsNuevo(Evento("pico-01", 10));

        Assert.False(filtro.EsNuevo(Evento("pico-01", 3)));
    }

    [Fact]
    public void SeqMayor_EsNuevo()
    {
        var filtro = new FiltroDuplicados();
        filtro.EsNuevo(Evento("pico-01", 10));

        Assert.True(filtro.EsNuevo(Evento("pico-01", 11)));
    }

    [Fact]
    public void CadaControlTieneSuPropioContador()
    {
        var filtro = new FiltroDuplicados();
        filtro.EsNuevo(Evento("pico-01", 10));

        Assert.True(filtro.EsNuevo(Evento("pico-02", 1)));
    }

    [Fact]
    public void DespuesDeReiniciar_AceptaSeqBajo()
    {
        var filtro = new FiltroDuplicados();
        filtro.EsNuevo(Evento("pico-01", 50));

        filtro.Reiniciar("pico-01");

        Assert.True(filtro.EsNuevo(Evento("pico-01", 1)));
    }

    [Fact]
    public void Reiniciar_NoAfectaAOtrosControles()
    {
        var filtro = new FiltroDuplicados();
        filtro.EsNuevo(Evento("pico-01", 10));
        filtro.EsNuevo(Evento("pico-02", 10));

        filtro.Reiniciar("pico-01");

        Assert.False(filtro.EsNuevo(Evento("pico-02", 10)));
    }
}