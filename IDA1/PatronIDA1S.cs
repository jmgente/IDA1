﻿using FTD2XX_NET;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;


namespace IDA1
{
    public class PatronIDA1S
    { 

    #region Parametros de configuracion del Puerto COM
        const int baudRate = 115200;
        const Parity paridad = Parity.None;
        const int bitsDatos = 8;
        const StopBits bitsParada = StopBits.One;
        const string nuevaLinea = "\r\n";
        #endregion

        public enum IDA_RESULT
        {
            IDA_OK,
            IDA_NOT_FOUND,
            IDA_SIN_RESPUESTA,
            IDA_FALLO_PATRON,
            IDA_COMANDO_DESCONOCIDO,
            IDA_LECTURA_ERRONEA,
            IDA_RESPUESTA_ERRONEA,
        }
        public enum EstadoIDA
        {
            SIN_CONEXION,   //No se detecto patron o no responde
            SIN_RESPUESTA,  //El patron no respondio a una peticion.
            CONECTADO,      //Patron detectado y en comunicaion. Timer de lectura activo
            PRE_FLUJO,      //Preparado para prueba flujo sin inicair
            FLUJO,          //Realizando prueba de flujo
            PRE_OCLUSION,  //Preparado para prueba oclusion sin iniciar
            OCLUSION,       //Realizando prueba de oclusion
            BUSCANDO_IDA,
        }

    #region Eventos Implementacion
        public class MedidaRecibidaEventArgs : EventArgs
        {
            public bool hayAire;
            public EstadoIDA estado;
            /// <summary>
            /// Tiempo transcurrido desde el inicio de la prueba en milisegundos.
            /// </summary>
            public double tiempo;
            /// <summary>
            /// Presión actual en mmHg. 
            /// </summary>
            public short presion;
            /// <summary>
            /// Volumen medido desde el inicio de la prueba en ml.
            /// </summary>
            public double volumen;

            public MedidaRecibidaEventArgs()
            {
                hayAire = false;
                estado = EstadoIDA.SIN_CONEXION;
                tiempo = -1;
                presion = -1;
                volumen = -1;
            }
        }

        public event EventHandler<MedidaRecibidaEventArgs> MedidaRecibida;

        protected virtual void OnMedidaRecibida(MedidaRecibidaEventArgs e)
        {
            Form llamador = MedidaRecibida.Target as Form;
            if (llamador.InvokeRequired)
            {
                llamador.BeginInvoke(MedidaRecibida, new object[] { this, e });
            }
            else MedidaRecibida?.Invoke(this, e);

        }

    #endregion

    #region Definicion de Campos
        //Campos para el control de la comunicacion
        SerialPort puerto;
        private EstadoIDA estado = EstadoIDA.SIN_CONEXION;

        private Form1 parent;
        private string marca;
        private string modelo;
        private string nSerie;
        private string identificador;
        private string nSerieCanal;
        private string nCanales;
        private string portName;    //nombre delpuerto

        #endregion
    #region Deficinion de Propiedades
        public EstadoIDA Estado
        {
            get { return estado; }
            set { estado = value; }
        }
        public string Marca { get { return marca; } }
        public string Modelo { get { return modelo; } }
        public string NSerie { get { return nSerie; } }
        public string Identificador { get { return identificador; } }
        public string NSerieCanal { get { return nSerieCanal; } }
        public bool HayAire { get; private set; }

        #endregion

        /// <summary>
        /// Constructor de patronIDA1S
        /// </summary>
        /// <param name="port">Puerto COM al que esta conectado</param>
        public PatronIDA1S( Form1 llamador )
        {
           this.parent = llamador;
        }

        /// <summary>
        /// Destructor
        /// <para>Debe asegurarse de que el puerto COM esta cerrado y de liberar el Timer</para>
        /// </summary>
        ~PatronIDA1S()
        {
            DesconectaPatron();
            if (puerto != null) puerto.Dispose();
        }

