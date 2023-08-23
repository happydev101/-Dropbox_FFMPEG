using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace video_trimmer
{
    public partial class Login : Form
    {
        public string password = string.Empty;
        public Login()
        {
            InitializeComponent();
        }

        // Password of customer
        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            password = textBox1.Text;
        }

        // Login button clicked
        private void button1_Click(object sender, EventArgs e)
        {
            this.Hide();

            password = textBox1.Text;

            VideoTrimmerForm mainUI = new VideoTrimmerForm(password);
            mainUI.ShowDialog();

            Application.Exit();
        }
    }
}
