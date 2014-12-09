namespace ScribbleApp
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.toolStripDropDownButton1 = new System.Windows.Forms.ToolStripDropDownButton();
            this.startSharingToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.stopSharingToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.scribbleControl1 = new ScribbleApp.ScribbleControl();
            this.toolStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripDropDownButton1});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(379, 25);
            this.toolStrip1.TabIndex = 4;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // toolStripDropDownButton1
            // 
            this.toolStripDropDownButton1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripDropDownButton1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.startSharingToolStripMenuItem,
            this.stopSharingToolStripMenuItem});
            this.toolStripDropDownButton1.Image = ((System.Drawing.Image)(resources.GetObject("toolStripDropDownButton1.Image")));
            this.toolStripDropDownButton1.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripDropDownButton1.Name = "toolStripDropDownButton1";
            this.toolStripDropDownButton1.Size = new System.Drawing.Size(29, 22);
            this.toolStripDropDownButton1.Text = "toolStripDropDownButton1";
            // 
            // startSharingToolStripMenuItem
            // 
            this.startSharingToolStripMenuItem.Name = "startSharingToolStripMenuItem";
            this.startSharingToolStripMenuItem.Size = new System.Drawing.Size(159, 22);
            this.startSharingToolStripMenuItem.Text = "Start Sharing";
            this.startSharingToolStripMenuItem.Click += new System.EventHandler(this.startSharingToolStripMenuItem_Click);
            // 
            // stopSharingToolStripMenuItem
            // 
            this.stopSharingToolStripMenuItem.Name = "stopSharingToolStripMenuItem";
            this.stopSharingToolStripMenuItem.Size = new System.Drawing.Size(159, 22);
            this.stopSharingToolStripMenuItem.Text = "Stop Sharing";
            this.stopSharingToolStripMenuItem.Click += new System.EventHandler(this.stopSharingToolStripMenuItem_Click);
            // 
            // scribbleControl1
            // 
            this.scribbleControl1.BackColor = System.Drawing.Color.Black;
            this.scribbleControl1.CanDeferRendering = true;
            this.scribbleControl1.Location = new System.Drawing.Point(0, 0);
            this.scribbleControl1.Margin = new System.Windows.Forms.Padding(5);
            this.scribbleControl1.Name = "scribbleControl1";
            this.scribbleControl1.RemoteRenderer = null;
            this.scribbleControl1.Size = new System.Drawing.Size(200, 185);
            this.scribbleControl1.TabIndex = 0;
            this.scribbleControl1.ViewName = "ScribbleView";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ClientSize = new System.Drawing.Size(379, 322);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.scribbleControl1);
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "Form1";
            this.Text = "Form1";
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private ScribbleControl scribbleControl1;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripDropDownButton toolStripDropDownButton1;
        private System.Windows.Forms.ToolStripMenuItem startSharingToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem stopSharingToolStripMenuItem;



    }
}

