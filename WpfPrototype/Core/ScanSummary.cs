namespace AchievementTimelineReplay
{
    internal sealed class ScanSummary
    {
        public int Reviewed,Recovered,NoSchema,NoDates,ReadErrors,SnapshotErrors,Duplicates,InvalidIds,OtherAccounts;
        public int Classified => Recovered+NoSchema+NoDates+ReadErrors+SnapshotErrors+Duplicates+InvalidIds;
        public string Format(string source) => $"{source}: {Reviewed} revisados · {Recovered} recuperados · {NoDates} sin logros fechados · {NoSchema} sin esquema · {ReadErrors} errores de lectura · {SnapshotErrors} errores de snapshot · {Duplicates} duplicados · {InvalidIds} IDs inválidos"+(OtherAccounts>0?$" · {OtherAccounts} de otras cuentas excluidos antes de revisar":"");
    }
}
