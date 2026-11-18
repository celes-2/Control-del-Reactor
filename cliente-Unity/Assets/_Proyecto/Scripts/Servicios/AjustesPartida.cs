using System;
using System.Collections.Generic;

namespace Reactor.Cliente.Servicios
{
    /// <summary>Cómo termina la partida (CA-08.4).</summary>
    public enum ModoFin
    {
        Tiempo,
        Puntos,
    }

    /// <summary>
    /// Parámetros que el jugador elige en la pantalla de configuración (US-08).
    /// Solo guarda y limita valores: las reglas de verdad las valida el servidor.
    /// </summary>
    public sealed class AjustesPartida
    {
        public const int MinSegundos = 60, MaxSegundos = 600, PasoSegundos = 30;
        public const int MinPuntos = 5, MaxPuntos = 100, PasoPuntos = 5;
        public const int MinCongelamiento = 1, MaxCongelamiento = 10;
        public const int MinJugadores = 2, MaxJugadoresPermitidos = 4;
        public const int MinGuardianes = 2, MaxGuardianes = 4;

        public ModoFin Modo { get; set; } = ModoFin.Tiempo;
        public int DuracionSegundos { get; set; } = 180;
        public int PuntosObjetivo { get; set; } = 20;
        public int SegundosCongelamiento { get; set; } = 3;
        public int MaxJugadores { get; set; } = 4;

        /// <summary>Id del escenario del servidor ("laboratorio", "estacion", "militar"); vacío = mapa clásico.</summary>
        public string EscenarioId { get; set; } = "";

        /// <summary>Guardianes de la partida (US-21): de 2 a 4.</summary>
        public int CantidadGuardianes { get; set; } = 4;

        public AjustesPartida Copia() => (AjustesPartida)MemberwiseClone();

        public void CambiarValor(int direccion)
        {
            if (Modo == ModoFin.Tiempo)
                DuracionSegundos = Limitar(DuracionSegundos + direccion * PasoSegundos, MinSegundos, MaxSegundos);
            else
                PuntosObjetivo = Limitar(PuntosObjetivo + direccion * PasoPuntos, MinPuntos, MaxPuntos);
        }

        public void CambiarCongelamiento(int direccion) =>
            SegundosCongelamiento = Limitar(SegundosCongelamiento + direccion, MinCongelamiento, MaxCongelamiento);

        /// <summary>Pasa al escenario siguiente o anterior de la lista (da la vuelta al llegar al final).</summary>
        public void CambiarEscenario(int direccion, IReadOnlyList<EscenarioInfo> escenarios)
        {
            if (escenarios == null || escenarios.Count == 0) return;
            var actual = IndiceDe(EscenarioId, escenarios);
            EscenarioId = escenarios[((actual + direccion) % escenarios.Count + escenarios.Count) % escenarios.Count].Id;
        }

        public void CambiarJugadores(int direccion) =>
            MaxJugadores = Limitar(MaxJugadores + direccion, MinJugadores, MaxJugadoresPermitidos);

        public void CambiarGuardianes(int direccion) =>
            CantidadGuardianes = Limitar(CantidadGuardianes + direccion, MinGuardianes, MaxGuardianes);

        /// <summary>Corrige valores fuera de rango (por ejemplo, de un valor guardado viejo).</summary>
        public void Normalizar()
        {
            DuracionSegundos = Limitar(DuracionSegundos, MinSegundos, MaxSegundos);
            PuntosObjetivo = Limitar(PuntosObjetivo, MinPuntos, MaxPuntos);
            SegundosCongelamiento = Limitar(SegundosCongelamiento, MinCongelamiento, MaxCongelamiento);
            MaxJugadores = Limitar(MaxJugadores, MinJugadores, MaxJugadoresPermitidos);
            CantidadGuardianes = Limitar(CantidadGuardianes, MinGuardianes, MaxGuardianes);
            EscenarioId = EscenarioId ?? "";
            if (!Enum.IsDefined(typeof(ModoFin), Modo)) Modo = ModoFin.Tiempo;
        }

        /// <summary>Si el escenario guardado ya no existe, vuelve al mapa clásico; y respeta los límites del escenario.</summary>
        public void AjustarA(IReadOnlyList<EscenarioInfo> escenarios)
        {
            if (escenarios == null || escenarios.Count == 0) return;
            var escenario = escenarios[IndiceDe(EscenarioId, escenarios)];
            EscenarioId = escenario.Id;
            if (escenario.MaxJugadores >= MinJugadores) MaxJugadores = Math.Min(MaxJugadores, escenario.MaxJugadores);
            CantidadGuardianes = Limitar(CantidadGuardianes, Math.Max(MinGuardianes, escenario.MinGuardianes),
                Math.Min(MaxGuardianes, Math.Max(escenario.MaxGuardianes, MinGuardianes)));
        }

        static int IndiceDe(string id, IReadOnlyList<EscenarioInfo> lista)
        {
            for (var i = 0; i < lista.Count; i++)
                if (lista[i].Id == (id ?? "")) return i;
            return 0;
        }

        static int Limitar(int valor, int minimo, int maximo) => Math.Max(minimo, Math.Min(maximo, valor));
    }
}
