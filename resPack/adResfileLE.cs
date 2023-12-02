

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


/* 
 * 
 * #pragma endian big

struct RESFILESectType {
    u32 hash;
    u16 unk;
    u16 unk2;
};


struct INDEXTableEntry { // structure is valid, function is not. don't know yet. 
    u32 unk;
    u32 hash;
    u16 unk1;
    u16 unk2;
    //u8 data[length] @ offset + parent.parent.dataClusterStart;
};

// This is referred to by the file format as an 'index'... Interesting. No clue what it's indexing.
struct IndexTable { // not a nametable. No idea yet. Structure is valid though.
    u32 count;
    u32 unknown;
    INDEXTableEntry entries[count];
};


struct ASSETIdx {
    u32 hash;
    u32 offset;;
    u32 length;
    // This is an odd way of doing things...
    u32 stringCount;
    // So, what happens is 

    /* 
        1. The STRG section is loaded into memory and parsed. 
            1a. The strings are null terminated 4 byte aligned strings. 
                00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F
            00	t  h  e  00 q  u  i  c  k  00 00 00 b  r  o  w    
            02  n  00 00 00 f  o  x  00 00 j  u  m  p  s  00 00
            03  o  v  e  r  00 00 00 x  a  y  r  00 00 00 00 00
            This means the next string to be loaded is aligned to 4 bytes. 

        2. Contents are dumped into a map.  
        3. Every time a section is loaded, it has a 'stringCount' attached to it.
        4. Strings are 'consumed' out of the map, advancing the current index by 'stringCount' values.    	
    */
/*
u32 nameOffset; // not name offset
if (hash != 0x73647461)
{ // 'sdat' -- maybe this is different because audio data on the GC is dumped into ARAM?
u8 data[length] @ offset + parent.parent.dataClusterStart;
}
else
{
u8 data[length] @ offset + parent.parent.vBufOffset - 1; // what the fuck? 
}
};

struct ASSETTable
{
    u32 count;
    u32 unknown;
    ASSETIdx Assets[count];
};


struct RESFILE
{
    u64 magic;
    u32 dataClusterStart;
    u32 endOfStringtable;
    u32 vBufOffset;
    u32 vBufLength;
    u32 NameTableOffset;
    u32 AssetIndexOffset;
    u32 LengthToEof;
    u32 sectionTypeCount;
    RESFILESectType sections[sectionTypeCount];


    IndexTable NameTable @ (NameTableOffset + dataClusterStart); 
    ASSETTable AssetTable @ (AssetIndexOffset);    
};

RESFILE root @ 0x00;


*/

using xayrga;
using xayrga.byteglider;
using System.IO.Compression;


namespace resPack
{

    internal class adResFileLE
    {
        internal class adResFileMeta
        {
            public int Type;
            public short unknown;
            public short unknown1;

            private void Read(bgReader rd)
            {
                Type = rd.ReadInt32();
                unknown = rd.ReadInt16();
                unknown1 = rd.ReadInt16();
            }

            public static adResFileMeta CreateFromStream(bgReader rd)
            {
                var newOBJ = new adResFileMeta();
                newOBJ.Read(rd);
                return newOBJ;
            }
        }


        internal class adResNamedIndex
        {
            public string Name;
            public int Hash;
            public long Offset;
        }

        internal enum adResResidency
        {
            MRAM = 0,
            ARAM = 1
        }

        internal class adResAsset
        {
            public string Name;
            public int Hash;
            public int Flags;
            public adResResidency Residency;
            public byte[] Data;
            public int Offset;

        }

        public const ulong RES_MAGIC = 0x7265730A07000300;
        public const int ASSET_SDTA = 0x61746473;
        public const int ASSET_STRG = 0x67727473;
        public const int ASSET_SURF = 0x66727573;
        public const int ASSET_GSHD = 0x64687367;
        public const int ASSET_BODY = 0x79646F62;
        public const int ASSET_WAVE = 0x65766177;
        public const int ASSET_LUA =  0x2161756C;
        public const int ASSET_INDX = 0x78646E69;

        public const int ZLIB = 0x42494C5A;


        int mramBufferOffset;
        int mramBufferLength;
        int aramBufferOffset;
        int aramBufferLength;
        int stringTableOffset;
        int assetListOffset;
        int toEOF;

        long stringBufferOffset;
        long indexBufferOffset; 

        public adResFileMeta[] Meta;
        public adResAsset[] Assets;
        private adResNamedIndex[] NamedIndex;

