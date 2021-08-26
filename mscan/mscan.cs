using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Xml;

namespace mscan
{

    public partial class mscan : Form
    {

        static int MAX_PARAM = 24;
        static uint TIMEOUT = 100;
        static uint LOG_MSG_SIZE = 0x33;
        static Byte TRAILER_VALUE = 0x0D;

        enum eLogMsgType : Byte
        {
            LOG_24x2B = 0x81,
            LOG_24x1B = 0x82,
            LOG_5x2B = 0x83,
            LOG_24x2B_LT = 0x84,
            LOG_2x2B_6x1B_LT = 0x85,
            LOG_5x2B_LT = 0x86,
            LOG_COPY = 0x87,
            LOG_INV = 0x88,
            LOG_LT = 0x89,
        }

        struct sLogType
        {
            public Byte ReqHeader;
            public double Rate;
            public Byte RespHeader;
            public List<Tuple<int, int>> Elem;
            public int RespSize;
        }

        static Dictionary<eLogMsgType, sLogType> mLogMsgInfo = new Dictionary<eLogMsgType, sLogType>
        {
            { eLogMsgType.LOG_24x2B, new sLogType{ ReqHeader = (Byte)eLogMsgType.LOG_24x2B, Rate = 10, RespHeader = 0x01, Elem = new List<Tuple<int, int>>{ Tuple.Create(24, 2) }, RespSize = 0x33 } },
            { eLogMsgType.LOG_24x1B, new sLogType{ ReqHeader = (Byte)eLogMsgType.LOG_24x1B, Rate = 5, RespHeader = 0x02, Elem = new List<Tuple<int, int>>{ Tuple.Create(24, 1) }, RespSize = 0x1B } },
            { eLogMsgType.LOG_5x2B, new sLogType{ ReqHeader = (Byte)eLogMsgType.LOG_5x2B, Rate = 2.5, RespHeader = 0x03, Elem = new List<Tuple<int, int>>{ Tuple.Create(5, 2) }, RespSize = 0x0D } },
            { eLogMsgType.LOG_24x2B_LT, new sLogType{ ReqHeader = (Byte)eLogMsgType.LOG_24x2B_LT, Rate = 10, RespHeader = 0x04, Elem = new List<Tuple<int, int>>{ Tuple.Create(24, 2) }, RespSize = 0x33 } },
            { eLogMsgType.LOG_2x2B_6x1B_LT, new sLogType{ ReqHeader = (Byte)eLogMsgType.LOG_2x2B_6x1B_LT, Rate = 2.5, RespHeader = 0x05, Elem = new List<Tuple<int, int>>{ Tuple.Create(2, 2), Tuple.Create(6, 1) }, RespSize = 0x0B } },
            { eLogMsgType.LOG_5x2B_LT, new sLogType{ ReqHeader = (Byte)eLogMsgType.LOG_5x2B_LT, Rate = 2.5, RespHeader = 0x06, Elem = new List<Tuple<int, int>>{ Tuple.Create(5, 2) }, RespSize = 0x0D } },
            // other types are non-periodic responses with no logging
        };

        class CircularBuffer<type>
        {
            public CircularBuffer(int size) { mSize = size;  mData = new type[size]; mHead = 0; mLength = 0; }

            public void Enqueue(type elem) {
                if (Length == mData.Length) throw (new Exception("enqueue: overflow"));
                mData[mHead + mLength++] = elem;
            }
            public type Dequeue() {
                if (Length == 0) throw (new Exception("dequeue: underflow"));
                var head = mHead;
                mHead = (mHead + 1) % mData.Length;
                mLength--;
                return mData[head];
            }
            public void Enqueue(type[] data, int size = 0) {
                if (size == 0) size = data.Length;
                if (mLength + size > mData.Length) throw (new Exception("enqueue: overflow"));
                var start = (mHead + mLength) % mData.Length;
                var copyElem = Math.Min(size, mData.Length - start);
                Buffer.BlockCopy(data, 0, mData, start, copyElem);
                Buffer.BlockCopy(data, copyElem, mData, 0, size - copyElem);
                mLength += size;
            }
            public type[] Dequeue(int size)
            {
                if (mLength < size) throw (new Exception("dequeue: underflow"));
                type[] data = null;
                if (size > 0)
                {
                    data = new type[size];
                    var copyElem = Math.Min(size, mData.Length - mHead);
                    Buffer.BlockCopy(mData, mHead, data, 0, copyElem);
                    Buffer.BlockCopy(mData, 0, data, copyElem, size - copyElem);
                    mHead = (mHead + size) % mData.Length;
                    mLength -= size;
                }
                return data;
            }

