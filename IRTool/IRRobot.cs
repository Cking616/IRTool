using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SuperSocket.ProtoBase;
using SuperSocket.ClientEngine;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace IRTool
{
    /// <summary>
    /// 使用Log4net插件的log日志对象
    /// </summary>
    public static class AppLog
    {
        private static Queue<string> IrRobotRevLog = new Queue<string>();

        public static void AddLog(string log) { IrRobotRevLog.Enqueue(log); }

        public static void Info(string cls, string log)
        {
            string str = cls + ":" + log;
            IrRobotRevLog.Enqueue(str);
        }

        public static string ReadLog() { return IrRobotRevLog.Dequeue(); }

        public static bool IsEmpty() { return IrRobotRevLog.Count == 0; }

        public static void Error(string err)
        {
            err = "Error:" + err;
            IrRobotRevLog.Enqueue(err);
        }
    }

    class IRRobotFilter : BeginEndMarkReceiveFilter<StringPackageInfo>
    {
        //开始和结束标记也可以是两个或两个以上的字节
        private readonly static byte[] BeginMark = new byte[] { (byte)'>' };
        private readonly static byte[] EndMark = new byte[] { (byte)'\r' };
        

        public IRRobotFilter()
            : base(BeginMark, EndMark) //传入开始标记和结束标记
        {

        }

        public override StringPackageInfo ResolvePackage(IBufferStream bufferStream)
        {
            if (bufferStream.Length == 1)
            {
                ChangeBeginMark(new byte[] { (byte)'!' });
                return new StringPackageInfo("Begin", "", null);
            }

            // other code you need implement according yoru protocol details
            byte[] bytes = new byte[bufferStream.Length];
            bufferStream.Read(bytes, 0, bytes.Length);
            string str = System.Text.Encoding.ASCII.GetString(bytes);
            AppLog.Info("Ir接收", str);
            string[] strArry = str.Split(' ');
            string strHead = strArry[1];
            string[] strParam = strArry.Skip(2).ToArray();
            string[] strHeads = strHead.Split('-');
            if (strHeads.Length < 2)
            {
                return new StringPackageInfo(strHead, "", strParam);
            }
            else
            {
                string strBody = strHeads[0];
                strHead = strHeads[1];
                return new StringPackageInfo(strHead, strBody, strParam);
            }
        }
    }
   
    class IRRobot
    {
        private EasyClient irClient;
        private string irAddress = "";
        private int irPort = 5000;
        private int irResetC = 0;
        private bool irNeedReset = false;
        private bool irIsIdle = true;
        private string irLastSend = "";
        private IRRobotFilter irFilter = new IRRobotFilter();
        private Queue<string> irSendBuffer = new Queue<string>();

        public bool IsConnected { get => irClient.IsConnected; }
        public bool IrNeedReset { get => irNeedReset;}
        public bool IrIsIdle { get => irIsIdle; }


        public IRRobot(string address, int port)
        {
            irAddress = address;
            irPort = port;
            irClient = new EasyClient();

            irClient.Connected += OnConnected;
            irClient.Closed += OnClosed;
            // Initialize the client with the receive filter and request handler
            irClient.Initialize(irFilter, OnRecieve);
            irClient.ConnectAsync(new IPEndPoint(IPAddress.Parse(irAddress), irPort));

            DispatcherTimer timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            timer.Tick += OnTimer;
            timer.IsEnabled = true;
        }

        private void OnConnected(Object state, EventArgs e) => AppLog.Info("系统", "成功与Ir控制器建立连接");

        private void OnClosed(Object state, EventArgs e) => AppLog.Info("系统", "与Ir控制器连接断开");

        public bool SendCmd(string cmd)
        {
            irSendBuffer.Enqueue(cmd);
            return true;
        }

        private bool __SendCmd(string cmd)
        {          
            if (!irIsIdle)
            {
                return false;
            }

            string send = cmd;
            if (cmd.IndexOf("\n") != cmd.Length - 1)
                send = cmd + "\n";

            irClient.Send(Encoding.ASCII.GetBytes(send));
            irFilter.ChangeBeginMark(new byte[] { (byte)'>' });
            irLastSend = cmd;
            irIsIdle = false;
            return true;
        }

        private void OnRecieve(StringPackageInfo request)
        {
            if (request.Key == "Begin")
            {
                return;
            }

            if (request.Key == "ERROR")
            {
                irResetC = 5;
                irNeedReset = true;
                AppLog.Info("系统", "收到错误返回，将会自动Reset");
                // SendCmd("RESET\n");
            }

            if (request.Key == "RESET" && request.Body == "END")
            {
                AppLog.Info("系统", "Reset成功");
                irNeedReset = false;
                irIsIdle = true;
                return;
            }

            if(request.Key == irLastSend && request.Body == "END")
            {
                irIsIdle = true;
                string msg = request.Key +  "指令执行成功";
                AppLog.Info("系统", msg);
            }
        }

        private void OnTimer(Object state, EventArgs e)
        {
            if (irNeedReset)
            {
                if (irResetC > 0)
                {
                    irResetC--;
                }
                else
                {
                    __SendCmd("reset");
                }
                return;
            }

            if (irSendBuffer.Count > 0)
            {
                string cmd = irSendBuffer.ElementAt(0);
                if(__SendCmd(cmd))
                {
                    string msg = "执行指令" + cmd;
                    AppLog.Info("系统", msg);
                    irSendBuffer.Dequeue();
                }
            }
        }
    }
}