        /// <summary>
        /// Comprueba si hay un IDA1S conectado al puerto especificado.
        /// Tiene un retardo de 1 segundo en caso de no haber respueta del puerto COM
        /// </summary>
        /// <param name="com">Puerto que se comprobara</param>
        /// <returns>Devuelve la cadena de identificacion del patron o string.Empty si no hay un IDA1S</returns>
        public static string IsIDA1SinCOM(string com)
        {
            Debug.WriteLine("Inicio IsIDA1SinCOM en " + com);

            SerialPort pb;
            string read;

            try
            {
                pb = new SerialPort(com, baudRate, paridad, bitsDatos, bitsParada);
                pb.NewLine = nuevaLinea;
                pb.ReadTimeout = 1000;
                pb.Open();
                Debug.WriteLine("IsIDA1SinCOM: Puerto abierto");

            }
            catch (System.IO.IOException) { Debug.WriteLine("IsIDA1SinCOM: IOException en " + com); return string.Empty; }

            try
            {
                Debug.WriteLine("IsIDA1SinCOM: GETPARAMS");
                pb.WriteLine("[GETPARAMS, 0]");
                read = pb.ReadLine();
                Debug.WriteLine(" - " + read);

                if (read != "[P1,FLUKE BIOMEDICAL]") return string.Empty;
                read = pb.ReadLine();
                if (read != "[P2,IDA1S]") return string.Empty;
                read = pb.ReadLine();
                read = pb.ReadLine();
                read = pb.ReadLine();
                pb.WriteLine("[BYE]");
                return read.Substring(4, read.Count() - 5);
            }
            catch (System.TimeoutException) { Debug.WriteLine("TimeOut.."); return string.Empty; }
            finally { pb.Close(); pb.Dispose(); Debug.WriteLine("IsIDA1SinCOM: Puerto Closed"); }
        }

        /// <summary>
        /// Busca todos los FTDI disponibles conectados al PC y devuelve sus puertos COM correspondientes.
        /// </summary>
        /// <returns>Array de strings con los puertos COM con la forma "COM<para>n</para>".</returns>
        private string[] BuscaFTDIsDisponibles()
        {
            FTDI miFTDI = new FTDI();
            List<string> COMs = new List<string>();
            string resultado;
            FTDI.FT_STATUS stat;
            uint nDevices = 0;

            stat = miFTDI.GetNumberOfDevices(ref nDevices);
            if (stat != FTDI.FT_STATUS.FT_OK)
            {
                Debug.WriteLine("Fallo al cojer numero de FTDI");
                return new string[0];
            }

            if (nDevices == 0) return new string[0];   //No hay ningun parton conectado

            FTDI.FT_DEVICE_INFO_NODE[] devices = new FTDI.FT_DEVICE_INFO_NODE[nDevices];
            stat = miFTDI.GetDeviceList(devices);
            if (stat != FTDI.FT_STATUS.FT_OK)
            {
                Debug.WriteLine("Fallo al cojer lista de FTDIs");
                return new string[0];
            }

            for (uint i = 0; i < nDevices; i++)
            {
                stat = miFTDI.OpenByIndex(i);
                if (stat != FTDI.FT_STATUS.FT_OK)
                {
                    Debug.WriteLine("Fallo al abrir el FTDI[{0}]", i);
                    continue;
                }

                stat = miFTDI.GetCOMPort(out resultado);
                if (stat != FTDI.FT_STATUS.FT_OK)
                {
                    Debug.WriteLine("Fallo al cojer el COM del FTDI[{0}]", i);
                    miFTDI.Close();
                    continue;
                }

                if (resultado != string.Empty) COMs.Add(resultado);
                miFTDI.Close();
            }

            return COMs.ToArray();
        }

        /// <summary>
        /// Se conecta con el primer IDA1S que se encuentre conectado al PC.
        /// <para>Este metodo esta pensado para ejecutarse dentro del evento 
        /// <see cref="DoWorkEventHandler">DoWork</see> DoWork de un <see cref="BackgroundWorker"/></para>
        /// </summary>
        /// <param name="worker">Recibe el <see cref="BackgroundWorker"/> en el que se esta ejecutando</param>
        /// <returns><see cref="IDA_RESULT"/> con el resultado.</returns>
        public IDA_RESULT ConectaConIDA1S( BackgroundWorker worker )
        {
            IDA_RESULT resultado;

            string[] puertos = BuscaFTDIsDisponibles();
            for ( int i = 0 ; i < puertos.Length ; i++)
            {
                worker.ReportProgress((100 / puertos.Length) * i ); //Informamos del porcentage de progreo

                if (IsIDA1SinCOM(puertos[i]) != string.Empty)
                {
                    portName = puertos[i];
                    resultado = InicializaIDA1S();
                    if (resultado == IDA_RESULT.IDA_OK)
                    {
                        Estado = EstadoIDA.CONECTADO;
                        return IDA_RESULT.IDA_OK;
                    }
                }
            }

            worker.ReportProgress(100); //Informamos del ultimo porcentage de progreo
            Thread.Sleep(500);          // Esperamos un poco para que se aprecie el incremento de la barra de progreso
            Estado = EstadoIDA.SIN_CONEXION;
            return IDA_RESULT.IDA_NOT_FOUND;
        }

