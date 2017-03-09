using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using FTD2XX_NET;
using System.Threading;

namespace IDA1
{
    public partial class Form1 : Form
    {
        #region Constantes de configuracion de la prueba
        const double LIMITE_TIEMPO_OCLUSION = 2;

        #endregion

        enum ComandosPatron
        {
            PREPARA_OCLUSION,
            INICIA_OCLUSION,
            PREPARA_FLUJO,
            INICIA_FLUJO,
            DESCONECTA,
            CONECTA,
            FINALIZA_OCLUSION,
            FINALIZA_FLUJO,
        }

        PatronIDA1S patron;
        Prueba prueba;
        private bool checkStatusActivo = false;
        private bool detenCheckStatus = false;

        public Form1()
        {
            InitializeComponent();
            prueba = new Prueba(this);
            patron = new PatronIDA1S(this);
            patron.MedidaRecibida += ProcesaMedida;

            lblTiempoOc.DataBindings.Add("Text", prueba, "DuracionOclusion");
            lblPresion.DataBindings.Add("Text", prueba, "PresionMaxima");
            dataGridViewDepura.DataSource = prueba.DatosOclusion;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        private void btnBuscarPatron_Click(object sender, EventArgs e)
        {
            if (btnBuscarPatron.Text == "Desconectar")
            {
                PatronDesconectado();
            }
            else
            {
                btnBuscarPatron.Enabled = false;
                progressBar1.Visible = true;
                lblPatron.Text = "Buscando IDA1S";
                lblPatron.ForeColor = System.Drawing.SystemColors.ControlText;
                backgroundWorkerPat.RunWorkerAsync();
            }
        }        

        private void btnOclusion_Click(object sender, EventArgs e)
        {
            PatronIDA1S.IDA_RESULT resultado;

            resultado = EnviaComandoPatron(ComandosPatron.PREPARA_OCLUSION);
            if ( resultado != PatronIDA1S.IDA_RESULT.IDA_OK)
            {
                MessageBox.Show("Fallo en el inicio del patron", "Error en el patron");
                PatronDesconectado();
                return;
            }

            MessageBox.Show("* Conecta la bomba al patron.\r\n" + 
                   "* Asegurate de que la linea esta purgada y no tiene aire.\r\n" +
                   "* Configura el flujo de la bomba a 100ml/h.\r\n\r\n" +
                   " Cuando este listo pon la bomba en marcha y pulsa Aceptar.", 
                   "Prueba de oclusión");

            resultado = EnviaComandoPatron(ComandosPatron.INICIA_OCLUSION);
            if (resultado != PatronIDA1S.IDA_RESULT.IDA_OK)
            {
                MessageBox.Show("Fallo en el inicio del patron", "Error en el patron");
                PatronDesconectado();
                return;
            }

            btnOclusion.Enabled = false;
            btnStopOclu.Enabled = true;
            btnFlujo.Enabled = false;
        }
        
        /// <summary>
        /// Se desconecta del patron de fprma apropiada e inicializa los controles del Form al modo 'sin patron conectado'.
        /// </summary>
        private void PatronDesconectado()
        {
            lblPatron.Text = "No hay patron conectado";
            lblPatron.ForeColor = Color.Red;
            btnBuscarPatron.Text = "Buscar";
            btnBuscarPatron.Enabled = true;
            btnOclusion.Enabled = false;
            btnStopOclu.Enabled = false;
            btnFlujo.Enabled = false;
            btnStopFlow.Enabled = false;

            if (backgroundWorkerChekPat.IsBusy)    //Si esta activado el subproceso 
            {
                backgroundWorkerChekPat.CancelAsync();   //Lo desactiva
                while (backgroundWorkerChekPat.IsBusy)  //esperamos a que se desactive antes de continuar
                {
                    Application.DoEvents();
                }

            }

            EnviaComandoPatron(ComandosPatron.DESCONECTA);
        }

        /// <summary>
        /// Inicializa los controles del Form al estado 'patron conectado'
        /// </summary>
        private void PatronConectado()
        {
            lblPatron.Text = "Conectado a " + patron.Identificador;
            lblPatron.ForeColor = Color.Green;
            btnBuscarPatron.Text = "Desconectar";
            btnBuscarPatron.Enabled = true;
            btnOclusion.Enabled = true;
            btnStopOclu.Enabled = false;
            btnFlujo.Enabled = true;
            btnStopFlow.Enabled = false;

            if (!backgroundWorkerChekPat.IsBusy)     //Inicia el chequeo asincrono del status de patron.
                backgroundWorkerChekPat.RunWorkerAsync(); 
        }

        private void btnStopOclu_Click(object sender, EventArgs e)
        {
            PatronIDA1S.IDA_RESULT result;

            if(patron.Estado == PatronIDA1S.EstadoIDA.OCLUSION)
            {
                result = EnviaComandoPatron(ComandosPatron.FINALIZA_OCLUSION);
                if (result != PatronIDA1S.IDA_RESULT.IDA_OK) MessageBox.Show("Fallo al finalizar la oclusion");
                if (prueba.DatosOclusion.Last().Tiempo.TotalMinutes > LIMITE_TIEMPO_OCLUSION)
                {
                    DialogResult resultado = MessageBox.Show("La prueba de oclusion dio NO APTO.\r\n" +
                                        "¿Quieres guardarla?", "¡¡¡ NO APTO !!!",
                                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (resultado == DialogResult.No)
                    {
                        prueba.OclusionFinalizada(false);
                        PatronConectado();
                    }
                }
                else
                {
                    prueba.OclusionFinalizada(true);
                    btnStopOclu.Enabled = false;
                    btnOclusion.Enabled = false;
                    btnFlujo.Enabled = true;
                }
            }
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            btnBuscarPatron_Click(null, new EventArgs());
            
        }

        #region BackgroundWorkerPAt Se encarga del proceso de busqueda y conexion con patron
        private void backgroundWorkerPat_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            e.Result = patron.ConectaConIDA1S(worker);
        }
        private void backgroundWorkerPat_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBar1.Value = e.ProgressPercentage;
        }
        private void backgroundWorkerPat_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            progressBar1.Visible = false;

