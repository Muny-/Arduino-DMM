using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Arduino_DMM
{
    public partial class COMPortPrompt : Form
    {

        public string COMPort = "_TMP_";

        public COMPortPrompt()
        {
            InitializeComponent();
        }

        bool setCancel = true;

        private void COMPortPrompt_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (setCancel)
                this.DialogResult = System.Windows.Forms.DialogResult.Cancel;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.COMPort = textBox1.Text;
            this.DialogResult = System.Windows.Forms.DialogResult.OK;
            setCancel = false;
        }
    }
}
