using System;
using System.Windows.Forms;

namespace LinearCutOptimization
{
    public class CutOptimizationForm : Form
    {
        public CutOptimizationForm()
        {
            this.Text = "Cut Optimization";
            this.Width = 400;
            this.Height = 300;

            InitializeComponents();
        }

        private void InitializeComponents()
        {
            // Add components such as labels, textboxes, buttons, etc.
            Label titleLabel = new Label();
            titleLabel.Text = "Cut Optimization Form";
            titleLabel.Location = new System.Drawing.Point(50, 20);
            titleLabel.AutoSize = true;

            TextBox inputBox = new TextBox();
            inputBox.Location = new System.Drawing.Point(50, 60);

            Button optimizeButton = new Button();
            optimizeButton.Text = "Optimize";
            optimizeButton.Location = new System.Drawing.Point(50, 100);
            optimizeButton.Click += (s, e) => { MessageBox.Show("Optimization Started!"); };

            this.Controls.Add(titleLabel);
            this.Controls.Add(inputBox);
            this.Controls.Add(optimizeButton);
        }
    }
}