            type[] mData;
            int mHead;
            int mLength;
            int mSize;

            public int Length { get => mLength; }
            public int Size { get => mSize; }
        };

        // variables
        eLogMsgType mLogMsgType;
        System.Timers.Timer mEnqueueTimer;
        System.Timers.Timer mDequeueTimer;
        CircularBuffer<Byte> mBuffer = new CircularBuffer<Byte>(j2534.MAX_MSG_SIZE * 4);

        uint mDeviceId = uint.MaxValue;
        uint mChannelId = uint.MaxValue;
        uint mFilterId = uint.MaxValue;

        uint[] mData = new uint[24];

        // Params
        XmlDocument mParamXml = null;
        DataTable mDataTable = new DataTable();
        struct sParamInfo {
            public string mDisplayName;
            public string mLogName;
            public string mRequestId;
            public string mUnitName;
            public string mEvalExpr;
            public double mChartMin;
            public double mChartMax;
        }
        Dictionary<string, sParamInfo> mParamInfo = new Dictionary<string, sParamInfo>();
        System.Windows.Forms.DataVisualization.Charting.Chart[] mCharts;

        // ROM
        XmlDocument mMetadataXml = null;
        Byte[] mROMFile = null;

        public mscan()
        {
            InitializeComponent();

            dataGridViewParam.Rows.Add(MAX_PARAM);

            mCharts = new System.Windows.Forms.DataVisualization.Charting.Chart[] { chart1, chart2, chart3, chart4, chart5, chart6, };

            buttonConnect.Enabled = false;
            buttonLoadROM.Enabled = false;

            this.Height = dataGridViewParam.Height + buttonConnect.Height;

            for (int i = 0; i < mData.Length; i++) mData[i] = 0;
        }

        private void buttonConnect_Click(object sender, EventArgs e)
        {
            j2534.PASSTHRU_MSG msg = new j2534.PASSTHRU_MSG();
            IntPtr pMsg = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(j2534.PASSTHRU_MSG)));
            j2534.eError1 ret;
            uint numMsgs = 1;

