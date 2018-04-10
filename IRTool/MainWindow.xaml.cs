using Microsoft.Win32;
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
using System.Windows.Threading;

namespace IRTool
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        const string IRCmdsPath = "./IRCmds.txt";
        const string IRStationPath = "./IRStation.txt";
        IRRobot irRobot = null;
        public MainWindow()
        {
            InitializeComponent();

            DispatcherTimer timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            timer.Tick += OnTimer;
            timer.IsEnabled = true;

            IrSendText_Initiate();
            IrStationTextBox_Initiate();
        }

        private void ExitApp()
        {

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

        public void IrConnect_Click(object sender, RoutedEventArgs e)
        {
            irRobot = new IRRobot(IrAddress.Text, 5001);
        }

        public void IrSend_Click(object sender, RoutedEventArgs e)
        {
            String S = IrSendTextBox.Text;
            if (S == "")
            {
                return;
            }

            IrSendTextBox.AddItem(new AutoCompleteEntry(S, null));

            if (irRobot.IsConnected)
            {
                irRobot.SendCmd(S);
            }
            else
            {
                AppLog.Error("IrRobot didn't connected");
            }

            IrSendTextBox.Text = "";
        }

        public void IrFilePath_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog()
            {
                Filter = "文本文件|*.txt"
            };
            if (dialog.ShowDialog() == true)
            {
                IrScriptFilePath.Text = dialog.FileName + dialog.SafeFileName;
            }
        }

        public void IrSendFile_Click(object sender, RoutedEventArgs e)
        {
            string FilePath = IrScriptFilePath.Text;
            if (File.Exists(FilePath))
            {
                StreamReader sr = new StreamReader(FilePath, Encoding.Default);
                String line;
                while ((line = sr.ReadLine()) != null)
                {
                }
            }
            else
            {
                MessageBox.Show("文件不存在，请选择正确的文件然后重试");
            }
        }

        public void IrExit_Click(object sender, RoutedEventArgs e)
        {
            ExitApp();
            Environment.Exit(0);
        }

        private void IrRobot_Process()
        {
            if (IrSendCmd.IsEnabled)
            {
                if (!irRobot.IsConnected)
                {
                    IrSendCmd.IsEnabled = false;
                    IrConnect.IsEnabled = true;
                    IrSendFile.IsEnabled = false;
                }

                if (irRobot.IrNeedReset)
                {
                    IrSendCmd.IsEnabled = false;
                    IrConnect.IsEnabled = true;
                    IrSendFile.IsEnabled = false;
                }
            }
            else
            {
                if (irRobot != null)
                {
                    if (irRobot.IsConnected && !irRobot.IrNeedReset)
                    {
                        IrSendCmd.IsEnabled = true;
                        IrConnect.IsEnabled = false;
                        IrSendFile.IsEnabled = true;
                    }
                }
                else
                {
                    IrSendCmd.IsEnabled = false;
                    IrSendFile.IsEnabled = false;
                    IrConnect.IsEnabled = true;
                }
            }
        }

        public void IrRecvMsgs(String S) => Dispatcher.BeginInvoke(new Action(() =>
                                          {
                                              if (IrMsgsBox.Text.Length == 0)
                                                  IrMsgsBox.Text = S;
                                              else
                                              {
                                                  if (S.IndexOf("\n") != S.Length - 1)
                                                      IrMsgsBox.Text += S + "\n";
                                                  else
                                                      IrMsgsBox.Text += S;
                                              }
                                              IrMsgsBox.Focus();
                                              IrMsgsBox.CaretIndex = IrMsgsBox.Text.Length;
                                          }));


        public void OnTimer(Object state, EventArgs e)
        {
            IrRobot_Process();

            while (!AppLog.IsEmpty())
            {
                string str = AppLog.ReadLog();
                IrRecvMsgs(str);
            }
        }
    }
}
