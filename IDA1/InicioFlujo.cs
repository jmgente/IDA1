using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace IDA1
{
    public partial class InicioFlujo : Form
    {
        public Form1 llamador;
        int contador = 0;

        public InicioFlujo()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void InicioFlujo_Shown(object sender, EventArgs e)
        {
            timer1.Start();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (!llamador.patron.HayAire)
            {
                if (++contador > 5)
                {
                    button1.Enabled = true;
                    timer1.Stop();
                    return;
                }
            }
            else contador = 0;
        }
    }
}
