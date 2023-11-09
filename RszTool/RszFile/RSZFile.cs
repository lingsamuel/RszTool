namespace RszTool
{
    public class RSZFile : BaseRszFile
    {
        public struct Header
        {
            public uint magic;
            public uint version;
            public uint objectCount;
            public uint instanceCount;
            public ulong userdataCount;
            public ulong instanceOffset;
            public ulong dataOffset;
            public ulong userdataOffset;
        }

        public struct ObjectTable
        {
            public uint instanceId;
        }

        public StructModel<Header> dataHeader = new();
        public List<StructModel<ObjectTable>> dataObjectTable = new();
        public List<InstanceInfo> dataInstanceInfo = new();
        public List<IRSZUserDataInfo> dataRSZUserDataInfo = new();

        public readonly List<RszInstance> instanceData = new();

        public RSZFile(RszHandler rszHandler) : base(rszHandler)
        {
        }

        public override bool Read()
        {
            var handler = FileHandler;
            if (!dataHeader.Read(handler)) return false;
            var rszParser = RszParser;

            for (int i = 0; i < dataHeader.Data.objectCount; i++)
            {
                StructModel<ObjectTable> objectTable = new();
                objectTable.Read(handler);
                dataObjectTable.Add(objectTable);
            }

            handler.Seek((long)dataHeader.Data.instanceOffset);
            for (int i = 0; i < dataHeader.Data.instanceCount; i++)
            {
                InstanceInfo instanceInfo = new();
                instanceInfo.Read(handler);
                instanceInfo.ReadClassName(rszParser);
                dataInstanceInfo.Add(instanceInfo);
            }

            handler.Seek((long)dataHeader.Data.userdataOffset);
            Dictionary<uint, int> distanceIdToIndex = new();
            if (RszHandler.TdbVersion < 67)
            {
                for (int i = 0; i < (int)dataHeader.Data.userdataCount; i++)
                {
                    RSZUserDataInfo_TDB_LE_67 rszUserDataInfo = new();
                    rszUserDataInfo.Read(handler);
                    rszUserDataInfo.ReadClassName(rszParser);
                    dataRSZUserDataInfo.Add(rszUserDataInfo);
                    distanceIdToIndex[rszUserDataInfo.instanceId] = i;
                }
                foreach (var item in dataRSZUserDataInfo)
                {
                    ((RSZUserDataInfo_TDB_LE_67)item).ReadEmbeddedRSZFile(RszHandler);
                }
            }
            else
            {
                for (int i = 0; i < (int)dataHeader.Data.userdataCount; i++)
                {
                    RSZUserDataInfo rszUserDataInfo = new();
                    rszUserDataInfo.Read(handler);
                    dataRSZUserDataInfo.Add(rszUserDataInfo);
                    distanceIdToIndex[rszUserDataInfo.instanceId] = i;
                }
            }

            // read instance data
            for (int i = 0; i < dataInstanceInfo.Count; i++)
            {
                RszClass? rszClass = RszParser.GetRSZClass(dataInstanceInfo[i].typeId);
                if (rszClass == null)
                {
                    Console.Error.WriteLine($"RszClass {dataInstanceInfo[i].typeId} not found!");
                    continue;
                }
                distanceIdToIndex.TryGetValue((uint)i, out int userDataIdx);
                RszInstance instance = new(rszClass, i, userDataIdx);
                instance.Read(handler);
                if (userDataIdx != -1)
                {
                    instance.RSZUserData = dataRSZUserDataInfo[userDataIdx];
                }
                instanceData.Add(instance);
            }
            return true;
        }

        public override bool Write()
        {
            var handler = FileHandler;
            if (!dataHeader.Write(handler)) return false;
            if (!dataObjectTable.Write(handler)) return false;
            // TODO write strings
            if (!dataInstanceInfo.Write(handler)) return false;
            if (!dataRSZUserDataInfo.Write(handler)) return false;
            return true;
        }
    }


    public class InstanceInfo : BaseModel
    {
        public uint typeId;
        public uint CRC;
        public string? ClassName { get; set; }

        public override bool Read(FileHandler handler)
        {
            if (!base.Read(handler)) return false;
            handler.Read(ref typeId);
            handler.Read(ref CRC);
            EndRead(handler);
            return true;
        }

        public override bool Write(FileHandler handler)
        {
            if (!base.Write(handler)) return false;
            handler.Write(typeId);
            handler.Write(CRC);
            return true;
        }

        public void ReadClassName(RszParser parser)
        {
            ClassName = parser.GetRSZClassName(typeId);
        }
    }

    public interface IRSZUserDataInfo : IModel
    {
        uint InstanceId { get; }
        uint TypeId { get; }
        string? ClassName { get; }

    }

    public class RSZUserDataInfo : BaseModel, IRSZUserDataInfo
    {
        public uint instanceId;
        public uint typeId;
        public ulong pathOffset;
        public string? path;

        public uint InstanceId => instanceId;
        public uint TypeId => typeId;
        public string? ClassName { get; set; }

        public override bool Read(FileHandler handler)
        {
            if (!base.Read(handler)) return false;
            handler.Read(ref instanceId);
            handler.Read(ref typeId);
            handler.Read(ref pathOffset);
            path = handler.ReadWString((long)pathOffset);
            EndRead(handler);
            return true;
        }

        public override bool Write(FileHandler handler)
        {
            if (!base.Write(handler)) return false;
            handler.Write(instanceId);
            handler.Write(typeId);
            handler.Write(pathOffset);
            return true;
        }

        public void ReadClassName(RszParser parser)
        {
            ClassName = parser.GetRSZClassName(typeId);
        }
    }

    public class RSZUserDataInfo_TDB_LE_67 : BaseModel, IRSZUserDataInfo
    {
        public uint instanceId;
        public uint typeId;
        public uint jsonPathHash;
        public uint dataSize;
        public ulong RSZOffset;

        public uint InstanceId => instanceId;
        public uint TypeId => typeId;
        public string? ClassName { get; set; }
        public RSZFile? EmbeddedRSZFile { get; set; }

        public override bool Read(FileHandler handler)
        {
            if (!base.Read(handler)) return false;
            handler.Read(ref instanceId);
            handler.Read(ref typeId);
            handler.Read(ref jsonPathHash);
            handler.Read(ref dataSize);
            handler.Read(ref RSZOffset);
            EndRead(handler);
            return true;
        }

        public override bool Write(FileHandler handler)
        {
            if (!base.Write(handler)) return false;
            handler.Write(instanceId);
            handler.Write(typeId);
            handler.Write(jsonPathHash);
            handler.Write(dataSize);
            handler.Write(RSZOffset);
            return true;
        }

        public void ReadClassName(RszParser parser)
        {
            ClassName = parser.GetRSZClassName(typeId);
        }

        public void ReadEmbeddedRSZFile(RszHandler rszHandler)
        {
            rszHandler.FileHandler.Seek((long)RSZOffset);
            EmbeddedRSZFile = new RSZFile(rszHandler);
            EmbeddedRSZFile.Read();
        }
    }
}