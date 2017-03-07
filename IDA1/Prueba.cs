using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace IDA1
{
    class Prueba : INotifyPropertyChanged
    {
        /// <summary>
        /// Contiene los datos de una medida de oclusion.
        /// </summary>
        public class DatoOclusion
        {
            #region Implementacion de INotifyPropertyChanged
            public event PropertyChangedEventHandler PropertyChanged;
            private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }

            #endregion

            private TimeSpan tiempo;
            private int presion;

            public TimeSpan Tiempo
            {
                get { return tiempo; }
                set { tiempo = value; NotifyPropertyChanged(); }
            }
            public int Presion
            {
                get { return presion; }
                set { presion = value; NotifyPropertyChanged(); }
            }

            /// <summary>
            /// Crea una instancia de <see cref="DatoOclusion"/>
            /// </summary>
            /// <param name="tiemp">Lapso de tiempo en milisegundos desde el comienzo de la prueba en el que se coge la medida.</param>
            /// <param name="pres">Lectura de la presion en mmHg</param>
            public DatoOclusion(double tiemp = 0,  int pres = 0)
            {
                Tiempo = TimeSpan.FromMilliseconds(tiemp); 
                Presion = pres;
            }
        }
        /// <summary>
        /// Contiene los datos de una medida de flujo.
        /// </summary>
        public class DatoVolumen
        {
            /// <summary>
            /// Lapso de tiempo desde el inicio de la prueba hasta la toma de la medida 
            /// </summary>
            public TimeSpan Tiempo { get; set; }
            /// <summary>
            /// Volumen total desde el inicio de la prueba.
            /// </summary>
            public double Volumen { get; set; }
            public double FlujoInstant { get; set; }
            
            /// <summary>
            /// Crea una nueva instancia de DatoVolumen.
            /// </summary>
            /// <param name="tiemp">Lapso de tiempo, en milisegundos, desde el comienzo de la prueba en el que se coge la medida.</param>
            /// <param name="vol">Volumen total, en ml, hasta la toma de la medida</param>
            public DatoVolumen(double tiemp = 0, double vol = 0)
            {
                Tiempo = TimeSpan.FromMilliseconds(tiemp);  //de milisegundos a horas
                Volumen = vol;
                FlujoInstant = Volumen / Tiempo.TotalHours;
            }
        }

        #region Implementacion de INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Campos declaracion
        Form1 parent;
        List<DatoOclusion> datosOclusion = new List<DatoOclusion>();
        List<DatoVolumen> datosVolumen = new List<DatoVolumen>();
        DateTime inicioOclusion;
        DateTime inicioFlujo;


        #endregion

        #region Propiedades declaracion
        public bool HayOclusion { get; set; }
        public bool HayFlujo { get; set; }
        public DateTime InicioOclusion { get { return inicioOclusion; } }
        public DateTime InicioFlujo { get { return inicioFlujo; } }
        public DateTime InicioPrueba => (inicioFlujo < inicioOclusion) ? inicioFlujo : inicioOclusion;
        public List<DatoOclusion> DatosOclusion { get { return datosOclusion; } }
        public List<DatoVolumen> DatosFlujo { get { return datosVolumen; } }
        public TimeSpan DuracionOclusion { get; private set; }
        public int PresionMaxima { get; private set; }

        #endregion

        public Prueba(Form1 parent)
        {
            this.parent = parent;
            HayFlujo = false;
            HayOclusion = false;
            inicioOclusion = DateTime.MinValue;
            inicioFlujo = DateTime.MinValue;
        }

        public void AddDatoOclusion(DatoOclusion dato)
        {
            if (datosOclusion.Count == 0) inicioOclusion = DateTime.Now;
            datosOclusion.Add(dato);
            DuracionOclusion = dato.Tiempo;
            PresionMaxima = dato.Presion;
            NotifyPropertyChanged("PresionMaxima");
            NotifyPropertyChanged("DuracionOclusion");
            NotifyPropertyChanged("DatosOclusion");
        }
        /// <summary>
        /// Añade un nuevo dato de medida de Oclusion.
        /// </summary>
        /// <param name="tiempo">Lapso de tiempo en milisegundos desde el inicio de la prueba hasta la toma de la medida.</param>
        /// <param name="presion">Valor de presion en mmHg</param>
        public void AddDatoOclusion(double tiempo, int presion)
        {
            if (datosOclusion.Count == 0) inicioOclusion = DateTime.Now;
            datosOclusion.Add(new DatoOclusion(tiempo, presion));
            DuracionOclusion = TimeSpan.FromMilliseconds(tiempo);
            PresionMaxima = presion;
            NotifyPropertyChanged("PresionMaxima");
            NotifyPropertyChanged("DuracionOclusion");
            NotifyPropertyChanged("DatosOclusion");
        }

        public void AddDatoVolumen(DatoVolumen dato)
        {
            DatoVolumen datoAnterior = new DatoVolumen();

            if (datosVolumen.Count() > 0) datoAnterior = datosVolumen.Last();
            else inicioFlujo = DateTime.Now;

            dato.FlujoInstant = (dato.Volumen - datoAnterior.Volumen) /
                              (dato.Tiempo.TotalHours - datoAnterior.Tiempo.TotalHours);

            datosVolumen.Add(dato);
        }
        /// <summary>
        /// Añade un nuevo dato de medida de Volumen.
        /// </summary>
        /// <param name="tiempo">Lapso de tiempo en milisegundos desde el inicio de la prueba hasta la toma de la medida.</param>
        /// <param name="volumen">Volumen medido en ml (total desde que se inicio la prueba hasta la medida)</param>
        public void AddDatoVolumen(double tiempo, double volumen)
        {
            DatoVolumen datoAnterior = new DatoVolumen();
            DatoVolumen dato = new DatoVolumen(tiempo, volumen);   //Tiempo de milisegundos a horas

            if (datosVolumen.Count() > 0) datoAnterior = datosVolumen.Last();
            else inicioFlujo = DateTime.Now;

            dato.FlujoInstant = (dato.Volumen - datoAnterior.Volumen) /
                           (dato.Tiempo.TotalHours - datoAnterior.Tiempo.TotalHours);

            datosVolumen.Add(dato);
        }

        internal void OclusionFinalizada(bool ok)
        {
            if (!ok)
            {
                datosOclusion.Clear();
                inicioOclusion = DateTime.MinValue;
                DuracionOclusion = TimeSpan.Zero;
                PresionMaxima = 0;
                HayOclusion = false;
                return;
            }

            HayOclusion = true;
            DuracionOclusion = datosOclusion.Last().Tiempo;
        }

    }



}
