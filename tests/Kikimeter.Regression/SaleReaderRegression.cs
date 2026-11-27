using System;
using System.IO;
using System.Collections.Generic;
using GameOverlay.Kikimeter.Services;

internal static class SaleReaderRegression
{
    public static void Run()
    {
        string file = Path.GetTempFileName();
        const string sale = "01:00:00,000 - [Information (jeu)] Vous avez vendu 2 objets pour un prix total de 1\u202f500§";
        try
        {
            File.WriteAllText(file, sale + "\n");
            using var tracker = new SaleTracker(file);
            var amounts = new List<long>();
            tracker.SaleDetected += (_, info) => amounts.Add(info.TotalKamas);
            tracker.ManualRead();
            if (amounts.Count != 0) throw new InvalidOperationException("Old sale replayed");
            File.AppendAllText(file, sale + "\n" + sale + "\n");
            tracker.ManualRead();
            tracker.ManualRead();
            if (amounts.Count != 2 || amounts[0] != 1500 || amounts[1] != 1500)
                throw new InvalidOperationException("Equal legitimate sales suppressed or replayed");
            File.AppendAllText(file, sale);
            tracker.ManualRead();
            if (amounts.Count != 2) throw new InvalidOperationException("Partial line emitted");
            File.AppendAllText(file, "\n");
            tracker.ManualRead();
            if (amounts.Count != 3) throw new InvalidOperationException("Complete line missed");
            if (SaleTracker.ParseSystemSale("01:00:00,000 - [Commerce] Player : 2 objets vendus pour un total de 1500§") != null)
                throw new InvalidOperationException("Commerce message treated as personal sale");
        }
        finally { File.Delete(file); }
    }
}