    #region COMANDOS PARA CONTROLAR EL IDA1S

        /// <summary>
        /// Inicializa el patron: Abre el puerto, coje datos del patron e inicializa 
        /// la comunicacion.
        /// <para>Posibles valores devueltos:
        ///     <see cref="IDA_RESULT.IDA_OK"/> 
        ///     <see cref="IDA_RESULT.IDA_SIN_RESPUESTA"/>
        ///     <see cref="IDA_RESULT.IDA_FALLO_PATRON"/>    
        /// </para>
        /// </summary>
        /// <returns>Posibles valores devueltos:
        ///     <see cref="IDA_RESULT.IDA_OK"/>
        ///     <see cref="IDA_RESULT.IDA_SIN_RESPUESTA"/>
        ///     <see cref="IDA_RESULT.IDA_FALLO_PATRON"/>    
        /// </returns>
        private IDA_RESULT InicializaIDA1S()
        {
            IDA_RESULT resultado;

            AbrePuerto();       //Abre el puerto
            resultado = CojeDatosPatron();
            if ( resultado != IDA_RESULT.IDA_OK) return resultado;

            DesconectaPatron(); //Desconecta del patron y cierra puerto (= Hydrograp)

            resultado = ConectaPatron();    //Establece la comunicacioncon el patron. Activa timer sondeo
            if (resultado != IDA_RESULT.IDA_OK) return resultado;

            return IDA_RESULT.IDA_OK;
        }

        /// <summary>
        /// CallBack del timer temporizador para que se comprueb el 
        /// estado del patron cada segundo mientras la conexion este activa.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public IDA_RESULT ControlStatus( )
        {
            string resp;

            try
            {
                puerto.WriteLine("[STATUS]");
                resp = ReadLine();
                if (resp.StartsWith("[STAT,"))
                {
                    if( resp.Length < 18 )
                    {
                        return IDA_RESULT.IDA_FALLO_PATRON;
                    }

                    switch (resp[6])
                    {
                        case ' ':   //En STANDBY
                        case 'O':   //Oclusion terminada. Estado STANDBY.¿Oclusion en memoria?
                        case 'V':   //Flujo terminado. Estado STANDBY. ¿Flujo en memoria?
                            Estado = EstadoIDA.CONECTADO;
                            break;
                        case 'T':   //Preparando para oclusion
                            Estado = EstadoIDA.PRE_OCLUSION;
                            break;
                        case 'o':   //Realizando oclusion
                            Estado = EstadoIDA.OCLUSION;
                            break;
                        case 'F':   //Preparando para flujo
                            Estado = EstadoIDA.PRE_FLUJO;
                            break;
                        case 'v':   //Realizando prueba de flujo
                            Estado = EstadoIDA.FLUJO;
                            break;
                        default:
                            return IDA_RESULT.IDA_FALLO_PATRON;
                    }

                    switch (resp[10])
                    {
                        case 'O':   //Realizando oclusion
                            Estado = EstadoIDA.OCLUSION;
                            break;
                        case 'F':   //Realizando prueba de flujo
                            Estado = EstadoIDA.FLUJO;
                            break;
                        case ' ':   //En STANDBY
                            Estado = EstadoIDA.CONECTADO;
                            break;
                        default:
                            return IDA_RESULT.IDA_FALLO_PATRON;
                    }

                    if (resp[14] == 'a' || resp[14] == 'b' || resp[14] == 'f') HayAire = true;
                    else HayAire = false;

                }
                else  return IDA_RESULT.IDA_FALLO_PATRON;

                puerto.WriteLine("[GETBATSTATUS]");
                resp = ReadLine();
                if (resp.StartsWith("[BATSTATUS,"))
                {
                    // No nos interesa controlar el estado de la bateria de momento
                }
                else return IDA_RESULT.IDA_FALLO_PATRON;

                return IDA_RESULT.IDA_OK;
            }
            catch (TimeoutException)   { return IDA_RESULT.IDA_SIN_RESPUESTA;  }
        }

