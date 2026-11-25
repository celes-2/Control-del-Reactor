using API.TiempoReal;
using Dominio.Juego;
using Xunit;

namespace PruebasAPI;

[Trait("CA", "18.5")]
public class RechazosTests
{
    [Theory]
    [InlineData(ResultadoAccion.Congelado, "JUGADOR_CONGELADO")]
    [InlineData(ResultadoAccion.LejosDelReactor, "LEJOS_DEL_REACTOR")]
    [InlineData(ResultadoAccion.ReactorDesactivado, "REACTOR_DESACTIVADO")]
    [InlineData(ResultadoAccion.ReactorVacio, "REACTOR_VACIO")]
    [InlineData(ResultadoAccion.Enfriamiento, "ENFRIAMIENTO")]
    public void CadaRechazo_TieneSuCodigoYUnMensaje(ResultadoAccion resultado, string codigo)
    {
        var usuario = Guid.NewGuid();

        var rechazo = Rechazos.Desde(usuario, resultado);

        Assert.NotNull(rechazo);
        Assert.Equal(usuario, rechazo.UserId);
        Assert.Equal(codigo, rechazo.Code);
        Assert.False(string.IsNullOrWhiteSpace(rechazo.Message));
    }

    [Theory]
    [InlineData(ResultadoAccion.EnergiaExtraida)]
    [InlineData(ResultadoAccion.Movido)]
    [InlineData(ResultadoAccion.Bloqueado)]
    public void ResultadoQueNoEsRechazo_DevuelveNull(ResultadoAccion resultado)
    {
        Assert.Null(Rechazos.Desde(Guid.NewGuid(), resultado));
    }

    [Theory]
    [InlineData("PARTIDA_NO_ACTIVA")]
    [InlineData("JUGADOR_ELIMINADO")]
    public void CodigoDeExcepcion_SeTraduceARechazo(string codigo)
    {
        var usuario = Guid.NewGuid();

        var rechazo = Rechazos.DesdeCodigo(usuario, codigo);

        Assert.NotNull(rechazo);
        Assert.Equal(usuario, rechazo.UserId);
        Assert.Equal(codigo, rechazo.Code);
        Assert.False(string.IsNullOrWhiteSpace(rechazo.Message));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("JUGADOR_AJENO")]
    [InlineData("OTRO")]
    public void CodigoQueNoSeMuestra_DevuelveNull(string? codigo)
    {
        Assert.Null(Rechazos.DesdeCodigo(Guid.NewGuid(), codigo));
    }
}