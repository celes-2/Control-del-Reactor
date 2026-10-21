using System;
using Reactor.Cliente.Servicios;

namespace Reactor.Cliente.Presentadores
{
    /// <summary>
    /// Lógica de la pantalla de configuración (US-08). Parte de los ajustes de la última partida (CA-08.1),
    /// deja elegir cómo termina (CA-08.4) y los segundos de congelamiento (CA-08.5), y al crear la partida pide
    /// el id al servidor y abre la sala de espera. La configuración solo se puede cambiar aquí, antes de
    /// iniciar (CA-08.2). Es C# puro, así que se prueba con una vista falsa.
    /// </summary>
    public sealed class ConfiguracionPresentador : IDisposable
    {
        readonly IConfiguracionVista vista;
        readonly IApiPartidas api;
        readonly IAlmacenAjustes almacen;
        readonly string usuarioId;
        readonly Action<string> alCrear;
        readonly Action alUnirse;

        AjustesPartida ajustes;
        bool creando;
        bool liberado;

        public ConfiguracionPresentador(
            IConfiguracionVista vista, IApiPartidas api, IAlmacenAjustes almacen, string usuarioId, Action<string> alCrear,
            Action alUnirse = null)
        {
            this.vista = vista ?? throw new ArgumentNullException(nameof(vista));
            this.api = api ?? throw new ArgumentNullException(nameof(api));
            this.almacen = almacen ?? throw new ArgumentNullException(nameof(almacen));
            this.alCrear = alCrear ?? throw new ArgumentNullException(nameof(alCrear));
            this.usuarioId = usuarioId;
            this.alUnirse = alUnirse;

            vista.AjusteCambiado += AlCambiarAjuste;
            vista.ModoElegido += AlElegirModo;
            vista.CrearPulsado += AlPulsarCrear;
            vista.UnirsePulsado += AlPulsarUnirse;
        }

        public AjustesPartida Ajustes => ajustes;

        public void Iniciar()
        {
            ajustes = almacen.Cargar();
            ajustes.Normalizar();
            Refrescar();
        }

        public void Dispose()
        {
            liberado = true;
            vista.AjusteCambiado -= AlCambiarAjuste;
            vista.ModoElegido -= AlElegirModo;
            vista.CrearPulsado -= AlPulsarCrear;
            vista.UnirsePulsado -= AlPulsarUnirse;
        }

        void AlCambiarAjuste(CampoAjuste campo, int direccion)
        {
            if (creando) return;
            switch (campo)
            {
                case CampoAjuste.Valor: ajustes.CambiarValor(direccion); break;
                case CampoAjuste.Congelamiento: ajustes.CambiarCongelamiento(direccion); break;
                case CampoAjuste.Jugadores: ajustes.CambiarJugadores(direccion); break;
            }
            Refrescar();
        }

        void AlElegirModo(ModoFin modo)
        {
            if (creando) return;
            ajustes.Modo = modo;
            Refrescar();
        }

        void AlPulsarUnirse()
        {
            if (!creando) alUnirse?.Invoke();
        }

        async void AlPulsarCrear()
        {
            if (creando) return;

            if (string.IsNullOrEmpty(usuarioId))
            {
                vista.MostrarAviso("Primero debes iniciar sesión.");
                return;
            }

            creando = true;
            vista.BloquearCrear(true);
            vista.MostrarAviso("Creando partida...");
            try
            {
                var partidaId = await api.CrearPartidaAsync(usuarioId, ajustes.Copia());
                if (liberado) return; // la escena ya cambió mientras esperábamos

                almacen.Guardar(ajustes);
                alCrear(partidaId);
            }
            catch (ApiException e)
            {
                if (liberado) return;
                vista.MostrarAviso(e.Message);
            }
            catch (Exception)
            {
                if (liberado) return;
                vista.MostrarAviso("No se pudo crear la partida. Inténtalo de nuevo.");
            }
            finally
            {
                if (!liberado)
                {
                    creando = false;
                    vista.BloquearCrear(false);
                }
            }
        }

        void Refrescar()
        {
            vista.MostrarModo(ajustes.Modo);
            if (ajustes.Modo == ModoFin.Tiempo)
                vista.MostrarValor("Duración", FormatoTiempo(ajustes.DuracionSegundos));
            else
                vista.MostrarValor("Puntos para ganar", ajustes.PuntosObjetivo + " puntos");
            vista.MostrarCongelamiento(ajustes.SegundosCongelamiento + (ajustes.SegundosCongelamiento == 1 ? " segundo" : " segundos"));
            vista.MostrarJugadores(ajustes.MaxJugadores + " jugadores");
        }

        public static string FormatoTiempo(int segundos) => $"{segundos / 60}:{segundos % 60:00}";
    }
}
