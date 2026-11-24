namespace Pamor.Core
{
    /// <summary>The six Commanders from the GDD. Each has one passive and one active paid with Tenaga Dalam.</summary>
    public enum Commander { Sangkuriang, SiPitung, CalonArang, JakaTingkir, Sawerigading, LutungKasarung }

    /// <summary>Names, text, costs and targeting for Commanders (docs/GDD.md "Commander"; docs/DECISIONS.md for the line-board reading).</summary>
    public static class CommanderBook
    {
        public static readonly Commander[] All =
            { Commander.Sangkuriang, Commander.SiPitung, Commander.CalonArang, Commander.JakaTingkir, Commander.Sawerigading, Commander.LutungKasarung };

        /// <summary>Own turns before a Commander active can be used again (the GDD sets none; chosen for balance).</summary>
        public const int Cooldown = 3;
        public const int SangkuriangBlockers = 2;
        public const int KerbauReach = 3;

        public static string Name(Commander c)
        {
            switch (c)
            {
                case Commander.SiPitung: return "Si Pitung";
                case Commander.CalonArang: return "Calon Arang";
                case Commander.JakaTingkir: return "Jaka Tingkir";
                case Commander.LutungKasarung: return "Lutung Kasarung";
                default: return c.ToString();
            }
        }

        public static string Origin(Commander c)
        {
            switch (c)
            {
                case Commander.SiPitung: return "Betawi";
                case Commander.CalonArang: return "Bali";
                case Commander.JakaTingkir: return "Jawa";
                case Commander.Sawerigading: return "Bugis";
                default: return "Sunda";
            }
        }

        public static string Passive(Commander c)
        {
            switch (c)
            {
                case Commander.Sangkuriang: return "Di Fase Susun boleh menaruh 2 penghalang di titik tengah.";
                case Commander.SiPitung: return "Tiap bidak yang dimakan mencuri 1 Tenaga Dalam lawan.";
                case Commander.CalonArang: return "Santet lebih murah 1.";
                case Commander.JakaTingkir: return "Tiap Pendekar selamat dari serangan pertama yang mengenainya.";
                case Commander.Sawerigading: return "Bidak di baris pinggir bergerak 1 titik lebih jauh.";
                default: return "Dukun melindungi 2 bidak.";
            }
        }

        public static string ActiveName(Commander c)
        {
            switch (c)
            {
                case Commander.Sangkuriang: return "Perahu Semalam";
                case Commander.SiPitung: return "Bayangan Pitung";
                case Commander.CalonArang: return "Teluh Massal";
                case Commander.JakaTingkir: return "Kerbau Ngamuk";
                case Commander.Sawerigading: return "Layar Pinisi";
                default: return "Wujud Asli";
            }
        }

        public static int ActiveCost(Commander c)
        {
            switch (c)
            {
                case Commander.Sangkuriang: return 6;
                case Commander.SiPitung: return 4;
                case Commander.CalonArang: return 7;
                case Commander.JakaTingkir: return 5;
                case Commander.Sawerigading: return 5;
                default: return 6;
            }
        }

        public static string ActiveDescription(Commander c)
        {
            switch (c)
            {
                case Commander.Sangkuriang: return "Pilih titik kosong: tembok 3 titik (titik itu dan atas-bawahnya) selama 2 giliran lawan.";
                case Commander.SiPitung: return "Telik Sandi yang sudah ketahuan bersembunyi lagi.";
                case Commander.CalonArang: return "3 bidak lawan acak tidak bisa bergerak 1 giliran.";
                case Commander.JakaTingkir: return "Pilih bidakmu lalu arah: menerjang lurus sampai 3 titik, memakan semua lawan di jalurnya.";
                case Commander.Sawerigading: return "Pindahkan 2 bidakmu ke sisi papan seberang (atas ke bawah, bawah ke atas).";
                default: return "1 Prajurit langsung naik jadi Dam.";
            }
        }

        /// <summary>How many targets the player picks (in order) for the active; 0 = none.</summary>
        public static int TargetCount(Commander c)
        {
            switch (c)
            {
                case Commander.CalonArang: return 0;
                case Commander.JakaTingkir: return 2; // the piece, then where its charge ends
                case Commander.Sawerigading: return 2; // two pieces
                default: return 1;
            }
        }

        public static string TargetPrompt(Commander c, int step)
        {
            switch (c)
            {
                case Commander.Sangkuriang: return "PILIH TITIK UNTUK TEMBOK";
                case Commander.SiPitung: return "PILIH TELIK SANDI";
                case Commander.JakaTingkir: return step == 0 ? "PILIH BIDAK YANG MENERJANG" : "PILIH UJUNG TERJANGAN";
                case Commander.Sawerigading: return step == 0 ? "PILIH BIDAK PERTAMA" : "PILIH BIDAK KEDUA";
                default: return "PILIH PRAJURIT";
            }
        }
    }
}
