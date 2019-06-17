using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;


namespace WcfDataService
{
    /// <summary>
    /// ROOT class for all bulk wrappers, this type is CRITICAL, all bulk wrapper types to be visible on the client side MUST inherit this class.
    /// </summary>
    [DataContract(Namespace = "WcfDataServices")]
    [KnownType("GetTypes")]
    public class Bulk
    {
        public static Type[] GetTypes()
        {
            Type[] value = System.Reflection.Assembly.GetAssembly(typeof(Bulk)).GetTypes().Where(x => x.IsSubclassOf(typeof(Bulk))).ToArray<Type>();
            return value;
        }
    }

    /// <summary>
    /// wrapper for BulkToolChangeInfoType
    /// </summary>
    [DataContract(Namespace = "WcfDataServices")]
    public class BulkToolChangeInfoTypeBox : Bulk
    {
      [DataMember]
      public WcfDataServices.UnmanagedDataTypes.BulkToolChangeInfoType BulkStruct;
    }

    /// <summary>
    /// wrapper for BulkNotificationDataType
    /// </summary>
    [DataContract(Namespace = "WcfDataServices")]
    public class BulkNotificationDataTypeBox : Bulk
    {
      [DataMember]
      public WcfDataServices.UnmanagedDataTypes.BulkNotificationDataType BulkStruct;

      
    }

    /// <summary>
    /// wrapper for BulkNotificationDataType
    /// </summary>
    [DataContract(Namespace = "WcfDataServices")]
    public class BulkRemoteCommandDataTypeBox : Bulk
    {
      [DataMember]
      public WcfDataServices.UnmanagedDataTypes.RemoteCommandInfoType BulkStruct;


    }

    /// <summary>
    /// wrapper for BulkMachinePosDataType
    /// </summary>
    [DataContract(Namespace = "WcfDataServices")]
    public class BulkMachinePosTypeBox : Bulk
    {
      [DataMember]
      public WcfDataServices.UnmanagedDataTypes.BulkMachinePosType BulkStruct;
    }

    /// <summary>
    /// wrapper for BulkToolDataDataType
    /// </summary>
    [DataContract(Namespace = "WcfDataServices")]
    public class BulkToolDataTypeBox : Bulk
    {
      [DataMember]
      public WcfDataServices.UnmanagedDataTypes.BulkToolDataType BulkStruct;
    }

    /// <summary>
    /// wrapper for BulkShutdownWinmaxType
    /// </summary>
    [DataContract(Namespace = "WcfDataServices")]
    public class BulkShutdownWinmaxTypeBox : Bulk
    {
        [DataMember]
        public WcfDataServices.UnmanagedDataTypes.BulkShutdownWinmaxType BulkStruct;
    }

    /// <summary>
    /// wrapper for BulkPartSetupType
    /// </summary>
    [DataContract(Namespace = "WcfDataServices")]
    public class BulkPartSetupTypeBox : Bulk
    {
      [DataMember]
      public WcfDataServices.UnmanagedDataTypes.BulkCurrentPartSetupType BulkStruct;
    }
	
    /// <summary>
    /// wrapper for BulkMillNCVariable
    /// </summary>
    [DataContract(Namespace = "WcfDataServices")]
    public class BulkMillNCVariableBox : Bulk
    {
        [DataMember]
        public WcfDataServices.UnmanagedDataTypes.BulkMillNCVariable BulkStruct;
    }

    /// <summary>
    /// wrapper for BulkLoadedPrograms
    /// </summary>
    [DataContract(Namespace = "WcfDataServices")]
    public class BulkLoadedProgramsBox : Bulk
    {
        [DataMember]
        public WcfDataServices.UnmanagedDataTypes.BulkLoadedPrograms BulkStruct;
    }
	
    /// <summary>
    /// base transer class.
    /// </summary>
    [DataContract(Namespace = "WcfDataServices")]
    [KnownType("GetTypes")]
    public class BulkWrapper
    {
        //[DataMember]
        //public System.Type bulktype { get; set; }

        [DataMember]
        public Bulk bulk { get; set; }

        public static Type[] GetTypes()
        {
            Type[] value = System.Reflection.Assembly.GetAssembly(typeof(Bulk)).GetTypes().Where(x => x.IsSubclassOf(typeof(Bulk))).ToArray<Type>();
            return value;
        }
    }

    [DataContract(Namespace = "WcfDataServices")]
    
    public class GetBulkParams
    {
        [DataMember]
        public string SID { get; set; }

        
    }
      [DataContract(Namespace = "WcfDataServices")]
    
    public class SetBulkParams
    {
        [DataMember]
        public string SID { get; set; }
        [DataMember]
        public BulkWrapper Value { get; set; }
        
    }
  
}
