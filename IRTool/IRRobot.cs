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

namespace IrTool
{
    struct IrScaraPoint
    {
        public int acutualZPoint;
        public int acutualSPoint;
        public int acutualEPoint;
        public int acutualWPoint;
        public string station;
        public bool isPerch;
        public int index;
        public int closeZPoint;
        public int closeSPoint;
        public int closeEPoint;
        public int closeWPoint;
    }

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

    class IrRobotFilter : TerminatorReceiveFilter<StringPackageInfo>
    {
        public IrRobotFilter()
            : base(Encoding.ASCII.GetBytes("\r\n\0"))
        {
        }

        public override StringPackageInfo ResolvePackage(IBufferStream bufferStream)
        {
            byte[] bytes = new byte[bufferStream.Length];
            bufferStream.Read(bytes, 0, bytes.Length);
            string str = System.Text.Encoding.ASCII.GetString(bytes);
            str = str.Replace("\0", string.Empty);
            AppLog.Info("Ir接收", str);

            if (str.StartsWith(">"))
            {
                return new StringPackageInfo("BEGIN", "", null);
            }

            if (str.StartsWith("!"))
            {
                string[] strArry = str.Split();
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
            else
            {
                string[] strArry = str.Split();
                string strHead = strArry[0];
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
    }

    class IrRobot
    {
        private EasyClient irClient;
        DispatcherTimer irTimer;
        private string irAddress = "";
        private int irPort = 5000;

        private int irResetC = 0;
        private bool irNeedReset = false;
        private bool irIsIdle = true;
        private string irLastSend = "";

        private IrRobotFilter irFilter = new IrRobotFilter();
        private Queue<string> irSendBuffer = new Queue<string>();



        private IrScaraPoint irCurPoint;
        private string irTargetStation;
        private bool irNeedFixPoint;
        private bool irIsSendRCP;

        public bool IsConnected { get => irClient.IsConnected; }
        public bool IrNeedReset { get => irNeedReset; }
        public bool IrIsIdle { get => irIsIdle; }

        public IrRobot(string address, int port)
        {
            irAddress = address;
            irPort = port;
            irClient = new EasyClient();

            irClient.Connected += OnConnected;
            irClient.Closed += OnClosed;
            // Initialize the client with the receive filter and request handler
            irClient.Initialize(irFilter, OnRecieve);
            irClient.ConnectAsync(new IPEndPoint(IPAddress.Parse(irAddress), irPort));

            irTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            irTimer.Tick += OnTimer;
        }

        public bool LearnStation(string station, bool isLow, bool isPerch, int index)
        {
            string cmd = "learn " + station;

            if (isPerch)
            {
                cmd += " perch,";
            }
            else
            {
                cmd += " inside,";
            }

            if (isLow)
            {
                index = -index;
            }

            cmd += string.Format(" index = {0:D},", index);

            irSendBuffer.Enqueue(cmd);
            return true;
        }

        public bool MoveStation(string station, bool isHigh, bool isInside, bool islinear, int index, int speed)
        {
            string cmd = "move " + station;

            if (!isInside)
            {
                cmd += " perch,";
            }
            else
            {
                cmd += " inside,";
            }

            if (!isHigh)
            {
                index = -index;
            }

            cmd += string.Format(" index = {0:D},", index);

            cmd += string.Format(" speed {0:D}", speed);

            if (islinear)
            {
                cmd += ", linear";
            }

            irSendBuffer.Enqueue(cmd);
            return true;
        }

        public bool SendCmd(string cmd)
        {
            irSendBuffer.Enqueue(cmd);
            return true;
        }

        public bool ReConnect()
        {
            irClient.ConnectAsync(new IPEndPoint(IPAddress.Parse(irAddress), irPort));
            return true;
        }

        private void PaserRcpParameters(string[] param)
        {
            if (param[0] == "ACTUAL-Z")
            {
                irCurPoint.acutualZPoint = int.Parse(param[1]);
            }
            else if (param[0] == "ACTUAL-S")
            {
                irCurPoint.acutualSPoint = int.Parse(param[1]);
            }
            else if (param[0] == "ACTUAL-E")
            {
                irCurPoint.acutualEPoint = int.Parse(param[1]);
            }
            else if (param[0] == "ACTUAL-W")
            {
                irCurPoint.acutualWPoint = int.Parse(param[1]);
            }
            else if (param[0] == "STATION")
            {
                irCurPoint.station = param[1];
            }
            else if (param[0] == "INDEX")
            {
                irCurPoint.index = int.Parse(param[1]);
            }
            else if (param[0] == "CLOSEST-Z")
            {
                irCurPoint.closeZPoint = int.Parse(param[1]);
            }
            else if (param[0] == "CLOSEST-S")
            {
                irCurPoint.closeSPoint = int.Parse(param[1]);
            }
            else if (param[0] == "CLOSEST-E")
            {
                irCurPoint.closeEPoint = int.Parse(param[1]);
            }
            else if (param[0] == "CLOSEST-W")
            {
                irCurPoint.closeWPoint = int.Parse(param[1]);
            }
            else if (param[0] == "PERCH")
            {
                irCurPoint.isPerch = true;
            }
            else if (param[0] == "INSIDE")
            {
                irCurPoint.isPerch = false;
            }
            else
            {
                return;
            }
        }

        private void OnConnected(Object state, EventArgs e)
        {
            irSendBuffer.Clear();
            irTimer.IsEnabled = true;
            irNeedFixPoint = true;
            irIsSendRCP = false;
            AppLog.Info("系统", "成功与Ir控制器建立连接");
        }

        private void OnClosed(Object state, EventArgs e)
        {
            irTimer.IsEnabled = false;
            AppLog.Info("系统", "与Ir控制器连接断开");
        }

        private bool __SendCmd(string cmd)
        {
            if (!irIsIdle)
            {
                if (!irNeedReset)
                {
                    return false;
                }
            }

            string send = cmd;
            if (cmd.IndexOf("\n") != cmd.Length - 1)
                send = cmd + "\n";

            irClient.Send(Encoding.ASCII.GetBytes(send));
            string[] cmdList = cmd.Split();
            irLastSend = cmdList[0].ToUpper();
           
            if (irLastSend == "MOVE")
            {
                irTargetStation = cmdList[1].ToUpper();
                if (irTargetStation == "HOME")
                {
                    irLastSend = irTargetStation;
                }
            }
            irIsIdle = false;
            return true;
        }

        private void OnRecieve(StringPackageInfo request)
        {
            string key = request.Key.ToUpper();
            string body = request.Key.ToUpper();
            if (key == "BEGIN")
            {
                return;
            }

            if (key == "ERROR")
            {
                irResetC = 15;
                irNeedReset = true;
                AppLog.Info("系统", "收到错误返回，将会自动Reset");
                irSendBuffer.Clear();
                return;
            }

            if (key == "RESET" && body == "END")
            {
                AppLog.Info("系统", "Reset成功");
                irNeedReset = false;
                irIsIdle = true;
                return;
            }

            if (key == "RCP" )
            {
                if(body == "END")
                {
                    irNeedFixPoint = false;
                    irIsIdle = true;
                    return;
                }
                else
                {
                    string[] param = request.Parameters;
                    PaserRcpParameters(param);
                    return;
                }
            }

            if (key == "MOVE" && body == "END")
            {
                if(irTargetStation != irCurPoint.station)
                {
                    irNeedFixPoint = true;
                    irIsSendRCP = false;
                }

                irIsIdle = true;
                return;
            }

            if (key == irLastSend && body == "END")
            {
                irIsIdle = true;
                string msg = request.Key + "指令执行成功";
                AppLog.Info("系统", msg);
            }
        }

        protected void OnTimer(Object state, EventArgs e)
        {
            if (irNeedReset)
            {
                if (irResetC > 0)
                {
                    irResetC--;
                }
                else
                {
                    AppLog.Info("系统", "发送Reset");
                    __SendCmd("reset");
                    irResetC = 200;
                }
                return;
            }

            if ( irNeedFixPoint && !irIsSendRCP)
            {
                irIsSendRCP = true;
                __SendCmd("rcp");
            }

            if (irSendBuffer.Count > 0)
            {
                string cmd = irSendBuffer.ElementAt(0);
                if (__SendCmd(cmd))
                {
                    string msg = "执行指令" + cmd;
                    AppLog.Info("系统", msg);
                    irSendBuffer.Dequeue();
                }
            }
        }
    }
}
