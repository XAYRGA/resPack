// See https://aka.ms/new-console-template for more information


namespace resPack
{
    static class Program
    {
        static void Main(string[] args)
        {
            var gggggg = File.OpenRead(args[0]);
            var gw = new xayrga.byteglider.bgReader(gggggg);
            var file = adResFileLE.CreateFromStream(gw);

            Directory.CreateDirectory("out");
            
            for (int i=0; i < file.Assets.Length; i++)
            {
                var asset = file.Assets[i];
                var filePath = $"{asset.Name}.{i32tostringLE(asset.Hash)}";

                var folder = Path.GetDirectoryName(filePath);

                Console.WriteLine(filePath);

                Directory.CreateDirectory($"out/{folder}");
                File.WriteAllBytes($"out/{filePath}", file.Assets[i].Data);
            }

        }

        public static string i32tostring(int value)
        {
            var str = "";
            for (int i=0; i < 4; i++)
            {
                var wShft = value >> ((3 - i) * 8);
                str +=   (char)((byte)wShft & 0xFF);         
            }            
            return str;
        }

        public static string i32tostringLE(int value)
        {
            var str = "";
            for (int i = 0; i < 4; i++)
            {
                var wShft = value >> i * 8;
                str += (char)((byte)wShft & 0xFF);
            }
            return str;
        }
    }
}