        /// <summary>
        /// Procesa la string devuelta por el patron con los datos de la medida.
        /// <para>(Hexadecimal, byte bajo menor peso).
        /// 8 primeros carateres son el tiempo transcurrido en milisegundos,
        /// 8 siguientes son el volumen en ul,
        /// 4 ultimos son la presion en mlHg
        /// </para>
        /// </summary>
        /// <param name="datos"></param>
        /// <returns></returns>
        private MedidaRecibidaEventArgs ProcesaDatos(string datos)
        {
            MedidaRecibidaEventArgs medida = new MedidaRecibidaEventArgs();

            medida.tiempo = Int64.Parse( datos.Substring(2, 8), NumberStyles.AllowHexSpecifier);
            medida.volumen = Int64.Parse(datos.Substring(10, 8), NumberStyles.AllowHexSpecifier) / 1000; // El valor devuelto por el patron es en ul. Lo convertimos en ml.
            medida.presion = short.Parse(datos.Substring(18, 4), NumberStyles.AllowHexSpecifier);
            medida.estado = Estado;
            medida.hayAire = (datos[1] == 'a' || datos[1] == 'b') ? true : false;
            return medida;
        }

        /// <summary>
        /// Recupera la informacion basica del patron.
        /// </summary>
        /// <returns>Posibles valores devueltos 
        ///     <see cref="IDA_RESULT.IDA_OK"/>
        ///     <see cref="IDA_RESULT.IDA_SIN_RESPUESTA"/>
        ///     <see cref="IDA_RESULT.IDA_FALLO_PATRON"/>    
        /// </returns>
        IDA_RESULT CojeDatosPatron()
        {
            string resp;

            try
            {
                puerto.WriteLine("[GETPARAMS, 0]");
                resp = ReadLine();   //[P1, Fabricante
                if(resp.Length < 4) return IDA_RESULT.IDA_FALLO_PATRON;
                if (resp.Substring(0, 4) != "[P1,") return IDA_RESULT.IDA_FALLO_PATRON;
                marca = resp.Substring(4, resp.Length - 5);

                resp = ReadLine();  //[P2, Modelo
                if (resp.Length < 4) return IDA_RESULT.IDA_FALLO_PATRON;
                if (resp.Substring(0, 4) != "[P2,") return IDA_RESULT.IDA_FALLO_PATRON;
                modelo = resp.Substring(4, resp.Length - 5);

                resp = ReadLine();  //[P3, Descripcion del patron
                resp = ReadLine();  //[P4, ??

                resp = ReadLine();  //[P5, Identificador: Modelo + N/S
                if (resp.Length < 4) return IDA_RESULT.IDA_FALLO_PATRON;
                if (resp.Substring(0, 4) != "[P5,") return IDA_RESULT.IDA_FALLO_PATRON;
                identificador = resp.Substring(4, resp.Length - 5);

                puerto.WriteLine("[GETSN,0]");   //Cojemos el numero de serie
                resp = ReadLine();   //[SN0, Numero de serie del patron.
                if (resp.Length < 5) return IDA_RESULT.IDA_FALLO_PATRON;
                if (resp.Substring(0, 5) != "[SN0,") return IDA_RESULT.IDA_FALLO_PATRON;
                nSerie = resp.Split(',')[1];

                puerto.WriteLine("[POLL]");   //Cojemos el numero de canales
                resp = ReadLine();   //No nos interesa el dato ya que trabajamos solo con el 1º canal
                nCanales = resp;

                puerto.WriteLine("[GETSN,1]");   //Cojemos el numero de serie del canal 1
                resp = ReadLine();    //[SN1,  Numero de serie del canal 1
                if (resp.Length < 5) return IDA_RESULT.IDA_FALLO_PATRON;
                if (resp.Substring(0, 5) != "[SN1,") return IDA_RESULT.IDA_FALLO_PATRON;
                nSerieCanal = resp.Split(',')[1];
            }
            catch (System.TimeoutException)
            {
                resp = puerto.ReadExisting();
                Debug.WriteLine("TimeOut..." + resp);
                Estado = EstadoIDA.SIN_RESPUESTA;
                return IDA_RESULT.IDA_SIN_RESPUESTA;
            }
            return IDA_RESULT.IDA_OK;
        }