        private void Read(bgReader rd)
        {

            var mgc = rd.ReadUInt64();
            // if (mgc!= RES_MAGIC)
            //    throw new InvalidDataException($"Not a RES file! {mgc:X}!=0x{RES_MAGIC:X}");
            mramBufferOffset = rd.ReadInt32();
            mramBufferLength = rd.ReadInt32();
            aramBufferOffset = rd.ReadInt32();
            aramBufferLength = rd.ReadInt32();
            stringTableOffset = rd.ReadInt32();
            assetListOffset = rd.ReadInt32();
            toEOF = rd.ReadInt32();

            var metaCount = rd.ReadInt32();
            Meta = new adResFileMeta[metaCount];

            for (int i = 0; i < metaCount; i++)
                Meta[i] = adResFileMeta.CreateFromStream(rd);


            /* Initialize StringBuffer */
            //stringBufferOffset = stringTableOffset; // Has to be initialized by STRG asset, unfortunately.

            /* Load Assets */
            rd.BaseStream.Position = assetListOffset;
            loadAssetTable(rd);


            if (stringBufferOffset <= 0) // We found no STRG offset. 
                return;

            if (indexBufferOffset <= 0 )
                return;

            indexBufferOffset += mramBufferOffset;
            stringBufferOffset += mramBufferOffset; // Stringtable is in MRAM

            rd.BaseStream.Position = indexBufferOffset;

            loadIndexTable(rd);


            Console.WriteLine($"mbo: 0x{mramBufferOffset:X} 0x{mramBufferLength:X}\tabo: 0x{aramBufferOffset:X} 0x{aramBufferLength:X}\nsto: 0x{stringTableOffset:X}\naio: 0x{assetListOffset:X}\nasto: 0x{stringTableOffset:X}\tidto: 0x{indexBufferOffset:X}");


            /* Process Assets */

            
            for (int i = 0; i < Assets.Length; i++)
            {
                var asset = Assets[i];
                var preread = 0l;
              


                //Console.WriteLine(asset.Name);
                asset.Name = $"GENERIC_{i:D4}";
                for (int x = 0; x < NamedIndex.Length; x++)
                    if (NamedIndex[x].Offset == asset.Offset)
                        asset.Name = NamedIndex[x].Name;

                switch (asset.Hash)
                {
                    case ASSET_WAVE:
                        rd.BaseStream.Position = asset.Offset;
                        asset.Name = getStringPointerValue(rd);
                        break;
                    case ASSET_LUA:
                        rd.BaseStream.Position = asset.Offset;
                        var fullSize = rd.ReadInt32();
                        rd.ReadUInt32(); // FOUR.
                        if (rd.ReadInt32() == ZLIB)
                        {
                            var UncompressedSize = rd.ReadInt32();
                            var data = new byte[UncompressedSize];
                            var ww = new ZLibStream(rd.BaseStream, CompressionMode.Decompress);
                            ww.Read(data);
                            asset.Data = data;
                        }

                        break;           
                    default:

                        break;

                }
            }
        }


        public static adResFileLE CreateFromStream(bgReader rd)
        {
            var newOBJ = new adResFileLE();
            newOBJ.Read(rd);
            return newOBJ;
        }


        private void loadAssetTable(bgReader rd)
        {
            var count = rd.ReadInt32();

            Assets = new adResAsset[count];
            for (int i = 0; i < count; i++)
            {
                var unknown = rd.ReadInt32();
                var hash = rd.ReadInt32();
                var offset = rd.ReadInt32();
                var length = rd.ReadInt32();
                var flags = rd.ReadInt32();

                // We want to know where the stringtable is!
                if (hash == ASSET_STRG)
                    stringBufferOffset = offset;
                else if (hash == ASSET_INDX)
                    indexBufferOffset = offset;

                var nAsset = new adResAsset()
                {
                    Hash = hash,
                    Flags = flags,
                };

                rd.PushAnchor();
                    if (hash == ASSET_SDTA)
                        offset += aramBufferOffset;
                    else
                        offset += mramBufferOffset;

                    rd.BaseStream.Position = offset;
                    nAsset.Offset = offset;
                    nAsset.Data = rd.ReadBytes(length);
                rd.PopAnchor();
                Assets[i] = nAsset;
            }
        }

        private void loadIndexTable(bgReader rd)
        {
            int count = rd.ReadInt32();
            rd.ReadUInt32(); // FOUR!?
            NamedIndex = new adResNamedIndex[count];
            for (int i = 0; i < count; i++)
            {
                var nni = new adResNamedIndex()
                {
                    Name = getStringPointerValue(rd),
                    Hash = rd.ReadInt32()
                };

                var cOffs = rd.BaseStream.Position;
                nni.Offset = cOffs + rd.ReadInt32();
                NamedIndex[i] = nni;
                //Console.WriteLine($"of {nni.Name} 0x{nni.Offset:X}");
            }
        }

        private string getStringPointerValue(bgReader rd)
        {
            string str = "";
            var origPos = rd.BaseStream.Position;
                
            var offset = rd.ReadInt32();
            rd.PushAnchor(); // store current position
                rd.BaseStream.Position = origPos + offset;
                str = rd.ReadTerminatedString('\x00');
            rd.PopAnchor(); // go back to old position.
            return str;
        }
    }
}


