using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using PureWeb.Diagnostics;
using PureWeb.Server;

namespace ScribbleApp
{
    public partial class Form1 : Form
    {
        private string m_ShareUrl = null;

        public Form1()
        {
            InitializeComponent();

            // explicity set the AutoSize properties to fit the size of form content
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        }

        private void OnShareCreated(object sender, UpdateShareEventArgs e)
        {
            if (e.Exception == null)
            {
                Trace.WriteLine("Share created sucessfully and copied to the clipboard: " + e.ShareUrl);
                m_ShareUrl = e.ShareUrl;
                Clipboard.SetText(m_ShareUrl);
                MessageBox.Show("Share Url: "+m_ShareUrl, "Share created sucessfully, copied to clipboard");
            }
            else
            {
                Trace.WriteLine("Share creation failed with exception: " + e.Exception);
                MessageBox.Show(e.Exception.ToString(), "Share creation failed!");
            }
        }

        private void OnShareInvalidated(object sender, UpdateShareEventArgs e)
        {
            if (e.Exception == null)
            {
                Trace.WriteLine("Share sucessfully invalidated: " + e.ShareUrl);
                MessageBox.Show("Share sucessfully invalidated.");
            }
            else
            {
                Trace.WriteLine("Share invalidation failed with exception: " + e.Exception);
                MessageBox.Show(e.Exception.ToString(), "Share invalidate failed!");
            }
        }

        private void startSharingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CollaborationManager.Instance.CreateShare("Scientific", OnShareCreated);
        }

        private void stopSharingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CollaborationManager.Instance.InvalideShare(m_ShareUrl, OnShareInvalidated);
        }
    }
}
