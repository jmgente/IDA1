using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;


namespace IDA1
{   
     
    class Prueba : INotifyPropertyChanged
    {
        #region Constantes de configuracion de la prueba
        /// <summary>
        /// Limite de tiempo para la oclusion en milisegundos.
        /// </summary>
        public const double LIMITE_TIEMPO_OCLUSION = 120000;
        /// <summary>
        /// Limite de volumen al que se detendra la prueba de flujo. En ul
        /// </summary>
        public const double LIMITE_VOLUMEN_FLUJO = 3000;
        /// <summary>
        /// Flujo medio al que se realiza la prueba en ml/h.
        /// </summary>
        public const double FLUJO_MEDIO = 10;
        /// <summary>
        /// Porcentage de error permitido.
        /// </summary>
        public const double FLUJO_PORCENTAGE_ERROR = 13;
        /// <summary>
        /// El limite absoluto de error superior.
        /// </summary>
        public const double FLUJO_LIMITE_UP = FLUJO_MEDIO + (FLUJO_MEDIO * FLUJO_PORCENTAGE_ERROR / 100);
        /// <summary>
        /// El limite absoluto de error inferior.
        /// </summary>
        public const double FLUJO_LIMITE_DOWN = FLUJO_MEDIO - (FLUJO_MEDIO * FLUJO_PORCENTAGE_ERROR / 100);
        #endregion

        /// <summary>
        /// Contiene los datos de una medida de oclusion.
        /// </summary>
        public class DatoOclusion : INotifyPropertyChanged
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
        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
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
        private bool esAptaOclusion;
        private bool esAptoFlujo;
        private bool hayOclusion;
        private bool hayFlujo;
        #endregion

        #region Propiedades declaracion
        //Datos de la oclusion
        public bool HayOclusion
        {
            get { return hayOclusion; }
            set { hayOclusion = value; NotifyPropertyChanged(); }
        }
        public DateTime InicioOclusion { get { return inicioOclusion; } }
        public List<DatoOclusion> DatosOclusion { get { return datosOclusion; } }
        public TimeSpan DuracionOclusion { get; private set; }
        public int PresionMaxima { get; private set; }
        public bool EsAptaOclusion
        {
            get { return esAptaOclusion; }
            set { esAptaOclusion = value; NotifyPropertyChanged("TextAptoOclu"); }
        }
        public string TextAptoOclu { get { return (EsAptaOclusion) ? "Apto" : "No Apto"; } }
        //Datos del flujo
        public bool HayFlujo
        {
            get { return hayFlujo; }
            set { hayFlujo = value; NotifyPropertyChanged(); }
        }
        public DateTime InicioFlujo { get { return inicioFlujo; } }
        public List<DatoVolumen> DatosFlujo { get { return datosVolumen; } }
        public bool EsAptoFlujo
        {
            get { return esAptoFlujo; }
            set { esAptoFlujo = value; NotifyPropertyChanged("TextAptoFlujo"); }
        }
        public TimeSpan DuracionFlujo { get; private set; }
        public double FlujoMedio { get; private set; }
        public double MaxFlujoInst { get; private set; }
        public double MinFlujoInst { get; private set; }
        public string TextAptoFlujo => (EsAptoFlujo) ? "Apto" : "No Apto";
        //Datos generales de la prueba
        public DateTime InicioPrueba => (inicioFlujo < inicioOclusion) ? inicioFlujo : inicioOclusion;



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
            if (HayOclusion) return;   //Si ya ha terminado la prueba no añaede mas datos

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
            if (HayOclusion) return;   //Si ya ha terminado la prueba no añaede mas datos

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
            if (HayFlujo) return;   //Si ya ha terminado la prueba no añaede mas datos

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
            if (HayFlujo) return;   //Si ya ha terminado la prueba no añaede mas datos

            DatoVolumen datoAnterior = new DatoVolumen();
            DatoVolumen dato = new DatoVolumen(tiempo, volumen);   //Tiempo de milisegundos a horas

            if (datosVolumen.Count() > 0) datoAnterior = datosVolumen.Last();
            else inicioFlujo = DateTime.Now;

            dato.FlujoInstant = (dato.Volumen - datoAnterior.Volumen) /
                           (dato.Tiempo.TotalHours - datoAnterior.Tiempo.TotalHours);

            datosVolumen.Add(dato);
        }

        /// <summary>
        /// Da por finalizada la prueba de oclusion y realiza los calculos con los datos recogidos.
        /// <para>Devuleve <see cref="true"/> si la prueba es APTA. <see cref="false"/> si NO APTA </para>
        /// </summary>
        internal bool OclusionFinalizada()
        {
            HayOclusion = true;
            DuracionOclusion = datosOclusion.Last().Tiempo;
            PresionMaxima = DatosOclusion.Max( d => d.Presion );
            EsAptaOclusion = (DuracionOclusion.TotalMilliseconds <= LIMITE_TIEMPO_OCLUSION) ? true : false;
            return EsAptaOclusion;
        }

        internal void BorraOclusion()
        {
            datosOclusion.Clear();
            inicioOclusion = DateTime.MinValue;
            DuracionOclusion = TimeSpan.Zero;
            PresionMaxima = 0;
            HayOclusion = false;
            EsAptaOclusion = false;
            return;
        }

        /// <summary>
        /// Da por finalizada la prueba de flujo y realiza los calculos con los datos recogidos.
        /// <para>Devuleve <see cref="true"/> si la prueba es APTA. <see cref="false"/> si NO APTA </para>
        /// </summary>
        internal bool FlujoFinalizado()
        {
            HayFlujo = true;
            DuracionFlujo = DatosFlujo.Last().Tiempo;
            FlujoMedio = DatosFlujo.Last().Volumen / DatosFlujo.Last().Tiempo.TotalHours;
            EsAptoFlujo = ( (FlujoMedio > FLUJO_LIMITE_DOWN) && (FlujoMedio < FLUJO_LIMITE_UP) ) ? true : false;
            MaxFlujoInst = DatosFlujo.Max(d => d.FlujoInstant);
            MinFlujoInst = DatosFlujo.Min(d => d.FlujoInstant);
            return EsAptoFlujo;
        }

        internal void BorraFlujo()
        {
            HayFlujo = false;
            DuracionFlujo = TimeSpan.Zero;
            inicioFlujo = DateTime.MinValue;
            FlujoMedio = 0;
            EsAptoFlujo = false;
            MaxFlujoInst = 0;
            MinFlujoInst = 0;
        }
    }



}
