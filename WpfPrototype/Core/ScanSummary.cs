namespace AchievementTimelineReplay
{
    internal sealed class ScanSummary
    {
        public int Reviewed,Recovered,NoSchema,NoDates,ReadErrors,SnapshotErrors,Duplicates,InvalidIds,OtherAccounts,UnmappedHistories,UnmatchedDates;
        public int Classified => Recovered+NoSchema+NoDates+ReadErrors+SnapshotErrors+Duplicates+InvalidIds+UnmappedHistories;
        public string Format(string source) => $"{source}: {Reviewed} revisados · {Recovered} recuperados · {NoDates} sin logros fechados · {UnmappedHistories} historiales sin correspondencia · {UnmatchedDates} fechas sin correspondencia · {NoSchema} sin esquema · {ReadErrors} errores de lectura · {SnapshotErrors} errores de snapshot · {Duplicates} duplicados · {InvalidIds} IDs inválidos"+(OtherAccounts>0?$" · {OtherAccounts} de otras cuentas excluidos antes de revisar":"");
    }
}
