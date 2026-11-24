using System.Collections.Generic;

namespace Pamor.Core
{
    /// <summary>One ajian per role (the GDD's first version, no Ajian Pamungkas).</summary>
    public enum Ajian { TamengWaja, SaipiAngin, Panglimunan, Santet, GelarSupitUrang, Pancasona }

    /// <summary>Costs, owners and text for the ajian (docs/GDD.md, "Tenaga Dalam dan Ajian").</summary>
    public static class AjianBook
    {
        public const int MaxTenaga = 10;
        public const int TenagaPerTurn = 1;
        public const int TenagaPerCapture = 2;
        /// <summary>How far (in steps along lines) a Dukun's Santet reaches (GDD: radius 3).</summary>
        public const int SantetRange = 3;
        /// <summary>Prajurit within this many steps of the Senopati join Gelar Supit Urang (GDD: radius 2).</summary>
        public const int SupitUrangRange = 2;

        /// <summary>
        /// Own turns that must pass before the same ajian can be cast again (GDD "Detail cooldown"):
        /// a cooldown of 2 means the next two own turns can't cast it.
        /// </summary>
        public static int CooldownTurns(Ajian a)
        {
            switch (a)
            {
                case Ajian.TamengWaja: return 2;
                case Ajian.SaipiAngin: return 3;
                case Ajian.Panglimunan: return 4;
                case Ajian.Santet: return 3;
                case Ajian.GelarSupitUrang: return 5;
                default: return 0; // Pancasona: once per match
            }
        }

        /// <summary>Pancasona is a reaction: it's offered when a move is about to kill the Patih, not cast on your turn.</summary>
        public static bool IsReaction(Ajian a) => a == Ajian.Pancasona;

        public static Ajian For(Role role)
        {
            switch (role)
            {
                case Role.Pendekar: return Ajian.SaipiAngin;
                case Role.TelikSandi: return Ajian.Panglimunan;
                case Role.Dukun: return Ajian.Santet;
                case Role.Senopati: return Ajian.GelarSupitUrang;
                case Role.Patih: return Ajian.Pancasona;
                default: return Ajian.TamengWaja;
            }
        }

        public static int Cost(Ajian a)
        {
            switch (a)
            {
                case Ajian.TamengWaja: return 2;
                case Ajian.SaipiAngin: return 3;
                case Ajian.Panglimunan: return 3;
                case Ajian.Santet: return 4;
                case Ajian.GelarSupitUrang: return 5;
                default: return 8;
            }
        }

        public static bool NeedsTarget(Ajian a) => a == Ajian.Santet;

        public static string Name(Ajian a)
        {
            switch (a)
            {
                case Ajian.TamengWaja: return "Tameng Waja";
                case Ajian.SaipiAngin: return "Saipi Angin";
                case Ajian.Panglimunan: return "Panglimunan";
                case Ajian.Santet: return "Santet";
                case Ajian.GelarSupitUrang: return "Gelar Supit Urang";
                default: return "Pancasona";
            }
        }

        public static string Description(Ajian a)
        {
            switch (a)
            {
                case Ajian.TamengWaja: return "Bidak ini kebal dimakan selama giliran lawan berikutnya.";
                case Ajian.SaipiAngin: return "Pendekar ini boleh bergerak 2 kali di giliran ini.";
                case Ajian.Panglimunan: return "Telik Sandi tak terlihat dan tak bisa dimakan selama 2 giliran lawan.";
                case Ajian.Santet: return "Pilih 1 bidak lawan (maks. 3 langkah): tidak bisa bergerak 2 giliran.";
                case Ajian.GelarSupitUrang: return "Prajurit dalam 2 langkah dari Senopati maju 1 langkah bersamaan.";
                default: return "Sekali per match: saat Patih akan gugur, kamu ditanya. Kalau dipakai, Patih hidup lagi di titik awalnya.";
            }
        }
    }

    /// <summary>What casting an ajian did, for the view and the log.</summary>
    public sealed class CastResult
    {
        public Ajian Ajian;
        public Side Caster;
        public int CasterPoint;
        public int Target;
        /// <summary>Pieces moved by the ajian (Gelar Supit Urang), as (from, to).</summary>
        public List<(int from, int to)> Shifts = new List<(int, int)>();
        public List<int> Promoted = new List<int>();
    }
}
