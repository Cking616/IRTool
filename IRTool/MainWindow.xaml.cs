using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace IRTool
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        const string IRCmdsPath = "./IRCmds.txt";
        const string IRStationPath = "./IRStation.txt";

        public MainWindow()
        {
            InitializeComponent();

            IrSendText_Initiate();

            IrStationTextBox_Initiate();
        }

        private void IrSendText_Initiate()
        {
            if (File.Exists(IRCmdsPath))
            {
                StreamReader sr = new StreamReader(IRCmdsPath, Encoding.Default);
                String line;
                while ((line = sr.ReadLine()) != null)
                {
                    IrSendTextBox.AddItem(new AutoCompleteEntry(line, null));
                }
            }
        }

        private void IrStationTextBox_Initiate()
        {
            if (File.Exists(IRStationPath))
            {
                StreamReader sr = new StreamReader(IRStationPath, Encoding.Default);
                String line;
                while ((line = sr.ReadLine()) != null)
                {
                    IrStationTextBox.AddItem(new AutoCompleteEntry(line, null));
                }
            }
        }
    }
}