            if (e.Error != null)
            {
                MessageBox.Show("Error de excepcion al conectar con el patron.\r\n" + e.Error.ToString());
                PatronDesconectado();
                return;
            }

            PatronIDA1S.IDA_RESULT resultado = (PatronIDA1S.IDA_RESULT)e.Result;

            switch (resultado)
            {
                case PatronIDA1S.IDA_RESULT.IDA_OK:
                    PatronConectado();
                    break;
                case PatronIDA1S.IDA_RESULT.IDA_NOT_FOUND:
                    PatronDesconectado();
                    break;
                case PatronIDA1S.IDA_RESULT.IDA_SIN_RESPUESTA:
                    MessageBox.Show("El patron no responde.");
                    PatronDesconectado();
                    break;
                case PatronIDA1S.IDA_RESULT.IDA_FALLO_PATRON:
                    MessageBox.Show("Hubo un fallo en la respuesta del patron.");
                    PatronDesconectado();
                    break;
                default:
                    MessageBox.Show("Error desconocido.");
                    PatronDesconectado();
                    break;
            }
        }
        #endregion

        private void backgroundWorkerChekPat_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            PatronIDA1S.IDA_RESULT resultado;

            while( !worker.CancellationPending )
            {
                if (!detenCheckStatus)
                {
                    checkStatusActivo = true;
                    resultado = patron.ControlStatus();
                    checkStatusActivo = false;
                    if (resultado != PatronIDA1S.IDA_RESULT.IDA_OK)
                    {
                        e.Result = resultado;
                        return;
                    }

                    worker.ReportProgress(0, resultado);

                    Thread.Sleep(1000);   //Comprobamos es estado del patron cada segundo
                }
            }
            e.Result = PatronIDA1S.IDA_RESULT.IDA_OK;
        }

        private void backgroundWorkerChekPat_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {

        }

        private void backgroundWorkerChekPat_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null) throw e.Error;

            PatronIDA1S.IDA_RESULT resultado = (PatronIDA1S.IDA_RESULT)e.Result;

            switch (resultado)      //El error del patron se devuelve en e.result
            {
                case PatronIDA1S.IDA_RESULT.IDA_OK:
                    break;
                case PatronIDA1S.IDA_RESULT.IDA_SIN_RESPUESTA:
                    MessageBox.Show("El patron no responde.");
                    PatronDesconectado();
                    break;
                case PatronIDA1S.IDA_RESULT.IDA_FALLO_PATRON:
                    MessageBox.Show("Hubo un fallo en la respuesta del patron.");
                    PatronDesconectado();
                    break;
                default:
                    MessageBox.Show("Error desconocido.");
                    PatronDesconectado();
                    break;
            }

        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            PatronDesconectado();
        }

        private PatronIDA1S.IDA_RESULT EnviaComandoPatron( ComandosPatron comando)
        {
            PatronIDA1S.IDA_RESULT resultado = PatronIDA1S.IDA_RESULT.IDA_COMANDO_DESCONOCIDO;

            detenCheckStatus = true;        //Avisa a ChekPat para que detenga la comprobacion de estado del patron
            while (checkStatusActivo) { }   //Esperamos a que termine la comprobacion de estado del patron

            switch (comando)
            {
                case ComandosPatron.PREPARA_OCLUSION:
                    resultado = patron.PreparaOclusion();
                    break;
                case ComandosPatron.INICIA_OCLUSION:
                    resultado = patron.IniciaOclusion();
                    break;
                case ComandosPatron.FINALIZA_OCLUSION:
                    resultado = patron.FinalizaOclusion();   
                    break;
                case ComandosPatron.PREPARA_FLUJO:
                    break;
                case ComandosPatron.INICIA_FLUJO:
                    break;
                case ComandosPatron.FINALIZA_FLUJO:
                    break;
                case ComandosPatron.DESCONECTA:
                    patron.DesconectaPatron();
                    resultado = PatronIDA1S.IDA_RESULT.IDA_OK;
                    break;
                case ComandosPatron.CONECTA:
                    break;
                default:
                    break;
            }

            detenCheckStatus = false;
            return resultado;
        }

        public void ProcesaMedida( object sender, PatronIDA1S.MedidaRecibidaEventArgs e)
        {

            switch (e.estado)
            {
                case PatronIDA1S.EstadoIDA.FLUJO:
                    prueba.AddDatoVolumen(e.tiempo, e.volumen);
                    break;
                case PatronIDA1S.EstadoIDA.OCLUSION:
                    prueba.AddDatoOclusion(e.tiempo, e.presion);
                    break;
                default:
                    MessageBox.Show("Incongluencia en los datos de medida recibidos.");
                    PatronDesconectado();
                    return;
            }
        }

        private void btnFlujo_Click(object sender, EventArgs e)
        {
            InicioFlujo inicia = new InicioFlujo();
            if (inicia.ShowDialog(this) == DialogResult.Cancel) return;

            btnFlujo.Enabled = false;
        }
    }
}
