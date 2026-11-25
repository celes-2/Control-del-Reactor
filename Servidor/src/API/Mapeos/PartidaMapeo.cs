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
            DanoTrampa = s.DanoTrampa ?? porDefecto.DanoTrampa,
            ModoFin = AModoFin(s.ModoFin) ?? porDefecto.ModoFin,
            PuntosObjetivo = s.PuntosObjetivo ?? porDefecto.PuntosObjetivo,
            SegundosCongelamiento = s.SegundosCongelamiento ?? porDefecto.SegundosCongelamiento,
            EscenarioId = string.IsNullOrWhiteSpace(s.EscenarioId) ? null : s.EscenarioId.Trim(),
            CantidadGuardianes = s.CantidadGuardianes ?? porDefecto.CantidadGuardianes   // 2 a 4: lo valida el dominio
        };
    }

    private static ModoFin? AModoFin(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        if (!Enum.TryParse<ModoFin>(texto.Trim(), ignoreCase: true, out var modo) || !Enum.IsDefined(modo))
            throw new DominioException($"Modo de fin desconocido: '{texto}'. Use Tiempo o Puntos.");

        return modo;
    }

    public static IReadOnlyList<Obstaculo>? AObstaculos(IReadOnlyList<ObstaculoSolicitud>? solicitud)
    {
        if (solicitud is null)
            return null;

        return solicitud.Select(o =>
        {
            if (!Enum.TryParse<TipoObstaculo>(o.Tipo, ignoreCase: true, out var tipo) || !Enum.IsDefined(tipo))
                throw new DominioException($"Tipo de obstáculo desconocido: '{o.Tipo}'. Use Muro, Trampa o Puerta.");

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
        v.Escenario,
        v.EscenarioId,
        v.CantidadGuardianes);

    public static EscenarioRespuesta ARespuesta(this EscenarioVista e) => new(
        e.Id,
        e.Nombre,
        e.Descripcion,
        e.Ancho,
        e.Alto,
        e.Filas,
        e.Puertas.Select(p => new PosicionRespuesta(p.X, p.Y)).ToList(),
        e.InicioGuardianes.Select(p => new PosicionRespuesta(p.X, p.Y)).ToList(),
        e.MaxJugadores,
        e.MinGuardianes,
        e.MaxGuardianes,
        e.GuardianesPorDefecto,
        e.TrampasAleatorias,
        e.PuntosEnergia.Select(p => new PosicionRespuesta(p.X, p.Y)).ToList());

    public static PartidaResumenRespuesta ARespuesta(this PartidaResumen r) =>
        new(r.Id, r.Jugadores, r.MaxJugadores, r.Anfitrion);
}