            if (mDeviceId != uint.MaxValue && mChannelId != uint.MaxValue)
            {
                // stop the queueing threads
                mEnqueueTimer.Stop();
                mEnqueueTimer.Dispose();
                mDequeueTimer.Stop();
                mDequeueTimer.Dispose();

                // block responses
                //ret = (j2534.eError1)j2534.PassThruStopMsgFilter(mChannelId, mFilterId);
                ret = (j2534.eError1)j2534.PassThruIoctl(mChannelId, (uint)j2534.eIoctl1.CLEAR_MSG_FILTERS, IntPtr.Zero, IntPtr.Zero);
                if (ret != j2534.eError1.ERR_SUCCESS) MessageBox.Show("PassThruIoctl: CLEAR_MSG_FILTERS " + ret.ToString());
                ret = (j2534.eError1)j2534.PassThruIoctl(mChannelId, (uint)j2534.eIoctl1.CLEAR_RX_BUFFER, IntPtr.Zero, IntPtr.Zero);
                if (ret != j2534.eError1.ERR_SUCCESS) MessageBox.Show("PassThruIoctl: CLEAR_RX_FILTERS " + ret.ToString());

                if (false)
                {
                    msg.ProtocolID = (uint)j2534.eProtocol1.ISO9141;
                    msg.RxStatus = 0;
                    msg.TxFlags = 0;
                    msg.Timestamp = 0;
                    msg.ExtraDataIndex = 0;
                    msg.DataSize = 0;

                    for (int i = 0; i < 10; i++)
                    {
                        numMsgs = 1;
                        Marshal.StructureToPtr(msg, pMsg, false);
                        ret = (j2534.eError1)j2534.PassThruReadMsgs(mChannelId, pMsg, ref numMsgs, TIMEOUT);
                        Marshal.PtrToStructure(pMsg, msg);
                        if (ret == j2534.eError1.ERR_SUCCESS) MessageBox.Show("PassThruReadMsgs: messages still received (" + msg.DataSize + "B) " + ret.ToString());
                    }
                }

                // write logging cancel - NOTE: this hangs with a nonzero timeout
                msg.ProtocolID = (uint)j2534.eProtocol1.ISO9141;
                msg.RxStatus = 0;
                msg.TxFlags = 0;
                msg.Timestamp = 0;
                msg.ExtraDataIndex = 0;
                msg.DataSize = 0x1;
                for (uint i = 0; i < msg.DataSize; i++) msg.Data[i] = 0x00;
                Marshal.StructureToPtr(msg, pMsg, false);
                numMsgs = 1;
                ret = (j2534.eError1)j2534.PassThruWriteMsgs(mChannelId, pMsg, ref numMsgs, TIMEOUT);
                if (ret != j2534.eError1.ERR_SUCCESS) MessageBox.Show("PassThruIoctl: CLEAR_RX_FILTERS " + ret.ToString());

                // disconnect
                ret = (j2534.eError1)j2534.PassThruDisconnect(mChannelId);
                if (ret != j2534.eError1.ERR_SUCCESS) MessageBox.Show("PassThruDisconnect: " + ret.ToString());
                ret = (j2534.eError1)j2534.PassThruClose(mDeviceId);
                if (ret != j2534.eError1.ERR_SUCCESS) MessageBox.Show("PassThruClose: " + ret.ToString());

                buttonConnect.Text = "Connect";

                mDeviceId = uint.MaxValue;
                mChannelId = uint.MaxValue;
            }
            else
            {
                j2534.init();

                mDeviceId = uint.MaxValue;
                ret = (j2534.eError1)j2534.PassThruOpen(IntPtr.Zero, ref mDeviceId);
                if (ret != j2534.eError1.ERR_SUCCESS) MessageBox.Show("PassThruOpen: " + ret.ToString());

                byte[] apiVersion = new byte[256], dllVersion = new byte[256], firmwareVersion = new byte[256];
                ret = (j2534.eError1)j2534.PassThruReadVersion(apiVersion, dllVersion, firmwareVersion, mDeviceId);
                if (ret != j2534.eError1.ERR_SUCCESS) MessageBox.Show("PassThruReadVersion: " + ret.ToString());

                mChannelId = uint.MaxValue;
                ret = (j2534.eError1)j2534.PassThruConnect(mDeviceId, (uint)j2534.eProtocol1.ISO9141, (uint)(j2534.eConnect.ISO9141_K_LINE_ONLY | j2534.eConnect.ISO9141_NO_CHECKSUM), 62500, ref mChannelId);
                if (ret != j2534.eError1.ERR_SUCCESS) MessageBox.Show("PassThruConnect: " + ret.ToString());

                // clear all message filters
                ret = (j2534.eError1)j2534.PassThruIoctl(mChannelId, (uint)j2534.eIoctl1.CLEAR_MSG_FILTERS, IntPtr.Zero, IntPtr.Zero);
                ret = (j2534.eError1)j2534.PassThruIoctl(mChannelId, (uint)j2534.eIoctl1.CLEAR_RX_BUFFER, IntPtr.Zero, IntPtr.Zero);
                //ret = (j2534.eError1)j2534.PassThruIoctl(mChannelId, (uint)j2534.eIoctl1.CLEAR_TX_BUFFER, IntPtr.Zero, IntPtr.Zero);

                // setup filter to pass all.  the response message format is some what arbitrary
                msg.ProtocolID = (uint)j2534.eProtocol1.ISO9141;
                msg.RxStatus = 0;
                msg.TxFlags = 0;
                msg.Timestamp = 0;
                msg.ExtraDataIndex = 0;
                msg.DataSize = 1;
                for (uint i = 0; i < msg.DataSize; i++) msg.Data[i] = 0x0;

                Marshal.StructureToPtr(msg, pMsg, false);
                ret = (j2534.eError1)j2534.PassThruStartMsgFilter(mChannelId, (uint)j2534.eFilter.PASS_FILTER, pMsg, pMsg, IntPtr.Zero, ref mFilterId);

                // FIXME: hard code 24x2B for now
                mLogMsgType = eLogMsgType.LOG_24x2B;

                // write log message
                msg.ProtocolID = (uint)j2534.eProtocol1.ISO9141;
                msg.RxStatus = 0;
                msg.TxFlags = 0;
                msg.Timestamp = 0;
                msg.ExtraDataIndex = 0;
                msg.DataSize = LOG_MSG_SIZE;
                for (uint i = 0; i < msg.DataSize; i++) msg.Data[i] = 0x0;

                // header
                msg.Data[0x00] = mLogMsgInfo[mLogMsgType].ReqHeader;
                // data based on params
                for (int i = 0; i < 0x18; i++)
                {
                    var valueString = dataGridViewParam.Rows[i].Cells[1].Value?.ToString();
                    var value = valueString != null && valueString != "" ? Convert.ToUInt16(valueString, 16) : 0;
                    msg.Data[2 * i + 1] = (Byte)((value >> 0) & 0xFF);
                    msg.Data[2 * i + 2] = (Byte)((value >> 8) & 0xFF);
                }
                // checksum
                for (uint i = 0; i < 0x31; i++) msg.Data[0x31] += msg.Data[i];
                // trailer
                msg.Data[0x32] = TRAILER_VALUE;

                numMsgs = 1;
                Marshal.StructureToPtr(msg, pMsg, false);
                ret = (j2534.eError1)j2534.PassThruWriteMsgs(mChannelId, pMsg, ref numMsgs, TIMEOUT);
                if (ret != 0 || numMsgs != 1) MessageBox.Show("PassThruWriteMsgs: " + ret.ToString() + ", " + numMsgs.ToString());

                // read message start
                msg.ProtocolID = (uint)j2534.eProtocol1.ISO9141;
                msg.RxStatus = 0;
                msg.TxFlags = 0;
                msg.Timestamp = 0;
                msg.ExtraDataIndex = 0;
                msg.DataSize = 0;

                numMsgs = 1;
                Marshal.StructureToPtr(msg, pMsg, false);
                ret = (j2534.eError1)j2534.PassThruReadMsgs(mChannelId, pMsg, ref numMsgs, TIMEOUT);
                Marshal.PtrToStructure(pMsg, msg);
                if (ret != j2534.eError1.ERR_SUCCESS || numMsgs != 1 || msg.DataSize != 0x0 || msg.RxStatus != (uint)j2534.eRxStatus.START_OF_MESSAGE)
                {
                    MessageBox.Show("PassThruReadMsgs: " + ret.ToString() + ", " + numMsgs.ToString() + ", " + msg.DataSize.ToString());
                }

                // setup timers - logging is already enabled
                // FIXME: temporary rate hacks because otherwise queue is overflowed
                mDequeueTimer = new System.Timers.Timer();
                mDequeueTimer.Interval = mLogMsgInfo[mLogMsgType].Rate / 2;
                mDequeueTimer.AutoReset = true;
                mDequeueTimer.Elapsed += HandleDequeue;
                mDequeueTimer.Start();

                mEnqueueTimer = new System.Timers.Timer();
                mEnqueueTimer.Interval = mLogMsgInfo[mLogMsgType].Rate * 2;
                mEnqueueTimer.AutoReset = true;
                mEnqueueTimer.Elapsed += HandleEnqueue;
                mEnqueueTimer.Start();

                if (mDeviceId != uint.MaxValue && mChannelId != uint.MaxValue) buttonConnect.Text = "Disconnect";
            }

