using System;
using System.Linq;
using Reactor.Cliente.Servicios;

namespace Reactor.Cliente.Presentadores
{
    /// <summary>
    /// Lógica de la pantalla Unirse: muestra las partidas en espera y deja entrar a una que no esté llena.
    /// No llama a "unirse" por REST: guarda el id y abre la sala de espera, que hace match:join por WebSocket
    /// (el servidor une al jugador si la partida sigue en espera). Es C# puro, así que se prueba con una vista falsa.
    /// </summary>
    public sealed class UnirsePresentador : IDisposable
    {
        readonly IUnirseVista vista;
        readonly IApiPartidas api;
        readonly Action<string> alUnirse;
        readonly Action alVolver;

        bool cargando;
        bool liberado;

        public UnirsePresentador(IUnirseVista vista, IApiPartidas api, Action<string> alUnirse, Action alVolver)
        {
            this.vista = vista ?? throw new ArgumentNullException(nameof(vista));
            this.api = api ?? throw new ArgumentNullException(nameof(api));
            this.alUnirse = alUnirse ?? throw new ArgumentNullException(nameof(alUnirse));
            this.alVolver = alVolver ?? throw new ArgumentNullException(nameof(alVolver));

            vista.UnirsePulsado += AlPulsarUnirse;
            vista.ActualizarPulsado += Actualizar;
            vista.VolverPulsado += AlPulsarVolver;
        }

        public void Iniciar() => Actualizar();

        public void Dispose()
        {
            liberado = true;
            vista.UnirsePulsado -= AlPulsarUnirse;
            vista.ActualizarPulsado -= Actualizar;
            vista.VolverPulsado -= AlPulsarVolver;
        }

        async void Actualizar()
        {
            if (cargando) return;
            cargando = true;
            try
            {
                var partidas = await api.ListarAbiertasAsync();
                if (liberado) return;

                var lista = partidas?.Where(p => p != null && !string.IsNullOrEmpty(p.Id)).ToList() ?? new System.Collections.Generic.List<PartidaAbierta>();
                vista.MostrarPartidas(lista);
                vista.MostrarEstado(lista.Count == 0 ? "No hay partidas abiertas. Pide al anfitrión que cree una." : "Elige una partida");
            }
            catch (ApiException e)
            {
                if (!liberado) vista.MostrarAviso(e.Message);
            }
            catch (Exception)
            {
                if (!liberado) vista.MostrarAviso("No se pudo obtener la lista de partidas.");
            }
            finally
            {
                cargando = false;
            }
        }

        void AlPulsarUnirse(string partidaId)
        {
            if (string.IsNullOrEmpty(partidaId)) return;
            alUnirse(partidaId);
        }

        void AlPulsarVolver() => alVolver();
    }
}
