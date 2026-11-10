using System.Windows.Forms;

namespace smashgg_to_liquipedia
{
    public partial class Authentication : Form
    {
        public string token;
        public string lpdbApiKey;

        public Authentication(string token, string lpdbApiKey)
        {
            InitializeComponent();

            this.token = token;
            this.lpdbApiKey = lpdbApiKey;
            textBox1.Text = token;
            textBox2.Text = lpdbApiKey;
        }

        private void Authentication_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.token = textBox1.Text.Trim();
            this.lpdbApiKey = textBox2.Text.Trim();
        }
    }
}