        /// <summary>
        /// Prepara el patron para la prueba de oclusion.
        /// <para>Devuelve true si salio el patron esta listo</para>
        /// </summary>
        /// <returns></returns>
        public IDA_RESULT PreparaOclusion()
        {
            try
            {
                puerto.WriteLine("[C1O,numero control, operador, 10000]");
                if (ReadLine() == "[OK]")
                {
                    Estado = EstadoIDA.PRE_OCLUSION;
                    return IDA_RESULT.IDA_OK;
                }

                return IDA_RESULT.IDA_FALLO_PATRON;
            }
            catch (TimeoutException)  { return IDA_RESULT.IDA_SIN_RESPUESTA; }
        }

        /// <summary>
        /// Inicia la prueba de oclusion en el patron.
        /// <para>Devuelve false si falla</para>
        /// </summary>
        /// <returns></returns>
        public IDA_RESULT IniciaOclusion()
        {
            try
            {
                puerto.WriteLine("[C1O,numero control, operador, 10000]");
                if (ReadLine() == "[OK]")
                {
                    Estado = EstadoIDA.OCLUSION;
                    return IDA_RESULT.IDA_OK;
                }

                return IDA_RESULT.IDA_FALLO_PATRON;

            }
            catch (TimeoutException)  { return IDA_RESULT.IDA_SIN_RESPUESTA; }
        }

        /// <summary>
        /// Finaliza la prueba de oclusion del patron.
        /// </summary>
        /// <returns></returns>
        public IDA_RESULT FinalizaOclusion()
        {
            string resp;

            try
            {
                puerto.WriteLine("[END,1]");
                resp = ReadLine();

                if (resp == "[OK]")
                {
                    Estado = EstadoIDA.CONECTADO;
                    return IDA_RESULT.IDA_OK;
                }

                return IDA_RESULT.IDA_FALLO_PATRON;
            }
            catch (TimeoutException) { return IDA_RESULT.IDA_SIN_RESPUESTA; }
        }

        /// <summary>
        /// Prepara el patron para la prueba de flujo.
        /// <para>Devuelve true si salio el patron esta listo</para>
        /// </summary>
        /// <returns></returns>
        internal IDA_RESULT PreparaFlujo()
        {
            try
            {
                puerto.WriteLine("[C1F,control,operador, 1000]");
                if (ReadLine() == "[OK]")
                {
                    Estado = EstadoIDA.FLUJO;
                    return IDA_RESULT.IDA_OK;
                }

                return IDA_RESULT.IDA_FALLO_PATRON;
            }
            catch (TimeoutException) { return IDA_RESULT.IDA_SIN_RESPUESTA; }
        }

        /// <summary>
        /// Inicia la prueba de flujo en el patron.
        /// </summary>
        /// <returns></returns>
        internal IDA_RESULT IniciaFlujo()
        {
            try
            {
                puerto.WriteLine("[C1FA,control,operador, 1000]");
                if (ReadLine() == "[OK]")
                {
                    Estado = EstadoIDA.PRE_FLUJO;
                    return IDA_RESULT.IDA_OK;
                }

                return IDA_RESULT.IDA_FALLO_PATRON;
            }
            catch (TimeoutException) { return IDA_RESULT.IDA_SIN_RESPUESTA; }
        }

        /// <summary>
        /// Finaliza la prueba de flujo del patron.
        /// </summary>
        /// <returns></returns>
        public IDA_RESULT FinalizaFlujo()
        {
            List<string> comandos = new List<string>();
            IDA_RESULT resp;

            try
            {
                puerto.WriteLine("[END,1]");
                resp = ReadLine(ref comandos);

                if( comandos.Count > 1 || )
                if (resp == "[OK]")
                {
                    Estado = EstadoIDA.CONECTADO;
                    return IDA_RESULT.IDA_OK;
                }

                return IDA_RESULT.IDA_FALLO_PATRON;
            }
            catch (TimeoutException) { return IDA_RESULT.IDA_SIN_RESPUESTA; }
        }

