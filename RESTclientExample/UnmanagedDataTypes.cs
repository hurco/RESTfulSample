


using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Runtime.InteropServices;

/**
 * These Data types are the marshalable types used to transfer bulk data from winmax to the Wcf Service 
 * and eventually to the clients of the Wcf service, these tpyes MUST be pack=1 and order sequential, 
 * the variables must be the same size (in bytes) and in the same order as the unmanaged types
 * 
 * There are several examples here for types consisting of simple primitives, arrays and also other structs
 * the marshalas attribute allows for types other than primitives to marshal correctly so pay attention to it's usage
 * 
 * */

namespace WcfDataServices
{
    public static class UnmanagedDataTypes
    {
        
        /// <summary>
        /// copied from unmanaged code, these #defines will porbably need to be tweaked so this actually builds correctly and functions for BBQ
        /// </summary>
        const int COMM_STRING_MAX_SIZE = 200;
        enum LogicalAxisType
        {
            X_AXIS = 0,
            Y_AXIS = 1,
            Z_AXIS = 2,
            S_AXIS = 3,
            A_AXIS = 4,
            B_AXIS = 5,
            C_AXIS = 6,
            U_AXIS = 7,
            V_AXIS = 8,
            W_AXIS = 9,
            S2_AXIS = 10,
            A2_AXIS = 11,
            B2_AXIS = 12,
            C2_AXIS = 13,
            S3_AXIS = 14,
            MAX_LOGICAL_AXIS = 15,
            NO_AXIS = 16,
        }

        private const int MAX_LOGICAL_AXIS = (int)LogicalAxisType.MAX_LOGICAL_AXIS;

        // motion has twice as many machine axes, which are all generic, up to AXn
        enum PhysicalAxisType
        {
            AX0 = 0,
            AX1 = 1,
            AX2 = 2,
            AX3 = 3,
            AX4 = 4,
            AX5 = 5,
            AX6 = 6,
            AX7 = 7,
            AX8 = 8,
            AX9 = 9,
            AX10 = 10,
            AX11 = 11,
            AX12 = 12,
            AX13 = 13,
            AX14 = 14,
            AX15 = 15,
            AX16 = 16,
            AX17 = 17,
            AX18 = 18,
            AX19 = 19,
            AX20 = 20,
            AX21 = 21,
            AX22 = 22,
            AX23 = 23,
            AX24 = 24,
            AX25 = 25,
            AX26 = 26,
            AX27 = 27,
            AX28 = 28,
            AX29 = 29,
            AX30 = 30,
            AX31 = 31,
            MAX_PHYSICAL_AXIS = 32,
        }