            Marshal.FreeHGlobal(pMsg);
        }

        j2534.PASSTHRU_MSG rmsg = new j2534.PASSTHRU_MSG();
        IntPtr rpMsg = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(j2534.PASSTHRU_MSG)));
        void HandleEnqueue(object sender, EventArgs e)
        {
            // need to lock buffer for read since we only have one msg data structure and PassThruReadMsgs probably not thread safe
            lock (mBuffer)
            {
                // try to read data from serial
                rmsg.ProtocolID = (uint)j2534.eProtocol1.ISO9141;
                rmsg.RxStatus = 0;
                rmsg.TxFlags = 0;
                rmsg.Timestamp = 0;
                rmsg.ExtraDataIndex = 0;
                rmsg.DataSize = 0;

                uint numMsgs = 1;
                Marshal.StructureToPtr(rmsg, rpMsg, false);
                // FIXME: why does it buffer 0xFE0 worth of data?
                var ret = (j2534.eError1)j2534.PassThruReadMsgs(mChannelId, rpMsg, ref numMsgs, 0);
                Marshal.PtrToStructure(rpMsg, rmsg);

                try
                {
                    if (ret == j2534.eError1.ERR_SUCCESS && rmsg.DataSize > 0 && numMsgs > 0) mBuffer.Enqueue(rmsg.Data, (int)rmsg.DataSize);
                }
                catch (Exception x)
                {
                    MessageBox.Show("read: " + x.ToString());
                }
            }
        }

        void HandleDequeue(object sender, EventArgs e)
        {
            Byte[] buffer = null;
            lock (mBuffer)
            {
                int size = 0;
                if (mLogMsgInfo[mLogMsgType].RespSize <= mBuffer.Length) size = mLogMsgInfo[mLogMsgType].RespSize;
                if (size * 2 <= mBuffer.Length) size *= 2;
                buffer = mBuffer.Dequeue(size);
            }

            if (buffer != null && buffer[0x00] == mLogMsgInfo[mLogMsgType].RespHeader && buffer[mLogMsgInfo[mLogMsgType].RespSize - 1] == TRAILER_VALUE)
            {
                int index = 1;
                foreach (var elemInfo in mLogMsgInfo[mLogMsgType].Elem)
                {
                    for (int i = 0; i < elemInfo.Item1; i++, index += elemInfo.Item2) mData[i] = (uint)((elemInfo.Item2 == 1 ? 0 : (buffer[index + 1] << 8)) | (buffer[index] << 0));
                }

                this.Invoke(new Action(() => UpdateParams()));
            }
        }

        void UpdateParams()
        {
            // update param and charts
            int count = 0;
            foreach (DataGridViewRow row in dataGridViewParam.Rows)
            {
                var paramName = (row.Cells[0] as DataGridViewComboBoxCell).Value?.ToString();

                if (paramName != null && paramName != "")
                {
                    var data = Convert.ToDouble(mDataTable.Compute(mParamInfo[paramName].mEvalExpr.Replace("x", mData[count].ToString()), String.Empty));
                    (row.Cells[2] as DataGridViewTextBoxCell).Value = data;

                    if (count < mCharts.Length)
                    {
                        mCharts[count].Series[0].Points.Add(data);
                        if (mCharts[count].Series[0].Points.Count > 20) mCharts[count].Series[0].Points.RemoveAt(0);

                        if (data < mCharts[count].ChartAreas[0].AxisY.Minimum) mCharts[count].ChartAreas[0].AxisY.Minimum = data;
                        if (data > mCharts[count].ChartAreas[0].AxisY.Maximum) mCharts[count].ChartAreas[0].AxisY.Maximum = data;
                        mCharts[count].Update();
                    }
                }

                count++;
            }

            // update children
        }

        private void buttonLoadParam_Click(object sender, EventArgs e)
        {
            OpenFileDialog fd = new OpenFileDialog { Filter = "xml files (*.xml)|*.xml|all files (*.*)|*.*", Title = "Load EvoScan Param XML", DefaultExt = "xml" };

            if (fd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    mParamXml = new XmlDocument();
                    mParamXml.Load(fd.FileName);

                    LoadECU();
                    buttonConnect.Enabled = true;
                }
                catch (Exception x)
                {
                    mParamXml = null;
                    //foreach (DataGridViewRow row in dataGridViewParam.Rows) (row.Cells[0] as DataGridViewComboBoxCell).Items.Clear();
                    //comboBox1.Items.Clear();
                    buttonConnect.Enabled = false;
                    MessageBox.Show("error: paramFileName " + fd.FileName + " " + x.ToString());
                }
            }
        }

        private void LoadECU()
        {
            var ecuNodes = mParamXml.SelectNodes("/EvoScanDataLogger/vehicle/ecu");

            foreach (XmlNode ecuNode in ecuNodes)
            {
                if (ecuNode.Attributes["name"].InnerText != "EFI") continue;

                comboBox1.Items.Add(ecuNode.Attributes["name"].InnerText);
            }

            foreach (var tableItem in comboBox1.Items)
            {
                var width = Convert.ToInt32(comboBox1.CreateGraphics().MeasureString(tableItem.ToString(), comboBox1.Font).Width);
                comboBox1.DropDownWidth = Math.Max(comboBox1.DropDownWidth, width);
            }
            if (comboBox1.Items.Count > 0) comboBox1.SelectedIndex = 0;
            LoadParams();
        }

        private void LoadParams()
        {
            var ecuNodes = mParamXml.SelectNodes("/EvoScanDataLogger/vehicle/ecu");

            foreach (XmlNode ecuNode in ecuNodes)
            {
                if (ecuNode.Attributes["name"].InnerText == comboBox1.SelectedItem.ToString())
                {
                    XmlNode modeNode = ecuNode.SelectSingleNode("Mode2");

                    foreach (DataGridViewRow row in dataGridViewParam.Rows)
                    {
                        (row.Cells[0] as DataGridViewComboBoxCell).Items.Clear();
                        (row.Cells[0] as DataGridViewComboBoxCell).Items.Add("");
                    }

                    mParamInfo.Clear();
                    foreach (XmlNode paramNode in modeNode)
                    {

                        // test eval
                        try
                        {
                            var logName = paramNode.Attributes["LogReference"].InnerText;
                            var requestId = paramNode.Attributes["RequestID"].InnerText;
                            var evalExpr = paramNode.Attributes["Eval"].InnerText;

                            // TODO: handle indirection
                            Convert.ToUInt16(requestId, 16);
                            Convert.ToDouble(mDataTable.Compute(evalExpr.Replace("x", 1.ToString()), String.Empty));
                            // add elements
                            foreach (DataGridViewRow row in dataGridViewParam.Rows) (row.Cells[0] as DataGridViewComboBoxCell).Items.Add(logName);

                            mParamInfo.Add(paramNode.Attributes["LogReference"].InnerText, new sParamInfo
                            {
                                mDisplayName = paramNode.Attributes["Display"].InnerText,
                                mLogName = paramNode.Attributes["LogReference"].InnerText,
                                mRequestId = paramNode.Attributes["RequestID"].InnerText,
                                mUnitName = paramNode.Attributes["Unit"].InnerText,
                                mEvalExpr = paramNode.Attributes["Eval"].InnerText,
                                mChartMin = Convert.ToDouble(paramNode.Attributes["ChartMin"].InnerText),
                                mChartMax = Convert.ToDouble(paramNode.Attributes["ChartMax"].InnerText),
                            });
                        }
                        catch (Exception x)
                        {
                            // do nothing
                        }
                    }

                    int count = 0;
                    foreach (DataGridViewRow row in dataGridViewParam.Rows)
                    {
                        if (count < mParamInfo.Count)
                        {
                            var paramName = (row.Cells[0] as DataGridViewComboBoxCell).Items[count++ + 1].ToString();
                            (row.Cells[0] as DataGridViewComboBoxCell).Value = paramName;
                            LoadParam(row, paramName);
                        }
                    }
                }
            }
        }

        private void LoadParam(DataGridViewRow row, string paramName)
        {
            paramName = paramName == null ? "" : paramName;
            (row.Cells[1] as DataGridViewTextBoxCell).Value = paramName == "" ? "" : mParamInfo[paramName].mRequestId;
            (row.Cells[2] as DataGridViewTextBoxCell).Value = "";
            (row.Cells[3] as DataGridViewTextBoxCell).Value = paramName == "" ? "" : mParamInfo[paramName].mUnitName;
            (row.Cells[4] as DataGridViewTextBoxCell).Value = paramName == "" ? "" : mParamInfo[paramName].mEvalExpr;

            if (row.Index < mCharts.Length)
            {
                mCharts[row.Index].Titles[0].Text = paramName == "" ? "" : mParamInfo[paramName].mDisplayName;
                mCharts[row.Index].ChartAreas[0].AxisY.Minimum = paramName == "" ? 0 : mParamInfo[paramName].mChartMin;
                mCharts[row.Index].ChartAreas[0].AxisY.Maximum = paramName == "" ? 100 : mParamInfo[paramName].mChartMax;
                mCharts[row.Index].Update();
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadParams();
        }

        private void dataGridViewParam_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            var ec = dataGridViewParam.EditingControl as DataGridViewComboBoxEditingControl;
            if (ec != null) ec.DroppedDown = true;
        }
        private void dataGridViewParam_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (dataGridViewParam.CurrentCell.ColumnIndex == 0 && e.Control is ComboBox)
            {
                var cb = e.Control as ComboBox;
                cb.SelectedIndexChanged -= LoadParamEvent;
                cb.SelectedIndexChanged += LoadParamEvent;
            }
        }

        private void LoadParamEvent(object sender, EventArgs e)
        {
            var cell = dataGridViewParam.CurrentCellAddress;
            var cb = sender as DataGridViewComboBoxEditingControl;
            LoadParam(dataGridViewParam.Rows[cell.Y], cb.EditingControlFormattedValue?.ToString());
        }

        private void buttonLoadROM_Click(object sender, EventArgs e)
        {
            OpenFileDialog fd = new OpenFileDialog { Filter = "rom files (*.bin,*.hex,*.srf)|*.bin;*.hex;*.srf|all files (*.*)|*.*", Title = "Load ROM"  };

            if (fd.ShowDialog() == DialogResult.OK)
            {
                var romPath = fd.FileName;
                fd = new OpenFileDialog { Filter = "xml files (*.xml)|*.xml|all files (*.*)|*.*", Title = "Load EcuFlash Metadata XML", DefaultExt = "xml" };

                if (fd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        mMetadataXml = new XmlDocument();
                        mMetadataXml.Load(fd.FileName);

                        mROMFile = System.IO.File.ReadAllBytes(romPath);

                        // drop SRF format header
                        int headerSize = mROMFile.Length % 0x1000;
                        if (System.IO.Path.GetExtension(romPath).ToLower() == ".srf" && headerSize != 0)
                        {
                            Buffer.BlockCopy(mROMFile, headerSize, mROMFile, 0, mROMFile.Length - headerSize);
                            mROMFile = mROMFile.Take(mROMFile.Length - headerSize).ToArray();
                        }

                        LoadTables();
                    }
                    catch (Exception x)
                    {
                        MessageBox.Show("error: ROM " + romPath + " Metadata " + fd.FileName + " " + x.ToString());

                        mMetadataXml = null;
                        mROMFile = null;
                    }
                }
            }
        }

        private void LoadTables()
        {
            foreach (XmlNode tableNode in mMetadataXml.SelectNodes("/rom/table"))
            {
                if (tableNode.ChildNodes.Count == 2 && tableNode.ChildNodes[0].Attributes["name"].InnerText == "Engine Load" && tableNode.ChildNodes[1].Attributes["name"].InnerText == "RPM")
                {
                    comboBox2.Items.Add(tableNode.Attributes["name"].InnerText);
                }
            }

            foreach (var tableItem in comboBox2.Items) {
                var width = Convert.ToInt32(comboBox2.CreateGraphics().MeasureString(tableItem.ToString(), comboBox2.Font).Width);
                comboBox2.DropDownWidth = Math.Max(comboBox2.DropDownWidth, width);
            }
            if (comboBox2.Items.Count > 0)
            {
                comboBox2.SelectedIndex = 0;
                LoadTable();
            }
        }

        private void LoadTable()
        {

        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadTable();
        }
    }
}