        /// <summary>
        /// Inicializa el <see cref="puerto"/> si no esta inicializado.
        /// <para>Si no esta abierto, configura los parametros para el IDA y abre el puerto.</para>
        /// </summary>
        /// <exception cref="System.IO.IOException">Fallo general de comunicacion.</exception>
        /// <exception cref="UnauthorizedAccessException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        private void AbrePuerto()
        {
            if (puerto == null) puerto = new SerialPort(portName, baudRate,
                                                        paridad, bitsDatos, bitsParada);

            if (!puerto.IsOpen)
            {
                puerto.NewLine = nuevaLinea;
                puerto.ReadTimeout = 1000;
                puerto.Open();
            }
        }

        /// <summary>
        /// Prepara el puerto COM y establece la comunicacion con el patron e 
        /// inicializa el <see cref="temporizador"/> de control de estado.
        /// </summary>
        /// <returns>Posibles valores devueltos 
        ///     <see cref="IDA_RESULT.IDA_OK"/>
        ///     <see cref="IDA_RESULT.IDA_SIN_RESPUESTA"/>
        ///     <see cref="IDA_RESULT.IDA_FALLO_PATRON"/>    
        /// </returns>
        private IDA_RESULT ConectaPatron()
        {
            string respuesta;

            AbrePuerto();

            try
            {
                puerto.WriteLine("[LOG]");   //Inicia la conexion
                respuesta = puerto.ReadLine();
            }
            catch (System.TimeoutException)
            {
                respuesta = puerto.ReadExisting();
                Debug.WriteLine("ConectaPatron: TimeOut..." + respuesta);
                Estado = EstadoIDA.SIN_RESPUESTA;
                return IDA_RESULT.IDA_SIN_RESPUESTA;
            }

            if (respuesta != "[LOG,1,0,0,0]")
            {
                Debug.WriteLine("ConectaPatron: Respuesta recibida erronea -> " + respuesta);
                return IDA_RESULT.IDA_RESPUESTA_ERRONEA;
            }

            //Iniciada conexion correctamente
            Estado = EstadoIDA.CONECTADO;
            return IDA_RESULT.IDA_OK;
        }

        /// <summary>
        /// Se encarga de desconectarse del patron de forma apropiada y cerrar el puerto.
        /// </summary>
        public void DesconectaPatron()
        {

            if (Estado != EstadoIDA.SIN_CONEXION)
            {
                if (puerto?.IsOpen ?? false)  //Si 'puerto' no es NULL y esta abierto
                {
                    if (Estado == EstadoIDA.FLUJO ||
                        Estado == EstadoIDA.OCLUSION ||
                        Estado == EstadoIDA.PRE_FLUJO ||
                        Estado == EstadoIDA.PRE_OCLUSION)
                    {
                        puerto.WriteLine("[END,1]");
                        try { List<string> respuesta = null; ReadLine(ref respuesta); } catch(TimeoutException) { }
                    }

                    puerto.WriteLine("[BYE]");
                    puerto.Close();
                }

                Estado = EstadoIDA.SIN_CONEXION;
            }
        }

        #endregion

        /// <summary>
        /// Lee los datos recibidos del patron. Si es una medida, la procesa y proboca el evento <see cref="MedidaRecibida"/>
        /// <para>Devuleve <see cref="IDA_RESULT.IDA_OK"/> o <see cref="IDA_RESULT.IDA_LECTURA_ERRONEA"/></para>
        /// </summary>
        /// <param name="comandos"> Referencia de un List<string> donde se guardaran los camandos recibidos.</param>
        /// <returns><see cref="IDA_RESULT.IDA_OK"/> o <see cref="IDA_RESULT.IDA_LECTURA_ERRONEA"/></returns>
        private IDA_RESULT ReadLine(ref List<string> comandos)
        {
            string resp;

            do
            {
                resp = puerto.ReadLine();
                while (resp[0] == '1')
                {
                    if(resp.Length != 22)
                    {
                        Debug.WriteLine("Lectura de medida erronea -> " + resp);
                        return IDA_RESULT.IDA_LECTURA_ERRONEA;
                    }

                    OnMedidaRecibida(ProcesaDatos(resp));
                    resp = puerto.ReadLine();
                }

                if (resp[0] == '[') comandos.Add(resp);
                else
                {
                    Debug.WriteLine("Lectura erronea -> " + resp);
                    return IDA_RESULT.IDA_LECTURA_ERRONEA;
                }

            } while (puerto.BytesToRead > 0);

            return IDA_RESULT.IDA_OK;
        }
    }
}