        private const byte MAX_KIN_ORDER = MAX_LOGICAL_AXIS;
        [StructLayout(LayoutKind.Sequential, Pack = 1)] // a struct to pass to and from unmanaged code
        [DataContract]
        public struct BulkShutdownWinmaxType
        {
            [DataMember]
            public bool bRestart;
            [DataMember]
            public bool bUserConfirm;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)] // a struct to pass to and from unmanaged code
        [DataContract]
        public struct BulkPowerType
        {
            [DataMember]
            public bool bEstopPressed;         // B97:0 bit 0
            [DataMember]
            public bool bControlPowerOn;       // B97:0 bit 1
            [DataMember]
            public bool bServoPowerOn;         // B97:0 bit 2
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)] // a struct to pass to and from unmanaged code
        [DataContract]
        public struct BulkUserMsgDisplayType
        {
            [DataMember]
            int dwMsgId;
            [DataMember]
            int dwAckSID;
            [DataMember]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] //specifying the array length to the marshaller is important
            double[] dMsgData;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)] // a struct to pass to and from unmanaged code
        [DataContract]
        public struct BulkUserMsgClearType
        {
            [DataMember]
            int dwMsgId;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)] // a struct to pass to and from unmanaged code

        public struct BulkToolChangeInfoType
        {
            public void Init()
            {
                this.bAutoToolChangeCouldMove = new bool[UnmanagedDataTypes.MAX_LOGICAL_AXIS]; //used to guarentee enough space to unmarshal the array from unmanaged code
                this.bManualToolChangeCouldMove = new bool[UnmanagedDataTypes.MAX_LOGICAL_AXIS];
                this.bAutoSafetyCouldMove = new bool[UnmanagedDataTypes.MAX_LOGICAL_AXIS];
                this.bMoveToZone1CouldMove = new bool[UnmanagedDataTypes.MAX_LOGICAL_AXIS];
                this.bMoveToZone2CouldMove = new bool[UnmanagedDataTypes.MAX_LOGICAL_AXIS];
                this.bPalletChangeCouldMove = new bool[UnmanagedDataTypes.MAX_LOGICAL_AXIS];
                this.bTPSInsertToolCouldMove = new bool[UnmanagedDataTypes.MAX_LOGICAL_AXIS];
                this.bTPSRemoveToolCouldMove = new bool[UnmanagedDataTypes.MAX_LOGICAL_AXIS];
                this.bManualSafetyCouldMove = new bool[UnmanagedDataTypes.MAX_LOGICAL_AXIS];
            }
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = UnmanagedDataTypes.MAX_LOGICAL_AXIS)] //specifying the array length to the marshaller is important
            public bool[] bAutoToolChangeCouldMove;      // B97:25 bits 0-7
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = UnmanagedDataTypes.MAX_LOGICAL_AXIS)]
            public bool[] bManualToolChangeCouldMove;    // B97:25 bits 8-15
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = UnmanagedDataTypes.MAX_LOGICAL_AXIS)]
            public bool[] bAutoSafetyCouldMove;          // B97:26 bits 0-7
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = UnmanagedDataTypes.MAX_LOGICAL_AXIS)]
            public bool[] bMoveToZone1CouldMove; // B97:26 bits 8-15
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = UnmanagedDataTypes.MAX_LOGICAL_AXIS)]
            public bool[] bMoveToZone2CouldMove;     // B97:27 bits 0-7
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = UnmanagedDataTypes.MAX_LOGICAL_AXIS)]
            public bool[] bPalletChangeCouldMove;     // B97:27 bits 8-15
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = UnmanagedDataTypes.MAX_LOGICAL_AXIS)]
            public bool[] bTPSInsertToolCouldMove;      // B97:28 bits 0-7
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = UnmanagedDataTypes.MAX_LOGICAL_AXIS)]
            public bool[] bTPSRemoveToolCouldMove;        // B97:28 bits 8-15
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = UnmanagedDataTypes.MAX_LOGICAL_AXIS)]
            public bool[] bManualSafetyCouldMove;      // B97:29 bits 0-7
        }


        /// <summary>
        /// CRITICAL class for transfer of bulks from COMM, allows us to unmarshal the actual data to the correct struct while not having multiple endpoints in COMM
        /// the short needs to describe the specific struct so that it can then be unmarshaled and packed for delivery to the client
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BoxedBulk
        {
            public void Init()
            {
                this.data = new byte[65536];
            }
            public uint type;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 65536)]
            public byte[] data;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        [DataContract]
        public struct BulkNotificationDataType
        {
            public void Init()
            {
                this.m_wrcFileName = new byte[COMM_STRING_MAX_SIZE * 2];//wchar
                this.m_Msg = new byte[COMM_STRING_MAX_SIZE * 2];

            }
            [DataMember]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = COMM_STRING_MAX_SIZE * 2)]
            public byte[] m_wrcFileName;
            [DataMember]
            public ushort m_NotificationIndex;
            //use byte array as wchar_t is 2 bytes long, but attempts to marshal as string or char were unsuccessful, the byte array can be decoded with encoding.unicode.getstring
            [DataMember]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = COMM_STRING_MAX_SIZE * 2)]
            public byte[] m_Msg;
            [DataMember]
            public int m_MsgType;
            [DataMember]
            _SYSTEMTIME m_Timestamp; //complex type of inner struct
        }

        /// <summary>
        /// this struct isn't marshalled directly but rather is a component of another struct
        /// </summary>
        /// 
        [DataContract]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct _SYSTEMTIME
        {
            [DataMember]
            public ushort wYear;
            [DataMember]
            public ushort wMonth;
            [DataMember]
            public ushort wDayOfWeek;
            [DataMember]
            public ushort wDay;
            [DataMember]
            public ushort wHour;
            [DataMember]
            public ushort wMinute;
            [DataMember]
            public ushort wSecond;
            [DataMember]
            public ushort wMilliseconds;
        }
        [DataContract]
        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        public struct RemoteCommandInfoType
        {
            public void Init()
            {
                this.dValue = new double[10];//wchar
                this.sValue = new byte[1000];
            }
            [DataMember]
            [FieldOffset(0)]
            public uint dwCmdId;
            [DataMember]
            [FieldOffset(8)]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public double[] dValue;
            [DataMember]
            [FieldOffset(88)]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1000)] //5x200
            public byte[] sValue;
            [DataMember]
            [FieldOffset(1092)]
            public uint dwCmdCompleteSid;

        }

        [DataContract]
        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
        public struct BulkToolDataType
        {
            [DataMember]
            public int ToolNumber;
            [DataMember]
            public int tooldatalength;
            [DataMember]
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 200)]
            public String Ack;
            [DataMember]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
            public byte[] tooldata; //1KB for starters might need to make as large as 64KB
        };

        [DataContract]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BulkMachinePosType
        {
            [DataMember]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_LOGICAL_AXIS)]
            public double[] dMachinePosition;
        }

        [DataContract]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BulkCurrentPartSetupType
        {
            [DataMember]
            BulkMachinePosType offsets;
            [DataMember]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            double[] A_Centerline;
            [DataMember]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            double[] B_Centerline;
            [DataMember]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            double[] C_Centerline;
            [DataMember]
            double XYSkewAngle;
        }
    }

}
