namespace Linienrechner
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            connect_label = new Label();
            notifyIcon1 = new NotifyIcon(components);
            label2 = new Label();
            label3 = new Label();
            label_adress = new Label();
            pictureBox1 = new PictureBox();
            label1 = new Label();
            label5 = new Label();
            Date_Label = new Label();
            dataGridView1 = new DataGridView();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            SuspendLayout();
            // 
            // connect_label
            // 
            connect_label.AutoSize = true;
            connect_label.Font = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            connect_label.ForeColor = Color.FromArgb(255, 128, 128);
            connect_label.Location = new Point(12, 47);
            connect_label.Name = "connect_label";
            connect_label.Size = new Size(141, 17);
            connect_label.TabIndex = 15;
            connect_label.Text = "Not connected to SPS";
            // 
            // notifyIcon1
            // 
            notifyIcon1.Text = "notifyIcon1";
            notifyIcon1.Visible = true;
            notifyIcon1.DoubleClick += notifyIcon1_DoubleClick;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            label2.Location = new Point(12, 26);
            label2.Name = "label2";
            label2.Size = new Size(57, 21);
            label2.TabIndex = 16;
            label2.Text = "Status";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            label3.Location = new Point(262, 26);
            label3.Name = "label3";
            label3.Size = new Size(81, 21);
            label3.TabIndex = 17;
            label3.Text = "IP-Adress";
            // 
            // label_adress
            // 
            label_adress.AutoSize = true;
            label_adress.Location = new Point(262, 47);
            label_adress.Name = "label_adress";
            label_adress.Size = new Size(13, 15);
            label_adress.TabIndex = 20;
            label_adress.Text = "0";
            // 
            // pictureBox1
            // 
            pictureBox1.Image = Properties.Resources.buo_icon;
            pictureBox1.Location = new Point(610, 14);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(50, 50);
            pictureBox1.TabIndex = 21;
            pictureBox1.TabStop = false;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(12, 282);
            label1.Name = "label1";
            label1.Size = new Size(75, 15);
            label1.TabIndex = 22;
            label1.Text = "Version: 1.0.0";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            label5.Location = new Point(421, 26);
            label5.Name = "label5";
            label5.Size = new Size(62, 21);
            label5.TabIndex = 23;
            label5.Text = "Datum";
            // 
            // Date_Label
            // 
            Date_Label.AutoSize = true;
            Date_Label.Location = new Point(421, 49);
            Date_Label.Name = "Date_Label";
            Date_Label.Size = new Size(13, 15);
            Date_Label.TabIndex = 24;
            Date_Label.Text = "0";
            // 
            // dataGridView1
            // 
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Location = new Point(12, 119);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowTemplate.Height = 25;
            dataGridView1.Size = new Size(648, 150);
            dataGridView1.TabIndex = 25;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(672, 312);
            Controls.Add(dataGridView1);
            Controls.Add(Date_Label);
            Controls.Add(label5);
            Controls.Add(label1);
            Controls.Add(pictureBox1);
            Controls.Add(label_adress);
            Controls.Add(label3);
            Controls.Add(connect_label);
            Controls.Add(label2);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximumSize = new Size(1920, 1160);
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Linienrechner";
            TransparencyKey = Color.Fuchsia;
            FormClosing += Form1_FormClosing;
            Load += Form1_Load;
            Shown += Form1_Shown;
            Resize += Form1_Resize;
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private Label connect_label;
        private NotifyIcon notifyIcon1;
        private Label label2;
        private Label label3;
        private Label label_adress;
        private PictureBox pictureBox1;
        private Label label1;
        private Label label5;
        private Label Date_Label;
        private DataGridView dataGridView1;
    }
}