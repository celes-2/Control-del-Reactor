using Aplicacion.CasosDeUso;
using Contratos.Partidas;
using Dominio.Comun;
using Dominio.Juego;

namespace API.Mapeos;

/// <summary>Traduce entre los contratos HTTP y los tipos de la aplicación / dominio.</summary>
internal static class PartidaMapeo
{
    public static ConfigPartida AConfig(CrearPartidaSolicitud s)
    {
        var porDefecto = new ConfigPartida();
        return porDefecto with
        {
            MaxJugadores = s.MaxJugadores ?? porDefecto.MaxJugadores,
            Ancho = s.Ancho ?? porDefecto.Ancho,
            Alto = s.Alto ?? porDefecto.Alto,
            DuracionSegundos = s.DuracionSegundos ?? porDefecto.DuracionSegundos,
            EnergiaObjetivo = s.EnergiaObjetivo ?? porDefecto.EnergiaObjetivo,
            CapacidadReactor = s.CapacidadReactor ?? porDefecto.CapacidadReactor,
            EnergiaPorExtraccion = s.EnergiaPorExtraccion ?? porDefecto.EnergiaPorExtraccion,
            RegeneracionPorSegundo = s.RegeneracionPorSegundo ?? porDefecto.RegeneracionPorSegundo,
            VidaInicial = s.VidaInicial ?? porDefecto.VidaInicial,
            DanoTrampa = s.DanoTrampa ?? porDefecto.DanoTrampa
        };
    }

    public static IReadOnlyList<Obstaculo>? AObstaculos(IReadOnlyList<ObstaculoSolicitud>? solicitud)
    {
        if (solicitud is null)
            return null;

        return solicitud.Select(o =>
        {
            if (!Enum.TryParse<TipoObstaculo>(o.Tipo, ignoreCase: true, out var tipo) || !Enum.IsDefined(tipo))
                throw new DominioException($"Tipo de obstáculo desconocido: '{o.Tipo}'. Use Muro o Trampa.");

            return new Obstaculo(new Posicion(o.X, o.Y), tipo);
        }).ToList();
    }

    public static PartidaRespuesta ARespuesta(this PartidaVista v) => new(
        v.Id,
        v.Estado.ToString(),
        v.SegundosRestantes,
        v.GanadorId,
        v.MaxJugadores,
        v.Ancho,
        v.Alto,
        v.ReactorX,
        v.ReactorY,
        v.EnergiaReactor,
        v.CapacidadReactor,
        v.EnergiaObjetivo,
        v.Jugadores
            .Select(j => new JugadorRespuesta(j.Id, j.Nombre, j.AvatarId, j.X, j.Y, j.Vida, j.Energia, j.Eliminado))
            .ToList(),
        v.Obstaculos
            .Select(o => new ObstaculoRespuesta(o.X, o.Y, o.Tipo.ToString()))
            .ToList(),
        v.Escenario);

    public static PartidaResumenRespuesta ARespuesta(this PartidaResumen r) =>
        new(r.Id, r.Jugadores, r.MaxJugadores, r.Anfitrion);
}
