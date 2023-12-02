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


namespace resPack
{


    internal class adResFile
    {
        internal class adResFileMeta
        {
            public int Type;
            public short unknown;
            public short unknown1;

            private void Read(bgReader rd)
            {
                Type = rd.ReadInt32BE();
                unknown = rd.ReadInt16BE();
                unknown1 = rd.ReadInt16BE();
            }

            public static adResFileMeta CreateFromStream(bgReader rd)
            {
                var newOBJ = new adResFileMeta();
                newOBJ.Read(rd);
                return newOBJ;
            }
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
            public string[] Strings;
            public adResResidency Residency;
            public byte[] Data;
           
        }

        public const ulong RES_MAGIC = 0x7265730A07000300;
        public const int ASSET_SDTA = 0x73647461;
        public const int ASSET_STRG = 0x73747267;
        public const int ASSET_WAVE = 0x77617665;
        public const int ASSET_BODY = 0x626F6479;
        public const int ASSET_SURFACE = 0x73757266;


        int mramBufferOffset;
        int mramBufferLength;
        int aramBufferOffset;
        int aramBufferLength;       
        int stringTableOffset;
        int assetIndexOffset;
        int toEOF;
        long stringBufferOffset;

        public adResFileMeta[] Meta;
        public adResAsset[] Assets;

        private void Read(bgReader rd)
        {
            if (rd.ReadUInt64BE() != RES_MAGIC)
                throw new InvalidDataException($"Not a RES file! MAGIC!=0x{RES_MAGIC:X}");
            mramBufferOffset = rd.ReadInt32BE();
            mramBufferLength = rd.ReadInt32BE();
            aramBufferOffset = rd.ReadInt32BE();
            aramBufferLength = rd.ReadInt32BE();
            stringTableOffset = rd.ReadInt32BE();
            assetIndexOffset = rd.ReadInt32BE();
            toEOF = rd.ReadInt32BE();

            var metaCount = rd.ReadInt32BE();
            Meta = new adResFileMeta[metaCount];

            for (int i = 0; i < metaCount; i++)
                Meta[i] = adResFileMeta.CreateFromStream(rd);

         

            /* Initialize StringBuffer */
            //stringBufferOffset = stringTableOffset; // Has to be initialized by STRG asset, unfortunately.

            /* Load Assets */
            rd.BaseStream.Position = assetIndexOffset;
            loadAssetTable(rd);


            if (stringBufferOffset  <= 0) // We found no STRG offset. 
                return;

            stringBufferOffset += mramBufferOffset; // Stringtable is in MRAM

            Console.WriteLine($"mbo: 0x{mramBufferOffset:X} 0x{mramBufferLength:X}\tabo: 0x{aramBufferOffset:X} 0x{aramBufferLength:X}\nsto: 0x{stringTableOffset:X}\naio: 0x{assetIndexOffset:X}\nasto: 0x{stringTableOffset:X}");
            /* Load Strings */
            for (int i=0; i < Assets.Length; i++)
            {
                var asset = Assets[i];
                Console.WriteLine("AA " + Program.i32tostring(asset.Hash));
                for (int x = 0; x < asset.Strings.Length; x++) 
                    if (asset.Hash==ASSET_BODY || asset.Hash== ASSET_WAVE || asset.Hash == ASSET_SURFACE)
                    {
                        asset.Strings[x] = readNextString(rd);
                        if (asset.Hash == ASSET_WAVE)
                            break;
                    }
                  
                if (asset.Strings.Length > 0)
                    asset.Name = asset.Strings[0];
            }
        }


        public static adResFile CreateFromStream(bgReader rd)
        {
            var newOBJ = new adResFile();
            newOBJ.Read(rd);
            return newOBJ;
        }

        private void loadAssetTable(bgReader rd)
        {
            var count = rd.ReadInt32BE();


            Assets = new adResAsset[count];
            
            for (int i = 0; i < count; i++)
            {
                var unknown = rd.ReadInt32BE();
                var hash = rd.ReadInt32BE();
                var offset = rd.ReadInt32BE();
                var length = rd.ReadInt32BE();
                var stringCount = rd.ReadInt32BE();

                // We want to know where the stringtable is!
                if (hash == ASSET_STRG)
                    stringBufferOffset = offset;

                var nAsset = new adResAsset()
                {
                    Hash = hash,
                    Strings = new string[stringCount]
                };

                rd.PushAnchor();
                
                    if (hash == ASSET_SDTA)
                        offset += aramBufferOffset;
                    else
                        offset += mramBufferOffset;
                rd.BaseStream.Position = offset;
                nAsset.Data = rd.ReadBytes(length);
                rd.PopAnchor(); 
                Assets[i] = nAsset;
            }
            
        }

        private string readNextString(bgReader rd)
        {
            string str = "";
            rd.PushAnchor(); // store current position
                rd.BaseStream.Position = stringBufferOffset;
                str = rd.ReadTerminatedString();
                rd.Align(4, BGAlignDirection.FORWARD);
                stringBufferOffset = rd.BaseStream.Position; // Store new aligned position.
            Console.WriteLine(str);
            rd.PopAnchor(); // go back to old position.
            return str;            
        }
    }
}
