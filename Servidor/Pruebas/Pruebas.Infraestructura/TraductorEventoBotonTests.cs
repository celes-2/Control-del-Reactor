using Dominio.Juego;
using Infraestructura.Hardware;
using Xunit;

namespace PruebasInfraestructura;

public class TraductorEventoBotonTests
{
    private const string Topic = "arena/devices/pico-01/events";

    // ---------- Positivas ----------

    [Theory]
    [InlineData("UP", Accion.Arriba)]
    [InlineData("DOWN", Accion.Abajo)]
    [InlineData("LEFT", Accion.Izquierda)]
    [InlineData("RIGHT", Accion.Derecha)]
    [InlineData("ACTION", Accion.Especial)]
    public void Traducir_CadaBotonValido_DevuelveSuAccion(string boton, Accion esperada)
    {
        var mensaje = "{\"button\":\"" + boton + "\",\"seq\":7}";

        var evento = TraductorEventoBoton.Traducir(Topic, mensaje);

        Assert.NotNull(evento);
        Assert.Equal("pico-01", evento.DeviceId);
        Assert.Equal(esperada, evento.Accion);
        Assert.Equal(7, evento.Seq);
    }

    [Fact]
    public void Traducir_SinSeq_UsaCero()
    {
        var evento = TraductorEventoBoton.Traducir(Topic, "{\"button\":\"UP\"}");

        Assert.NotNull(evento);
        Assert.Equal(0, evento.Seq);
    }

    [Fact]
    public void Traducir_BotonEnMinusculas_SeAcepta()
    {
        var evento = TraductorEventoBoton.Traducir(Topic, "{\"button\":\"up\",\"seq\":1}");

        Assert.NotNull(evento);
        Assert.Equal(Accion.Arriba, evento.Accion);
    }

    // ---------- Negativas: mensaje invalido ----------

    [Theory]
    [InlineData("esto no es json")]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("{\"seq\":3}")]                     // falta button
    [InlineData("{\"button\":5}")]                  // button no es texto
    [InlineData("{\"button\":\"JUMP\",\"seq\":1}")] // boton que no existe
    public void Traducir_MensajeInvalido_DevuelveNull(string mensaje)
    {
        Assert.Null(TraductorEventoBoton.Traducir(Topic, mensaje));
    }

    // ---------- Negativas: topic invalido ----------

    [Theory]
    [InlineData("arena/devices/pico-01/status")] // es el topic de estado, no de eventos
    [InlineData("otro/topic")]
    [InlineData("arena/devices//events")]       // sin deviceId
    [InlineData("arena/devices/a/b/events")]    // deviceId con "/"
    [InlineData("arena/devices/events")]
    public void Traducir_TopicInvalido_DevuelveNull(string topic)
    {
        Assert.Null(TraductorEventoBoton.Traducir(topic, "{\"button\":\"UP\",\"seq\":1}"));
    }
}