using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;

namespace multi_threading
{
    public partial class frmMultiThreading : Form
    {
        public frmMultiThreading()
        {
            InitializeComponent();
        }
        Thread thRed;
        Thread thBlue;
        Random rdm;

        private void btnRed_Click(object sender, EventArgs e)
        {
            thRed = new Thread(threadRed);
            thRed.Start();
        }

        public void threadRed()
        {
            for (int i = 0; i < 100; i++)
            {
                Thread.Sleep(100);
                this.CreateGraphics().DrawRectangle(new Pen(Brushes.Red,4),
                    new Rectangle(rdm.Next(0,this.Width),
                    rdm.Next(0,this.Height),20,20));
            }
            MessageBox.Show("Completed Red", "Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnBlue_Click(object sender, EventArgs e)
        {
            thBlue = new Thread(threadBlue);
            thBlue.Start();
        }

        public void threadBlue()
        {
            for (int i = 0; i < 100; i++)
            {
                Thread.Sleep(100);
                this.CreateGraphics().DrawRectangle(new Pen(Brushes.Blue, 4),
                    new Rectangle(rdm.Next(0, this.Width),
                    rdm.Next(0, this.Height), 20, 20));
            }
            MessageBox.Show("Completed Blue", "Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void frmMultiThreading_Load(object sender, EventArgs e)
        {
            rdm = new Random();
        }
    }
}
